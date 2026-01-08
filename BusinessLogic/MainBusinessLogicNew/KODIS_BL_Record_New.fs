namespace BusinessLogicNew

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
                                |> fun a -> Async.RunSynchronously (a, cancellationToken = token)

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
    
    let internal downloadAndSave token context =  
   
        IO (fun ()
                ->
                let downloadWithResume (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, PdfDownloadErrors>> = 
               
                    async 
                        {
                            let maxRetries = 5                            
                            let initialBackoffMs = 1000
   
                            let rec attempt retryCount (backoffMs : int) = 

                                async 
                                    {
                                        token.ThrowIfCancellationRequested()
   
                                        let existingFileLength =                               
                                            runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                            |> function
                                                | Some _ -> (FileInfo pathToFile).Length
                                                | None   -> 0L
                                      
                                        let headerContent1 = "Range" 
                                        let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                                      
                                        let request =
                                            match existingFileLength > 0L with
                                            | true  -> 
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds timeOutInSeconds2 
                                                            config_cancellationToken token 
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                            header headerContent1 headerContent2
                                                        }
                                            | false ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds timeOutInSeconds2 
                                                            config_cancellationToken token 
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                        }     

                                        match! Request.sendAsync >> Async.Catch <| request with
                                        | Choice1Of2 response
                                            ->
                                            use response = response

                                            match response.statusCode with
                                            | HttpStatusCode.OK
                                            | HttpStatusCode.PartialContent 
                                                ->
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask
                                                    use fileStream = new FileStream(pathToFile, FileMode.Append, FileAccess.Write, FileShare.None)
                                                    do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                    return Ok ()
                                                with
                                                | ex 
                                                    ->
                                                    match Helpers.ExceptionHelpers.isCancellation token ex with
                                                    | err 
                                                        when err = StopDownloading
                                                        ->
                                                        runIO (postToLog <| string ex.Message <| "#123456J")
                                                        return Error StopDownloading
                                                    | err 
                                                        ->
                                                        runIO (postToLog <| string ex.Message <| "#3352")
                                                        return Error err 
   
                                            | HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog () (sprintf "%s Forbidden 403 #2211" uri) 
                                                return Error FileDownloadError
   
                                            | status
                                                ->
                                                runIO <| postToLog (string status) "#2212" 
                                                return Error FileDownloadError
   
                                        | Choice2Of2 ex 
                                            ->
                                            match Helpers.ExceptionHelpers.isCancellation token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#123456H")
                                                return Error StopDownloading
                                            | err 
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog (string ex.Message) (sprintf "#7024 (retry %d)" retryCount) 
                                                        return Error err    
                                    }
   
                            return! attempt 0 initialBackoffMs
                    }
   
                let downloadAndSaveTimetables (token : CancellationToken) context =                                         
         
                    let l = context.list |> List.length
                            in
                            let counterAndProgressBar =
                                MailboxProcessor<MsgIncrement>
                                    .StartImmediate
                                        <|
                                        fun inbox 
                                            ->   
                                            let rec loop n = 
                                                async
                                                    {
                                                        try
                                                            let! Inc i = inbox.Receive()
                                                            context.reportProgress (float n, float l)
                                                            return! loop (n + i)
                                                        with
                                                        | ex -> runIO (postToLog <| ex.Message <| "#903-MP")
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
                               
                    try
                        (token, uri, pathToFile)
                        |||> List.Parallel.map2_IO_Token_Async                                    
                            (fun uri (pathToFile : string) 
                                -> 
                                async
                                    {
                                        try
                                            counterAndProgressBar.Post <| Inc 1

                                            token.ThrowIfCancellationRequested()
   
                                            // my original safety check – keep it to avoid re-downloading finished PDFs)
                                            let pathToFileExistFirstCheck =
                                                runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
   
                                            match pathToFileExistFirstCheck with
                                            | Some _ 
                                                ->
                                                // File already exists → skip download entirely
                                                return Ok ()
   
                                            | None 
                                                ->
                                                // File does not exist (or was deleted) → download (with resume if partial)
                                                let result = downloadWithResume uri pathToFile token                                                    
                                                          
                                                match! result with
                                                | Ok _    
                                                    -> 
                                                    return Ok ()

                                                | Error err 
                                                    ->
                                                    match err with
                                                    | err 
                                                        when err = StopDownloading
                                                        ->
                                                        runIO (postToLog <| string err <| "#123456G")
                                                        return Error <| PdfError StopDownloading
                                                    | err 
                                                        ->
                                                        runIO (postToLog <| string err <| "#7028")
                                                        return Error <| PdfError err  
                                              
                                        with
                                        | ex
                                            ->
                                            match Helpers.ExceptionHelpers.isCancellation token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#123456F")
                                                return Error <| PdfError StopDownloading
                                            | err 
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#024")
                                                return Error <| PdfError err 
                                    }                                 
                            )

                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token) 

                    with
                    | ex 
                        ->
                        match Helpers.ExceptionHelpers.isCancellation token ex with
                        | err 
                            when err = StopDownloading 
                            ->
                            runIO (postToLog (string ex.Message) "#123456E") 
                            [ Error (PdfError StopDownloading) ]
                        | err 
                            ->
                            runIO (postToLog (ex.Message) "#024-6") 
                            [ Error (PdfError err) ]
                               
                    |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                    |> Option.defaultValue (Ok ())  
                        
                match context.dir |> Directory.Exists with  //TOCTOU race condition by tady nemel byt problem
                | false ->
                    runIO (postToLog NoFolderError "#251")
                    Error (PdfError NoFolderError)  
                | true  ->                                   
                    match context.list with
                    | [] 
                        -> 
                        Ok String.Empty     
                    | _ -> 
                        match downloadAndSaveTimetables token context with
                        | Ok _       -> Ok String.Empty
                        | Error case -> Error case                         
        )