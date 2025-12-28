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
                                    let rec loop n = 
                                        async { match! inbox.Receive() with Inc i -> reportProgress (float n, float l); return! loop (n + i) }
                                    loop 0
      
                try 
                    (token, jsonLinkList, pathToJsonList)
                    |||> List.Parallel.map2_IO_Token 
                        (fun uri (pathToFile : string) 
                            ->                                
                            counterAndProgressBar.Post <| Inc 1                           
                            
                            token.ThrowIfCancellationRequested ()                            
                            
                            let existingFileLength = 
                                try
                                    let fileInfo = FileInfo pathToFile
                                    match fileInfo.Exists with  //TOCTOU tady bude vzdy, dle copilota tato verze je nejmensi problem
                                    | true  -> fileInfo.Length
                                    | false -> 0L
                                                   
                                with
                                |_ -> 0L

                            let request uri =
                                match existingFileLength with
                                | 0L 
                                    ->
                                    http
                                        {
                                            GET uri
                                            config_timeoutInSeconds 30
                                            config_cancellationToken token
                                            header "User-Agent" "FsHttp/Android"
                                        }

                                | length when length > 0L
                                    ->
                                    http
                                        {
                                            GET uri
                                            config_timeoutInSeconds 30
                                            config_cancellationToken token
                                            header "User-Agent" "FsHttp/Android"
                                            header "Range" (sprintf "bytes=%d-" length)
                                        } 

                                | _ 
                                    ->
                                    runIO (postToLog <| "pathToFileExistCheck failed" <| "#2282-Json")
                                    http { GET uri }                                                                                                                                    
                                                   
                            let response = request >> Request.send <| uri      
                            let statusCode = response.statusCode
                                                 
                            match statusCode with
                            | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                ->         
                                Ok <| response.SaveFile pathToFile
                                        
                            | HttpStatusCode.Forbidden 
                                ->
                                runIO <| postToLog () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2211-Json") 
                                Error JsonDownloadError
    
                            | status
                                ->
                                runIO (postToLog <| (string status) <| "#2212-Json")
                                Error JsonDownloadError
                        )  
                   |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                   |> Option.defaultValue (Ok ())                   
                                
                with
                | ex                             
                    -> 
                    match Helpers.ExceptionHelpers.isCancellation ex with
                    | true
                       ->
                       Error StopJsonDownloading
                    | false 
                       ->
                       runIO (postToLog <| string ex.Message <| "#020")
                       Error JsonDownloadError
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
                                                 let rec loop n = 
                                                     async { match! inbox.Receive() with Inc i -> context.reportProgress (float n, float l); return! loop (n + i) }
                                                 loop 0
                                                             
                            return    
                                try 
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
    
                                    (token, uri, pathToFile)
                                    |||> List.Parallel.map2_IO_Token //context.listMappingFunction                            
                                        (fun uri (pathToFile : string) 
                                            -> 
                                            counterAndProgressBar.Post <| Inc 1
                                                
                                            // Artificial checkpoint
                                            token.ThrowIfCancellationRequested () 
    
                                            let existingFileLength = 
                                                try
                                                    let fileInfo = FileInfo pathToFile
                                                    match fileInfo.Exists with  //TOCTOU tady bude vzdy, dle copilota tato verze je nejmensi problem
                                                    | true  -> fileInfo.Length
                                                    | false -> 0L
                                                   
                                                with
                                                |_ -> 0L

                                            let request uri =
                                                match existingFileLength with
                                                | 0L 
                                                    ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds 30
                                                            config_cancellationToken token
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                        }

                                                | length when length > 0L
                                                    ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds 30
                                                            config_cancellationToken token
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                            header "Range" (sprintf "bytes=%d-" length)
                                                        } 

                                                | _ 
                                                    ->
                                                    runIO (postToLog <| "pathToFileExistCheck failed" <| "#2212-43")
                                                    http { GET uri }                                                                                                                                    
                                                                   
                                            let response = request >> Request.send <| uri      
                                            let statusCode = response.statusCode
                                                 
                                            match statusCode with
                                            | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                                ->         
                                                Ok <| response.SaveFile pathToFile
                                                                 
                                            | HttpStatusCode.Forbidden 
                                                ->
                                                runIO <| postToLog () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2211-44") 
                                                Error <| PdfError FileDownloadError
    
                                            | status 
                                                ->
                                                runIO (postToLog <| (string status) <| "#2212-45")
                                                Error <| PdfError FileDownloadError                                                                            
                                        )         
                                    |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                                    |> Option.defaultValue (Ok ())                                      
                                
                                with
                                | ex                             
                                    -> 
                                    match Helpers.ExceptionHelpers.isCancellation ex with
                                    | true
                                        ->
                                        Error <| PdfError StopDownloading
                                    | false 
                                        ->
                                        //runIO (postToLog <| string ex.Message <| "#024")
                                        Error <| PdfError FileDownloadError
                         } 
     
                reader
                    {    
                        let! context = fun env -> env

                        return                       
                            try
                                match context.list with
                                | [] 
                                    -> 
                                    Ok String.Empty     
                                | _ 
                                    -> 
                                    downloadAndSaveTimetables token context
                                    |> Result.map (fun _ -> String.Empty)
                            with
                            | ex 
                                ->  
                                runIO (postToLog <| string ex.Message <| "#251")
                                Error <| PdfError NoFolderError                       
                    }       
        )