namespace BusinessLogic_R

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
open Settings.SettingsGeneral

open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

module KODIS_BL_Record_Json =       
           
    //************************ Main code **********************************

    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress =
    
        IO (fun () 
                ->
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()
    
                let l = jsonLinkList |> List.length
    
                let counterAndProgressBar =
                    MailboxProcessor<MsgIncrement>.StartImmediate
                        (fun inbox 
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
                                                reportProgress (float n, float l)
                                                return! loop (n + i)
                                            | Stop
                                                ->
                                                return () // exit loop → agent terminates
                                        with
                                        | ex -> () //runIO (postToLog2 <| string ex.Message <| "#0001-KBLJson")
                                    }
                            loop 0
                    )
    
                let downloadWithResume (uri : string) (pathToFile : string) =

                    async
                        {
                            let maxRetries = maxRetries3
                            let initialBackoffMs = delayMs //delayMsJson

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
                                                            config_timeoutInSeconds (umSecondsToFloat timeOutInSeconds2)
                                                            config_cancellationToken token
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                            header "Range" (sprintf "bytes=%d-" existingFileLength)
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
                                                        runIO (postToLog2 <| string ex.Message <| "#0002-KBLJson")
                                                        return Error JsonDownloadError
                                                | false 
                                                    ->
                                                    runIO (postToLog2 "Max retries on ignored Range header" "#0007-KBLJson")
                                                    return Error JsonDownloadError

                                            | _, HttpStatusCode.OK
                                            | _, HttpStatusCode.PartialContent
                                                ->
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask
                                                    let fileMode =
                                                        match existingFileLength > 0L with
                                                        | true  -> FileMode.Append
                                                        | false -> FileMode.Create
                                                    use fileStream = new FileStream(pathToFile, fileMode, FileAccess.Write, FileShare.None)
                                                    do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                    do! fileStream.FlushAsync(token) |> Async.AwaitTask

                                                    return Ok ()
                                                with                                               
                                                | ex 
                                                    -> 
                                                    checkCancel token
                                                    runIO (postToLog2 <| string ex.Message <| "#0033-KBLJson")  
                                                    return
                                                        runIO <| comprehensiveTryWith 
                                                            JsonLetItBe StopJsonDownloading JsonTimeoutError 
                                                            JsonDownloadError JsonTlsHandshakeError token ex  

                                            | _, HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog2 () (sprintf "%s Forbidden 403 #0003-KBLJson" uri)
                                                return Error JsonDownloadError

                                            | _, status
                                                ->
                                                runIO (postToLog2 (string status) "#0004-KBLJson")
                                                return Error JsonDownloadError

                                        | Choice2Of2 ex
                                            ->
                                            match runIO <| isCancellationGeneric JsonLetItBe StopJsonDownloading JsonTimeoutError JsonDownloadError token ex with
                                            | err
                                                when err = StopJsonDownloading
                                                ->
                                                runIO (postToLog2 <| string ex.Message <| "#0005-KBLJson")  
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
                                                            runIO <| comprehensiveTryWith 
                                                                JsonLetItBe StopJsonDownloading JsonTimeoutError 
                                                                JsonDownloadError JsonTlsHandshakeError token ex
                                    }

                            return! attempt 0 (umMiliSecondsToInt32 initialBackoffMs)
                        }
                  
                let result =  
                
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
                                            let! result = downloadWithResume uri pathToFile
                                            counterAndProgressBar.Post <| Inc 1  

                                            return result  
                                        with                                               
                                        | ex 
                                            -> 
                                            checkCancel token
                                            runIO (postToLog2 <| string ex.Message <| "#0007-KBLJson")  
                                            return
                                                runIO <| comprehensiveTryWith 
                                                    JsonLetItBe StopJsonDownloading JsonTimeoutError 
                                                    JsonDownloadError JsonTlsHandshakeError token ex 
                                    }
                            )
                    with                                               
                    | ex 
                        -> 
                        async
                            {
                                checkCancel token
                                runIO (postToLog2 <| string ex.Message <| "#0008-KBLJson")  
                                return
                                    [
                                        runIO <| comprehensiveTryWith 
                                            JsonLetItBe StopJsonDownloading JsonTimeoutError 
                                            JsonDownloadError JsonTlsHandshakeError token ex
                                    ]
                            }

                    |> fun a -> Async.RunSynchronously(a, cancellationToken = token) 
 
                reportProgress (float l, float l) 
                counterAndProgressBar.Post Stop
                
                match result |> List.length = l with
                | true  ->
                        runIO (postToLog3 <| result <| "#6666-KBLJson")
                        result 
                        |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                        |> Option.defaultValue (Ok ())
                | false ->
                        runIO (postToLog2 <| "" <| "#0009-KBLJson")  
                        Error JsonLetItBe  //json souboru je dost ... :-) 
        )    