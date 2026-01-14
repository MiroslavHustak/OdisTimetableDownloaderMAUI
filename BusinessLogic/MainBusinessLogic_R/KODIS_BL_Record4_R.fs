namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//*******************

open FsHttp
open FSharp.Control
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open Api.FutureLinks

open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

open Settings.SettingsGeneral
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 Nic neni trvalejsiho, nez neco docasneho ...
    // 31-12-2025 ... kdo by to byl rekl, ze se nic nezmeni   
    
    let internal operationOnDataFromJson (token : CancellationToken) variant dir = 
    
        IO (fun () 
                ->           
                let result1 () : Async<Result<(string * string) list, ParsingAndDownloadingErrors>> = 
                    async
                        {
                            token.ThrowIfCancellationRequested() 

                            match! getFutureLinksFromRestApi >> runIO <| urlApi with
                            | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                            | Error err -> return Error <| PdfDownloadError2 err
                        }
    
                let result2 () : Async<Result<(string * string) list, ParsingAndDownloadingErrors>> = 
                    async
                        {
                            token.ThrowIfCancellationRequested() 

                            match variant with
                            | FutureValidity 
                                ->
                                match! getFutureLinksFromRestApi >> runIO <| urlJson with
                                | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                                | Error err -> return Error <| PdfDownloadError2 err
                            | _              
                                -> 
                                return Ok []
                        }
       
                async 
                    {
                        let! results = 
                            [| 
                                result1 ()
                                result2 ()
                            |]
                            |> Async.Parallel
                            |> Async.Catch
    
                        match results with
                        | Choice1Of2 resultsArray 
                            ->
                            return
                                match List.ofArray resultsArray with
                                | [ Ok list1; Ok list2 ]
                                    -> 
                                    Ok (List.distinct (list1 @ list2))

                                | [ Error err; _ ]    
                                    -> 
                                    runIO (postToLog <| err <| "#013")                                     
                                    Error err

                                | [ _; Error err ] 
                                    ->                                    
                                    runIO (postToLog <| err <| "#014")   
                                    Error err

                                | _                   
                                    ->
                                    runIO (postToLog <| JsonDataFilteringError <| "#015")                                    
                                    Error <| JsonParsingError2 JsonDataFilteringError

                        | Choice2Of2 ex
                            -> 
                            let cond = 
                                isCancellationGeneric 
                                    StopJsonParsing JsonParsingTimeoutError
                                    JsonDataFilteringError token ex

                            match cond with
                            | err 
                                when err = StopJsonParsing
                                ->
                                //runIO (postToLog <| string ex.Message <| "#123456C")
                                return Error <| JsonParsingError2 StopJsonParsing
                            | err 
                                ->
                                runIO (postToLog <| string ex.Message <| "#016")                      
                                return Error <| JsonParsingError2 JsonDataFilteringError 
                    }
                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        )    

    let internal downloadAndSave token context =  
   
        IO (fun ()
                ->
                let downloadWithResume (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, PdfDownloadErrors>> = 
               
                    async 
                        {
                            let maxRetries = 500                            
                            let initialBackoffMs = 1000

                            token.ThrowIfCancellationRequested() 
   
                            let rec attempt retryCount (backoffMs : int) = 

                                async 
                                    {
                                        token.ThrowIfCancellationRequested()
   
                                        let existingFileLength =                               
                                            runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                            |> Option.map (fun _ -> (FileInfo pathToFile).Length)
                                            |> Option.defaultValue 0L
                                      
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
                                                    return 
                                                        comprehensiveTryWith 
                                                            LetItBeKodis4 StopDownloading TimeoutError 
                                                            FileDownloadError TlsHandshakeError token ex
   
                                            | HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog () (sprintf "%s Forbidden 403 #2211-K4" uri) 
                                                return Error FileDownloadError
   
                                            | status
                                                ->
                                                runIO <| postToLog (string status) "#2212-K4" 
                                                return Error FileDownloadError
   
                                        | Choice2Of2 ex 
                                            ->
                                            match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                //runIO (postToLog <| string ex.Message <| "#123456H-K4")
                                                return Error StopDownloading
                                            | _ 
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog (string ex.Message) (sprintf "#7024-K4 (retry %d)" retryCount) 
                                                        return 
                                                            comprehensiveTryWith
                                                                LetItBeKodis4 StopDownloading TimeoutError
                                                                FileDownloadError TlsHandshakeError token ex    
                                    }
   
                            return! attempt 0 initialBackoffMs
                    }
   
                let downloadAndSaveTimetables (token : CancellationToken) context =                                         
         
                    let l = context.list |> List.length
                           
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
                                                | ex -> runIO (postToLog <| ex.Message <| "#903-MP-K4")
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
                        |||> List.Parallel.map2_IO_AW_Token_Async                                    
                            (fun uri (pathToFile : string) 
                                -> 
                                async
                                    {
                                        try
                                            counterAndProgressBar.Post <| Inc 1

                                            token.ThrowIfCancellationRequested() 
   
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
                                                        //runIO (postToLog <| string err <| "#123456G-K4")
                                                        return Error <| PdfDownloadError2 StopDownloading
                                                    | err 
                                                        ->
                                                        runIO (postToLog <| string err <| "#7028-K4")
                                                        return Error <| PdfDownloadError2 err  
                                        with                                        
                                        | ex
                                            -> 
                                            return 
                                                comprehensiveTryWith
                                                    (PdfDownloadError2 LetItBeKodis4)
                                                    (PdfDownloadError2 StopDownloading)
                                                    (PdfDownloadError2 TimeoutError) 
                                                    (PdfDownloadError2 FileDownloadError) 
                                                    (PdfDownloadError2 TlsHandshakeError)
                                                    token ex
                                    }                                 
                            )

                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token) 

                    with
                    | ex 
                        -> 
                        [
                            comprehensiveTryWith 
                                (PdfDownloadError2 LetItBeKodis4)
                                (PdfDownloadError2 StopDownloading)
                                (PdfDownloadError2 TimeoutError)
                                (PdfDownloadError2 FileDownloadError)
                                (PdfDownloadError2 TlsHandshakeError)
                                token ex 
                        ]
                                                              
                    |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                    |> Option.defaultValue (Ok ())  
                        
                match context.dir |> Directory.Exists with  //TOCTOU race condition by tady nemel byt problem
                | false ->
                        runIO (postToLog NoFolderError "#251-K4")
                        Error (PdfDownloadError2 NoFolderError)  
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