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

open Api.Logging
open Settings.SettingsGeneral

open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

module KODIS_BL_Record4 =   // Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu    

    let internal downloadAndSave (token : CancellationToken) context =  
   
        IO (fun ()
                ->
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let l = context.list |> List.length

                let counterAndProgressBar =
                    MailboxProcessor<MsgIncrement>.StartImmediate
                        <|
                        fun inbox 
                            ->   
                            let rec loop n = 
                                async
                                    {
                                        try
                                            checkCancel token      
                                            let! msg = inbox.Receive()
                                                    
                                            match msg with
                                            | Inc i 
                                                -> 
                                                context.reportProgress (float n, float l)
                                                return! loop (n + i)
                                            | Stop
                                                ->
                                                return () // exit loop → agent terminates
                                        with
                                        | _ -> () 
                                    }
                            loop 0                               
                
                let downloadWithResume (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, PdfDownloadErrors>> = 
               
                    async 
                        {
                            let maxRetries = maxRetries4                           
                            let initialBackoffMs = delayMs

                            checkCancel token
   
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
                                                            config_timeoutInSeconds (umSecondsToFloat timeOutInSeconds2) 
                                                            config_cancellationToken token 
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                            header headerContent1 headerContent2
                                                        }
                                            | false ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds (umSecondsToFloat timeOutInSeconds2) 
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
                                                match retryCount < maxRetries with
                                                | true 
                                                    ->
                                                    try
                                                        File.Delete pathToFile
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                    with
                                                    | ex
                                                        ->
                                                        runIO (postToLog2 <| string ex.Message <| "#0005-K4BL")
                                                        return Error FileDownloadError
                                                | false 
                                                    ->
                                                    runIO (postToLog2 "Max retries on ignored Range header" "#0006-K4BL")
                                                    return Error FileDownloadError
                                            | _, HttpStatusCode.OK         
                                            | _, HttpStatusCode.PartialContent 
                                                ->   
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask
                                                    // Decide file mode based on whether we're resuming or starting fresh
                                                    let fileMode =
                                                        match existingFileLength > 0L with
                                                        | true  -> FileMode.Append
                                                        | false -> FileMode.Create
                                                    use fileStream = new FileStream(pathToFile, fileMode, FileAccess.Write, FileShare.None)                                        
                                                    do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                    do! fileStream.FlushAsync token |> Async.AwaitTask

                                                    return Ok ()
                                                with 
                                                | ex
                                                    ->
                                                    checkCancel token
                                                    runIO (postToLog2 <| string ex.Message <| "#0066-K4BL")  
                                                    return
                                                        runIO <| comprehensiveTryWith
                                                            LetItBe StopDownloading TimeoutError
                                                            FileDownloadError TlsHandshakeError token ex
                                            | _, HttpStatusCode.Forbidden 
                                                ->
                                                runIO <| postToLog2 () (sprintf "%s Forbidden 403 #0007-K4BL" uri)
                                                return Error FileDownloadError
                                            | _, status 
                                                ->
                                                runIO <| postToLog2 (string status) "#0008-K4BL"
                                                return Error FileDownloadError
                                           
                                           
                                        | Choice2Of2 ex 
                                            ->
                                            match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                runIO (postToLog2 <| string ex.Message <| "#0009-K4BL")  
                                                return Error StopDownloading
                                            | _ 
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog2 (string ex.Message) (sprintf "#0010-K4BL (retry %d)" retryCount) 
                                                        return 
                                                            runIO <| comprehensiveTryWith
                                                                LetItBe StopDownloading TimeoutError
                                                                FileDownloadError TlsHandshakeError token ex
                                    }
   
                            return! attempt 0 (umMiliSecondsToInt32 initialBackoffMs)
                    }
   
                let downloadAndSaveTimetables (token : CancellationToken) context =                                         
                             
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
                                            
                                            let pathToFileExistFirstCheck =
                                                runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
                                            
                                            match pathToFileExistFirstCheck with
                                            | Some _ 
                                                ->
                                                // File already exists → skip download entirely
                                                counterAndProgressBar.Post <| Inc 1                                                
                                                return Ok ()
   
                                            | None 
                                                ->
                                                // File does not exist (or was deleted) → download (with resume if partial)
                                                let! result = downloadWithResume uri pathToFile token 

                                                //runIO (postToLog2 (sprintf "downloadWithResume returned: %A for %s" result uri) "#DEBUG-K4BL")

                                                counterAndProgressBar.Post <| Inc 1
                                                
                                                return
                                                    result
                                                    |> Result.mapError(
                                                        function
                                                            | err 
                                                                when err = StopDownloading
                                                                ->
                                                                runIO (postToLog2 <| string err <| "#0009-K4BL") 
                                                                PdfDownloadError2 StopDownloading
                                                            | err 
                                                                ->
                                                                runIO (postToLog2 <| string err <| "#0010-K4BL")
                                                                PdfDownloadError2  err 
                                                        )                                               
                                        with                                        
                                        | ex
                                            -> 
                                            checkCancel token
                                            runIO (postToLog2 <| string ex.Message <| "#0011-K4BL")  
                                            return 
                                                runIO <| comprehensiveTryWith
                                                    (PdfDownloadError2 LetItBe)
                                                    (PdfDownloadError2 StopDownloading)
                                                    (PdfDownloadError2 TimeoutError) 
                                                    (PdfDownloadError2 FileDownloadError) 
                                                    (PdfDownloadError2 TlsHandshakeError)
                                                    token ex
                                    }                                 
                            )  
                    with
                    | ex 
                        -> 
                        runIO (postToLog2 <| string ex.Message <| "#0012-K4BL")  
                        async
                            {
                                checkCancel token

                                return
                                    [
                                        runIO <| comprehensiveTryWith 
                                            (PdfDownloadError2 LetItBe)
                                            (PdfDownloadError2 StopDownloading)
                                            (PdfDownloadError2 TimeoutError)
                                            (PdfDownloadError2 FileDownloadError)
                                            (PdfDownloadError2 TlsHandshakeError)
                                            token ex 
                                    ]
                            }
                    
                pyramidOfDamnation //TOCTOU race condition by tady nemel byt problem       
                    {
                        let! _ = context.dir |> Directory.Exists, Error (PdfDownloadError2 NoFolderError)
                        let! _ = context.list <> List.Empty, Ok String.Empty
                        
                        let result = 
                            downloadAndSaveTimetables token context 
                            |> fun a -> Async.RunSynchronously(a, cancellationToken = token)  
                        
                        context.reportProgress (float l, float l)
                        counterAndProgressBar.Post Stop

                        let! _ = result |> List.length = l, Error (PdfDownloadError2 NotAllFilesDownloaded)

                        runIO (postToLog3 <| result <| "#4444-K4BL")

                        return
                            result
                            |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                            |> Option.defaultValue (Ok ())  
                            |> Result.map (fun _ -> String.Empty)  
                    }   
        )