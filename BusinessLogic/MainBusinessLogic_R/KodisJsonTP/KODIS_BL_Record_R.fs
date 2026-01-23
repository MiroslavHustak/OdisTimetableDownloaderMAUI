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
open Api.FutureValidityRestApi 

open Settings.SettingsGeneral

open Helpers
open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

module KODIS_BL_Record =       
   
    let internal downloadAndSave variant token context =  
   
        IO (fun ()
                ->
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let l = context.list |> List.length

                let counterAndProgressBar =
                        MailboxProcessor<MsgIncrement2>.StartImmediate 
                            <|
                            fun inbox 
                                ->
                                let rec loop n =
                                    async
                                        {
                                            try
                                                let! msg = inbox.Receive()

                                                match msg with
                                                | Inc2 i 
                                                    ->
                                                    context.reportProgress (float n, float l)
                                                    return! loop (n + i)
                                                | GetCount2 replyChannel //not used anymore, kept for educational purposes
                                                    ->
                                                    replyChannel.Reply n
                                                    return! loop n
                                                | Stop2  
                                                    ->
                                                    return ()
                                            with
                                            | ex -> () //runIO (postToLog2 <| string ex.Message <| "#0013-KBL")
                                        }
                                loop 0

                let downloadWithResume (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, PdfDownloadErrors>> = 
               
                    async 
                        {
                            let maxRetries = maxRetries500                            
                            let initialBackoffMs = delayMs
   
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
                                                when existingFileLength > 0L 
                                                ->
                                                // Server ignored Range header → unsafe to append
                                                runIO <| postToLog2 () (sprintf "%s ignored Range header #RANGE" uri)
                                                return Error FileDownloadError
                                            | HttpStatusCode.OK
                                            | HttpStatusCode.PartialContent 
                                                ->
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask
                                                    use fileStream = new FileStream(pathToFile, FileMode.Append, FileAccess.Write, FileShare.None)
                                                    do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                    do! fileStream.FlushAsync(token) |> Async.AwaitTask
                                                   
                                                    return Ok ()
                                                with                                               
                                                | ex
                                                    ->
                                                    checkCancel token
                                                    //runIO (postToLog2 <| string ex.Message <| "#0008-KBL")  //in order not to log cancellation
                                                    return 
                                                        runIO <| comprehensiveTryWith 
                                                            LetItBe StopDownloading TimeoutError 
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
                                            match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError token ex with
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
                                                            runIO <| comprehensiveTryWith
                                                                LetItBe StopDownloading TimeoutError
                                                                FileDownloadError TlsHandshakeError token ex  
                                    }
   
                            return! attempt 0 initialBackoffMs
                    }
   
                let downloadAndSaveTimetables variant (token : CancellationToken) =                       
                                                 
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
                        
                        // *********  Temporary code *********

                        asyncOption // a little bit overengineered :-)
                            {
                                do! Option.fromBool () (variant = FutureValidity)
                                return! runIO <| putFutureLinksToRestApi token uri
                            }
                        |> Async.Ignore<unit option> 
                        |> Async.Start  //slo by paralelne s nize, ale vzhledem k tomu, ze fire and forget je tady v poho, overhead vubec nestoji za to

                        //************************************
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
                                                counterAndProgressBar.Post <| Inc2 1
                                                return Ok ()
   
                                            | None 
                                                ->
                                                // File does not exist (or was deleted) → download (with resume if partial)
                                                let! result = downloadWithResume uri pathToFile token 
                                                counterAndProgressBar.Post <| Inc2 1

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
                        checkCancel token
                        //runIO (postToLog2 <| string ex.Message <| "#0017-KBL")  //in order not to log cancellation
                        async
                            {
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
                            downloadAndSaveTimetables variant token 
                            |> fun a -> Async.RunSynchronously(a, cancellationToken = token)  
                        
                        context.reportProgress (float l, float l)
                        counterAndProgressBar.Post Stop2
                        
                        let! _ = result |> List.length = l, Error (PdfDownloadError2 FileDownloadError)

                        return
                            result
                            |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                            |> Option.defaultValue (Ok ())  
                            |> Result.map (fun _ -> String.Empty)  
                    }   
        )