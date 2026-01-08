namespace BusinessLogic

open System
open System.IO
open System.Net
open System.Threading

//*******************

open FsHttp
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Settings.SettingsGeneral

open Api.Logging

open Helpers
open Helpers.Builders
open Helpers.StateMonad
open Helpers.DirFileHelper

open JsonData.ParseJsonData
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record =       
           
    //************************ Main code **********************************

    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress = //FsHttp

        IO (fun () 
                -> 
                let l = jsonLinkList |> List.length
                    in
                    let counterAndProgressBar =
                        MailboxProcessor<MsgIncrement>
                            .StartImmediate
                                <|
                                fun inbox 
                                    ->         
                                    //use _ = token.Register (fun () -> inbox.Post (Unchecked.defaultof<MsgIncrement>))
                                    
                                    let rec loop n = 
                                        async
                                            {
                                                try
                                                    let! Inc i = inbox.Receive()
                                                    reportProgress (float n, float l)
                                                    return! loop (n + i)
                                                with
                                                | ex -> runIO (postToLog <| string ex.Message <| "#900-MP")
                                            }
                                    loop 0      
              
                (token, jsonLinkList, pathToJsonList)
                |||> List.Parallel.map2_IO_Token 
                    (fun uri (pathToFile : string) 
                        ->     
                        try
                            counterAndProgressBar.Post <| Inc 1                           
                            
                            token.ThrowIfCancellationRequested ()                            
                                                                    
                            let existingFileLength =  // bez tohoto file checking mobilni app nefunguje, TOCTOU race zatim nebyl problem                             
                                runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                |> function
                                    | Some _ -> (FileInfo pathToFile).Length
                                    | None   -> 0L
                            
                            let get uri = 

                                let headerContent1 = "Range" 
                                let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                                
                                match existingFileLength > 0L with
                                | true  -> 
                                        http
                                            {
                                                GET uri
                                                config_timeoutInSeconds 60 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                config_cancellationToken token 
                                                header "User-Agent" "FsHttp/Android7.1"
                                                header headerContent1 headerContent2
                                            }
                                | false ->
                                        http
                                            {
                                                GET uri
                                                config_timeoutInSeconds 60 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                config_cancellationToken token 
                                                header "User-Agent" "FsHttp/Android7.1"
                                            }
                            
                            let runAsyncSafe a =
                                Async.Catch a
                                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)

                            match get >> Request.sendAsync >> runAsyncSafe <| uri with
                            | Choice1Of2 response 
                                -> 
                                try
                                    use response = response                                 
                                
                                    let statusCode = response.statusCode
                                
                                    match statusCode with
                                    | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                        -> 
                                        match (response.SaveFileAsync pathToFile) |> Async.AwaitTask |> runAsyncSafe with
                                        | Choice1Of2 result 
                                            -> 
                                            Ok result

                                        | Choice2Of2 _ 
                                            ->
                                            Error StopJsonDownloading
                                                                        
                                    | HttpStatusCode.Forbidden 
                                        ->
                                        runIO <| postToLog () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2211-Json") 
                                        Error JsonDownloadError
                                                                            
                                    | status
                                        ->
                                        runIO (postToLog <| (string status) <| "#2212-Json")
                                        Error JsonDownloadError 

                                with 
                                | ex 
                                    -> 
                                    runIO (postToLog <| string ex.Message <| "#2213-Json")
                                    Error JsonDownloadError
                                
                            | Choice2Of2 ex
                                ->
                                //runIO (postToLog <| string ex.Message <| "#2214-Json")
                                Error StopJsonDownloading  
                            
                        with
                        | ex 
                            -> // Cancellation pro json  downloading funguje jen s vnitrnim try with blokem
                            match Helpers.ExceptionHelpers.isCancellation token ex with
                            | err 
                                when err = StopDownloading
                                ->
                                runIO (postToLog <| string ex.Message <| "#123456W")
                                Error <| StopJsonDownloading
                            | err 
                                when err = TimeoutError
                                ->
                                runIO (postToLog <| string ex.Message <| "#020W")
                                Error <| JsonTimeoutError

                            | _ 
                                ->
                                runIO (postToLog <| string ex.Message <| "#020")
                                Error <| JsonDownloadError                             
                    )  
                |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                |> Option.defaultValue (Ok ())                  
            )
    
    let internal downloadAndSave token =  

        IO (fun () 
                ->         
                let downloadAndSaveTimetables (token : CancellationToken) =  
         
                    reader
                        {                           
                            let! context = fun env -> env 
         
                            let l = context.list |> List.length
                                 in
                                 let counterAndProgressBar =
                                     MailboxProcessor<MsgIncrement>
                                         .StartImmediate
                                             <|
                                             fun inbox 
                                                 ->   
                                                 //use _ = token.Register (fun () -> inbox.Post (Unchecked.defaultof<MsgIncrement>))
                                                 
                                                 let rec loop n = 
                                                    async
                                                        {
                                                            try
                                                                let! Inc i = inbox.Receive()
                                                                context.reportProgress (float n, float l)
                                                                return! loop (n + i)
                                                            with
                                                            | ex -> runIO (postToLog <| ex.Message <| "#901-MP")
                                                        }
                                                 loop 0
                                                 
                            //mel jsem 2x stejnou linku s jinym jsGeneratedString, takze uri bylo unikatni, ale cesta k souboru 2x stejna
                            let removeDuplicatePathPairs uri pathToFile =
                                (uri, pathToFile)
                                ||> List.zip 
                                |> List.distinctBy snd
                         
                            let uri, pathToFile =
                                context.list
                                |> List.distinct
                                |> List.unzip
                                |> fun (uri, pathToFile) -> removeDuplicatePathPairs uri pathToFile
                                |> List.unzip
                                                         
                            // State monad implementation test
                            //**************************************************************************************
                                 
                            let removeDuplicatePathPairsState2 (uriList : string list) (pathList : string list) =
    
                                let pairs = List.zip uriList pathList
                                 
                                let processPair (uri, path) =
    
                                    State
                                        (fun seen
                                            ->
                                            match Set.contains path seen with
                                            | true  -> (None, seen)
                                            | false -> (Some (uri, path), Set.add path seen)
                                        )
                                 
                                let computation =
    
                                    state
                                        {
                                            let! results =
                                                pairs
                                                |> List.fold 
                                                    (fun acc pair
                                                        ->
                                                        state
                                                            {
                                                                let! collected = acc
                                                                match! processPair pair with
                                                                | Some x -> return x :: collected
                                                                | None   -> return collected
                                                            }
                                                    )
                                                    (returnState [])
    
                                            return List.rev results
                                        }
                                 
                                fst (runState computation Set.empty)                                   
    
                            let uri2, pathToFile2 = removeDuplicatePathPairsState2 uri pathToFile |> List.unzip
    
                            //**************************************************************************************
                            
                            return   
                                try
                                    (token, uri, pathToFile)
                                    |||> List.Parallel.map2_IO_Token //context.listMappingFunction                            
                                        (fun uri (pathToFile : string) 
                                            -> 
                                            try 
                                                counterAndProgressBar.Post <| Inc 1
                                                
                                                // Artificial checkpoint
                                                token.ThrowIfCancellationRequested () 
    
                                                let pathToFileExistFirstCheck = // bez tohoto file checking mobilni app nefunguje, TOCTOU race zatim nebyl problem        
                                                    runIO <| checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                    in
                                                    match pathToFileExistFirstCheck with  
                                                    | Some _
                                                        -> 
                                                        let existingFileLength =                               
                                                            runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                                            |> function
                                                                | Some _ -> (FileInfo pathToFile).Length
                                                                | None   -> 0L
                                                 
                                                        let get uri = 
    
                                                            let headerContent1 = "Range" 
                                                            let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                       
                                                            match existingFileLength > 0L with
                                                            | true  -> 
                                                                    http
                                                                        {
                                                                            GET uri
                                                                            config_timeoutInSeconds timeOutInSeconds2 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                            config_cancellationToken token 
                                                                            header "User-Agent" "FsHttp/Android7.1"
                                                                            header headerContent1 headerContent2
                                                                        }
                                                            | false ->
                                                                    http
                                                                        {
                                                                            GET uri
                                                                            config_timeoutInSeconds timeOutInSeconds2 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                            config_cancellationToken token 
                                                                            header "User-Agent" "FsHttp/Android7.1"
                                                                        }    

                                                        let runAsyncSafe a =
                                                            Async.Catch a
                                                            |> fun a -> Async.RunSynchronously(a, cancellationToken = token)

                                                        match get >> Request.sendAsync >> runAsyncSafe <| uri with
                                                        | Choice1Of2 response 
                                                            -> 
                                                            try
                                                                use response = response

                                                                let statusCode = response.statusCode
                                                 
                                                                match statusCode with
                                                                | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                                                    ->  
                                                                    match (response.SaveFileAsync pathToFile) |> Async.AwaitTask |> runAsyncSafe with
                                                                    | Choice1Of2 result 
                                                                        -> 
                                                                        Ok result

                                                                    | Choice2Of2 ex 
                                                                        -> 
                                                                        match Helpers.ExceptionHelpers.isCancellation token ex with
                                                                        | err 
                                                                            when err = StopDownloading
                                                                            ->
                                                                            runIO (postToLog <| string ex.Message <| "#123456A")
                                                                            Error <| PdfError StopDownloading
                                                                        | err 
                                                                            ->
                                                                            runIO (postToLog <| string ex.Message <| "#024-K4")
                                                                            Error <| PdfError err                                                                 
                                                                 
                                                                | HttpStatusCode.Forbidden 
                                                                    ->
                                                                    runIO <| postToLog () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2211") 
                                                                    Error <| PdfError FileDownloadError
    
                                                                | status
                                                                    ->
                                                                    runIO (postToLog <| (string status) <| "#2212")
                                                                    Error <| PdfError FileDownloadError
                                                            with 
                                                            | ex 
                                                                -> 
                                                                runIO (postToLog <| string ex.Message <| "#2213")
                                                                Error <| PdfError FileDownloadError

                                                        | Choice2Of2 ex
                                                            ->
                                                            match Helpers.ExceptionHelpers.isCancellation token ex with
                                                            | err 
                                                                when err = StopDownloading
                                                                ->
                                                                runIO (postToLog <| string ex.Message <| "#123456B")
                                                                Error <| PdfError StopDownloading
                                                            | err 
                                                                ->
                                                                runIO (postToLog <| string ex.Message <| "#7024")
                                                                Error <| PdfError err            
                                                           
                                                    | None 
                                                        ->
                                                        runIO (postToLog <| "pathToFileExistFirstCheck failed" <| "#2230")
                                                        Error <| PdfError FileDownloadError      
                                            with
                                            | ex                             
                                                -> 
                                                match Helpers.ExceptionHelpers.isCancellation token ex with
                                                | err 
                                                    when err = StopDownloading
                                                    ->
                                                    runIO (postToLog <| string ex.Message <| "#123456C")
                                                    Error <| PdfError StopDownloading
                                                | err 
                                                    ->
                                                    runIO (postToLog <| string ex.Message <| "#024")
                                                    Error <| PdfError err             
                                        )  
                                with
                                | ex                             
                                    -> 
                                    match Helpers.ExceptionHelpers.isCancellation token ex with
                                    | err 
                                        when err = StopDownloading
                                        ->
                                        runIO (postToLog <| string ex.Message <| "#123456D")
                                        [ Error <| PdfError StopDownloading ]
                                    | err 
                                        ->
                                        runIO (postToLog <| string ex.Message <| "#024-6")
                                        [ Error <| PdfError err ]    
                                
                            |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                            |> Option.defaultValue (Ok ())                               
                         } 
     
                reader
                    {    
                        let! context = fun env -> env
             
                        return
                            match context.dir |> Directory.Exists with //TOCTOU race condition by tady nemel byt problem
                            | false ->
                                    runIO (postToLog <| NoFolderError <| "#251")
                                    Error <| PdfError NoFolderError  
                            | true  ->                                   
                                    match context.list with
                                    | [] 
                                        -> 
                                        Ok String.Empty     
                                    | _ 
                                        -> 
                                        match downloadAndSaveTimetables token context with
                                        | Ok _ 
                                            -> 
                                            Ok String.Empty
                                        
                                        | Error case 
                                            -> 
                                            Error case 
                    }       
        )