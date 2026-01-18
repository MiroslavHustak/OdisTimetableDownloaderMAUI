namespace BusinessLogic_R

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

open Api.Logging
open Api.FutureLinks

open Settings.SettingsGeneral

open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

module KODIS_BL_Record =       
           
    //************************ Main code **********************************

    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress =
    
        IO (fun () 
                ->
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()
    
                let l = jsonLinkList |> List.length
    
                let counterAndProgressBar =
                    MailboxProcessor<MsgIncrement>
                        .StartImmediate
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
                                            | ex -> () //runIO (postToLog2 <| string ex.Message <| "#0001-KBLJson")
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
                                        checkCancel token

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
                                                    runIO (postToLog2 <| string ex.Message <| "#0002-KBLJson")
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
                                                    checkCancel token
                                                    return
                                                        comprehensiveTryWith 
                                                            JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                                                            JsonDownloadError JsonTlsHandshakeError token ex  

                                            | _, HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog2 () (sprintf "%s Forbidden 403 #0003-KBLJson" uri)
                                                return Error JsonDownloadError

                                            | status
                                                ->
                                                runIO (postToLog2 (string status) "#0004-KBLJson")
                                                return Error JsonDownloadError

                                        | Choice2Of2 ex
                                            ->
                                            match isCancellationGeneric JsonLetItBeKodis StopJsonDownloading JsonTimeoutError JsonDownloadError token ex with
                                            | err
                                                when err = StopJsonDownloading
                                                ->
                                                //runIO (postToLog2 <| string ex.Message <| "#0005-KBLJson")  //in order not to log cancellation
                                                return Error StopJsonDownloading
                                            | err
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog2 (string ex.Message) (sprintf "#0006-KBLJson (retry %d)" retryCount)
                                                        return 
                                                            comprehensiveTryWith 
                                                                JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                                                                JsonDownloadError JsonTlsHandshakeError token ex
                                    }

                            return! attempt 0 initialBackoffMs
                        }
                    
                try
                    checkCancel token

                    (token, jsonLinkList, pathToJsonList)
                    |||> List.Parallel.map2_IO_AW_Token_Async 
                        (fun uri pathToFile 
                            ->
                            async 
                                {
                                    try
                                        checkCancel token
                                        counterAndProgressBar.Post <| Inc 1    

                                        return! downloadWithResume uri pathToFile
                                    with                                               
                                    | ex 
                                        -> 
                                        checkCancel token
                                        //runIO (postToLog2 <| string ex.Message <| "#0007-KBLJson")  //in order not to log cancellation
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
                    checkCancel token
                    comprehensiveTryWith 
                        JsonLetItBeKodis StopJsonDownloading JsonTimeoutError 
                        JsonDownloadError JsonTlsHandshakeError token ex
        )    
    
    let internal downloadAndSave variant token context =  
   
        IO (fun ()
                ->
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let downloadWithResume (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, PdfDownloadErrors>> = 
               
                    async 
                        {
                            let maxRetries = 5                            
                            let initialBackoffMs = 1000
   
                            let rec attempt retryCount (backoffMs : int) = 

                                async 
                                    {
                                        checkCancel token
   
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
                                                    checkCancel token
                                                    //runIO (postToLog2 <| string ex.Message <| "#0008-KBL")  //in order not to log cancellation
                                                    return 
                                                        comprehensiveTryWith 
                                                            LetItBeKodis4 StopDownloading TimeoutError 
                                                            FileDownloadError TlsHandshakeError token ex
   
                                            | HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog2 () (sprintf "%s Forbidden 403 #0009-KBL" uri) 
                                                return Error FileDownloadError
   
                                            | status
                                                ->
                                                runIO <| postToLog2 (string status) "#0010-KBL" 
                                                return Error FileDownloadError
   
                                        | Choice2Of2 ex 
                                            ->
                                            match isCancellationGeneric LetItBeKodis4 StopDownloading TimeoutError FileDownloadError token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                //runIO (postToLog2 <| string ex.Message <| "#0011-KBL")  //in order not to log cancellation
                                                return Error StopDownloading
                                            | err 
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog2 (string ex.Message) (sprintf "#0012-KBL (retry %d)" retryCount) 
                                                        return 
                                                            comprehensiveTryWith
                                                                LetItBeKodis4 StopDownloading TimeoutError
                                                                FileDownloadError TlsHandshakeError token ex  
                                    }
   
                            return! attempt 0 initialBackoffMs
                    }
   
                let downloadAndSaveTimetables variant (token : CancellationToken) context =                                         
         
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
                                                    match variant = FutureValidity || n = 2 with 
                                                    | true  -> do! putFutureLinksToRestApi >> runIO <| context.list //temporary solution until KODIS make things right
                                                    | false -> ()
                                                    context.reportProgress (float n, float l)
                                                    return! loop (n + i)
                                                with
                                                | ex -> () //runIO (postToLog2 <| string ex.Message <| "#0013-KBL")  
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
                        checkCancel token

                        (token, uri, pathToFile)
                        |||> List.Parallel.map2_IO_AW_Token_Async                                    
                            (fun uri (pathToFile : string) 
                                -> 
                                async
                                    {
                                        try
                                            checkCancel token 
                                            counterAndProgressBar.Post <| Inc 1
   
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
                                                                //runIO (postToLog2 <| string err <| "#0014-KBL")  //in order not to log cancellation
                                                                PdfDownloadError2 StopDownloading
                                                            | err 
                                                                ->
                                                                runIO (postToLog2 <| string err <| "#0015-KBL")
                                                                PdfDownloadError2  err 
                                                        )                                               
                                        with                                        
                                        | ex
                                            -> 
                                            checkCancel token
                                            //runIO (postToLog2 <| string ex.Message <| "#0016-KBL")  //in order not to log cancellation
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
                        checkCancel token
                        //runIO (postToLog2 <| string ex.Message <| "#0017-KBL")  //in order not to log cancellation
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
                            downloadAndSaveTimetables variant token context
                            |> Result.map (fun _ -> String.Empty)       
                    }                  
        )