namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//*******************

open FsHttp
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open Settings.SettingsGeneral

open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

module KODIS_BL_Record =       
           
    //************************ Main code **********************************

    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress =
    
        IO (fun () ->
    
            let l = jsonLinkList |> List.length
    
            let counterAndProgressBar =
                MailboxProcessor<MsgIncrement>.StartImmediate
                    (fun inbox 
                        ->
                        let rec loop n =
                            async 
                                {
                                    try
                                        let! Inc i = inbox.Receive()
                                        reportProgress (float n, float l)
                                        return! loop (n + i)
                                    with
                                    | ex -> runIO (postToLog <| string ex.Message <| "#900-MP-Json")
                                }
                        loop 0
                )
    
            let downloadWithResume (uri : string) (pathToFile : string) : Async<Result<unit, JsonDownloadErrors>> =

                async
                    {
                        let maxRetries = 500
                        let initialBackoffMs = 1000

                        let rec attempt retryCount (backoffMs : int) =

                            async
                                {
                                    token.ThrowIfCancellationRequested()

                                    let existingFileLength =
                                        runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
                                        |> Option.map (fun _ -> (FileInfo pathToFile).Length)
                                        |> Option.defaultValue 0L

                                    let request =
                                        match existingFileLength > 0L with
                                        | true  ->
                                                http
                                                    {
                                                        GET uri
                                                        config_timeoutInSeconds timeOutInSeconds2
                                                        config_cancellationToken token
                                                        header "User-Agent" "FsHttp/Android7.1"
                                                        header "Range" (sprintf "bytes=%d-" existingFileLength)
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

                                        match existingFileLength, response.statusCode with                                        
                                        // Server ignored Range → full response → must restart download
                                        | length, HttpStatusCode.OK
                                            when length > 0L
                                            ->
                                            try
                                                File.Delete pathToFile
                                                return! attempt retryCount backoffMs
                                            with
                                            | ex
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#JSON-RANGE-RESET")
                                                return Error JsonDownloadError

                                        | _, HttpStatusCode.OK
                                        | _, HttpStatusCode.PartialContent
                                            ->
                                            try
                                                use! stream =
                                                    response.content.ReadAsStreamAsync()
                                                    |> Async.AwaitTask

                                                use fileStream =
                                                    match existingFileLength > 0L with
                                                    | true  ->
                                                            new FileStream
                                                                (
                                                                    pathToFile,
                                                                    FileMode.Append,
                                                                    FileAccess.Write,
                                                                    FileShare.None
                                                                )
                                                    | false ->
                                                            new FileStream
                                                                (
                                                                    pathToFile,
                                                                    FileMode.Create,
                                                                    FileAccess.Write,
                                                                    FileShare.None
                                                                )

                                                do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                return Ok ()
                                            with                                               
                                            | ex 
                                                -> 
                                                return
                                                    comprehensiveTryWith 
                                                        JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                                                        JsonDownloadError JsonTlsHandshakeError token ex  

                                        | _, HttpStatusCode.Forbidden
                                            ->
                                            runIO <| postToLog () (sprintf "%s Forbidden 403 #2211-Json" uri)
                                            return Error JsonDownloadError

                                        | status
                                            ->
                                            runIO (postToLog (string status) "#2212-Json")
                                            return Error JsonDownloadError

                                    | Choice2Of2 ex
                                        ->
                                        match isCancellationGeneric JsonLetItBeKodis StopJsonDownloading JsonTimeoutError JsonDownloadError token ex with
                                        | err
                                            when err = StopJsonDownloading
                                            ->
                                            //runIO (postToLog <| string ex.Message <| "#123456H-Json")
                                            return Error StopJsonDownloading
                                        | err
                                            ->
                                            match retryCount < maxRetries with
                                            | true  ->
                                                    do! Async.Sleep backoffMs
                                                    return! attempt (retryCount + 1) (backoffMs * 2)
                                            | false ->
                                                    runIO <| postToLog (string ex.Message) (sprintf "#7024-Json (retry %d)" retryCount)
                                                    return 
                                                        comprehensiveTryWith 
                                                            JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                                                            JsonDownloadError JsonTlsHandshakeError token ex
                                }

                        return! attempt 0 initialBackoffMs
                    }
                    
            try
                (token, jsonLinkList, pathToJsonList)
                |||> List.Parallel.map2_IO_AW_Token_Async 
                    (fun uri pathToFile 
                        ->
                        async 
                            {
                                try
                                    counterAndProgressBar.Post <| Inc 1    
                                    token.ThrowIfCancellationRequested() 

                                    return! downloadWithResume uri pathToFile
                                with                                               
                                | ex 
                                    -> 
                                    return
                                        comprehensiveTryWith 
                                            JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                                            JsonDownloadError JsonTlsHandshakeError token ex 
                            }
                    )

                |> fun a -> Async.RunSynchronously(a, cancellationToken = token) 
                |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                |> Option.defaultValue (Ok ())

            with                                               
            | ex 
                -> 
                comprehensiveTryWith 
                    JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                    JsonDownloadError JsonTlsHandshakeError token ex
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
                                                runIO <| postToLog () (sprintf "%s Forbidden 403 #2211" uri) 
                                                return Error FileDownloadError
   
                                            | status
                                                ->
                                                runIO <| postToLog (string status) "#2212" 
                                                return Error FileDownloadError
   
                                        | Choice2Of2 ex 
                                            ->
                                            match isCancellationGeneric LetItBeKodis4 StopDownloading TimeoutError FileDownloadError token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                //runIO (postToLog <| string ex.Message <| "#123456H")
                                                return Error StopDownloading
                                            | err 
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog (string ex.Message) (sprintf "#7024 (retry %d)" retryCount) 
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
                                                                //runIO (postToLog <| string err <| "#123456G")
                                                                PdfDownloadError2 StopDownloading
                                                            | err 
                                                                ->
                                                                runIO (postToLog <| string err <| "#7028")
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