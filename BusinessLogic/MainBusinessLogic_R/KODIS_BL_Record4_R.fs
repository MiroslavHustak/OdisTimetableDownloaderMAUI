namespace BusinessLogic_R

open System
open System.IO
open System.Net
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

open Applicatives.CummulativeResultApplicative

open Api.Logging
open Api.FutureLinks

open Helpers
open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

open Settings.SettingsGeneral
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 Nic neni trvalejsiho, nez neco docasneho ...
    // 31-12-2025 ... kdo by to byl rekl, ze se nic nezmeni   
    
    let internal operationOnDataFromJson2 (token : CancellationToken) variant dir =

        IO (fun () 
                ->
                let normaliseAsyncResult (token : CancellationToken) (a : Async<Result<'a, ParsingAndDownloadingErrors>>) =
                    async 
                        {
                            try
                                token.ThrowIfCancellationRequested()
                                let! r = a
                                return r |> Result.mapError List.singleton
                            with
                            | ex                                 
                                ->
                                runIO (postToLog <| string ex.Message <| "#016")
                                return Error [ JsonParsingError2 JsonDataFilteringError ]
                        }
    
                let process1 () =
                    async
                        {
                            token.ThrowIfCancellationRequested()

                            match! getFutureLinksFromRestApi >> runIO <| urlApi with
                            | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                            | Error err -> return Error <| PdfDownloadError2 err
                        }
    
                let process2 () =
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
                                normaliseAsyncResult token (process1 ())
                                normaliseAsyncResult token (process2 ())
                            |]
                            |> Async.Parallel
    
                        let result1 = Array.head results
                        let result2 = Array.last results
    
                        return
                            (fun l1 l2 -> l1 @ l2)
                            <!!!> result1
                            <***> result2

                            |> Result.map List.distinct
                    }

                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        )    

    let internal operationOnDataFromJson4 (token : CancellationToken) variant dir = 

        IO (fun ()
                ->
                async 
                    {

                        let normaliseAsyncResult (token : CancellationToken) (a : Async<Result<'a, ParsingAndDownloadingErrors>>) =
                            async 
                                {
                                    try
                                        token.ThrowIfCancellationRequested()
                                        let! r = a
                                        return r |> Result.mapError List.singleton
                                    with
                                    | ex                                 
                                        ->
                                        runIO (postToLog <| string ex.Message <| "#016")
                                        return Error [ JsonParsingError2 JsonDataFilteringError ]
                                }
    
                        let process1 () =
                            async
                                {
                                    token.ThrowIfCancellationRequested()

                                    match! getFutureLinksFromRestApi >> runIO <| urlApi with
                                    | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                                    | Error err -> return Error <| PdfDownloadError2 err
                                }
    
                        let process2 () =
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

                        token.ThrowIfCancellationRequested() 
    
                        let! r1 = normaliseAsyncResult token (process1 ())
                        let! r2 = normaliseAsyncResult token (process2 ())
    
                        return
                            validation
                                {
                                    let! links1 = r1
                                    and! links2 = r2
    
                                    return links1 @ links2 |> List.distinct
                                }
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
                                            match isCancellationGeneric LetItBeKodis4 StopDownloading TimeoutError FileDownloadError token ex with
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
                                                let! result = downloadWithResume uri pathToFile token 
                                                
                                                return
                                                    result
                                                    |> Result.mapError(
                                                        function
                                                            | err 
                                                                when err = StopDownloading
                                                                ->
                                                                //runIO (postToLog <| string err <| "#123456G-K4")
                                                                PdfDownloadError2 StopDownloading
                                                            | err 
                                                                ->
                                                                runIO (postToLog <| string err <| "#7028-K4")
                                                                PdfDownloadError2  err 
                                                        )                                               
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
                        
                pyramidOfDamnation //TOCTOU race condition by tady nemel byt problem       
                    {
                        let! _ = context.dir |> Directory.Exists, Error (PdfDownloadError2 NoFolderError)
                        let! _ = context.list <> List.Empty, Ok String.Empty
                    
                        return
                            downloadAndSaveTimetables token context
                            |> Result.map (fun _ -> String.Empty)       
                    }              
        )