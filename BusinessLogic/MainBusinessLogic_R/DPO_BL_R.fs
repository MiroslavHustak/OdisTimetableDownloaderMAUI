namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Threading

//******************************************

open FsHttp
open FSharp.Data
open FsToolkit.ErrorHandling

//******************************************

open Helpers
open Helpers.Validation
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

open Api.Logging

open Settings.SettingsDPO
open Settings.SettingsGeneral

open Types.Types
open Types.ErrorTypes  
open Types.Haskell_IO_Monad_Simulation

module DPO_BL =
     
    let internal filterTimetables pathToDir =    

        IO (fun () 
                -> 
                let getLastThreeCharacters input =
                    match String.length input <= 3 with
                    | true  -> input 
                    | false -> input.Substring(input.Length - 3)

                let removeLastFourCharacters input =
                    match String.length input <= 4 with
                    | true  -> String.Empty
                    | false -> input.[..(input.Length - 5)]                    
    
                let urlList = 
                    [
                        pathDpoWebTimetablesBus      
                        pathDpoWebTimetablesTrBus
                        pathDpoWebTimetablesTram
                    ]
    
                urlList
                |> List.collect 
                    (fun url 
                        -> 
                        //failwith "testing FSharp.Data.HtmlDocument.Load url"  //chytat tady exn je extremne pracne, zachyti to try-with blok v DPO.fs 
                        let document = FSharp.Data.HtmlDocument.Load url                 
                            in                
                            document.Descendants "a"
                            |> Seq.choose 
                                (fun htmlNode
                                    ->
                                    htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                                    |> Option.bind
                                        (fun attr
                                            -> 
                                            option //moje paranoia na null nebo prazdne retezce
                                                {
                                                    let! nodes = htmlNode.InnerText () |> Option.ofNullEmpty
                                                    let nodes : string = nodes
                                                    let! attr = attr.Value () |> Option.ofNullEmpty
                                                    let attr : string = attr
                                                               
                                                    return (nodes, attr)
                                                }                                                          
                                        )            
                                )  
                            |> Seq.filter
                                (fun (_ , item2)
                                    ->
                                    item2.Contains @"/jr/" && item2.Contains ".pdf" && not (item2.Contains "AE-en") && not (item2.Contains "eng")
                                )
                            |> Seq.map 
                                (fun (_ , item2) 
                                    ->  
                                    let linkToPdf = sprintf"%s%s" pathDpoWeb item2  //https://www.dpo.cz // /jr/2023-04-01/024.pdf 
                                    //chybne odkazy jsou pozdeji tise eliminovany

                                    let linkToPdf = 
                                        isValidHttps linkToPdf
                                        |> Option.fromBool linkToPdf
                                        |> Option.defaultValue String.Empty

                                    let adaptedLineName =
                                        let s (item2 : string) = item2.Replace(@"/jr/", String.Empty).Replace(@"/", "?").Replace(".pdf", String.Empty) 
                                        
                                        let rec x s =                                                                            
                                            match (getLastThreeCharacters s).Contains("?") with
                                            | true  -> x (sprintf "%s%s" s "_")                                                                             
                                            | false -> s

                                        let xTail s =
                                            let rec loop s =
                                                match (getLastThreeCharacters s).Contains("?") with
                                                | true  -> loop (sprintf "%s%s" s "_")
                                                | false -> s
                                            loop s

                                        let rec xCPS s cont =
                                            match (getLastThreeCharacters s).Contains("?") with
                                            | true  -> xCPS (sprintf "%s%s" s "_") cont
                                            | false -> cont s 
                                        
                                        // (x << s) item2
                                        // xCPS (s item2) id                                        
                                        (xTail << s) item2 

                                    let lineName = 
                                        let s adaptedLineName = sprintf "%s_%s" (getLastThreeCharacters adaptedLineName) adaptedLineName  
                                        let s1 s = removeLastFourCharacters s 
                                        sprintf"%s%s" <| (s >> s1) adaptedLineName <| ".pdf"
                                                    
                                    let pathToFile = 
                                        let item2 = item2.Replace("?", String.Empty)                                            
                                        sprintf "%s/%s" pathToDir lineName

                                    linkToPdf, pathToFile
                                )
                            |> Seq.distinct
                            |> Seq.filter 
                                (fun (item1, item2)
                                    -> 
                                    not (String.IsNullOrWhiteSpace item1) && not (String.IsNullOrWhiteSpace item2)//just in case                                         
                                )  
                            |> Seq.toList                                
                    ) 
        )
    
    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) (filteredTimetables : IO<(string * string) list>) : IO<Result<unit, MHDErrors>> =
    
        IO (fun () 
                ->    
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let downloadWithResumeDPO (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, MHDErrors>> =
    
                    async
                        {
                            let maxRetries = maxRetries500
                            let initialBackoffMs = delayMs
    
                            let rec attempt retryCount (backoffMs : int) =

                                async
                                    {
                                        checkCancel token
    
                                        let existingFileLength =
                                            runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
                                            |> Option.map (fun _ -> FileInfo(pathToFile).Length)
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

                                            match response.statusCode with
                                            | HttpStatusCode.OK when existingFileLength > 0L 
                                                ->
                                                return Error FileDownloadErrorMHD // server ignored Range
                                            | HttpStatusCode.OK
                                            | HttpStatusCode.PartialContent 
                                                ->
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask

                                                    let fileMode =
                                                        match existingFileLength > 0L with
                                                        | true  -> FileMode.Append
                                                        | false -> FileMode.Create

                                                    use fs = new FileStream(pathToFile, fileMode, FileAccess.Write, FileShare.None)
                                                    do! stream.CopyToAsync(fs, token) |> Async.AwaitTask
                                                    do! stream.FlushAsync(token) |> Async.AwaitTask

                                                    return Ok ()
                                                with                                                                                  
                                                | ex 
                                                    -> 
                                                    checkCancel token
                                                    runIO (postToLog2 <| string ex.Message <| "#0001-DPOBL") //in order not to log cancellation
                                                    return
                                                        runIO <| comprehensiveTryWithMHD 
                                                            LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                            FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                            | _ ->
                                                runIO (postToLog2 (string response.statusCode) "#0002-DPOBL")
                                                return Error FileDownloadErrorMHD
    
                                        | Choice2Of2 ex 
                                            ->
                                            match runIO <| isCancellationGeneric LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD token ex with
                                            | err when err = StopDownloadingMHD 
                                                ->
                                                runIO (postToLog2 <| string ex.Message <| "#0003-DPOBL") //in order not to log cancellation
                                                return Error StopDownloadingMHD
                                            | _ when retryCount < maxRetries 
                                                ->
                                                do! Async.Sleep backoffMs
                                                return! attempt (retryCount + 1) (backoffMs * 2)
                                            | _ ->
                                                runIO (postToLog2 <| string ex.Message <| "#0004-DPOBL") 
                                                return 
                                                    runIO <| comprehensiveTryWithMHD 
                                                        LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                        FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                    }
    
                            return! attempt 0 initialBackoffMs
                        }
    
                let filteredTimetables =
                    runIO filteredTimetables
                    |> List.distinct
    
                match filteredTimetables with
                | [] 
                    ->
                    Ok ()
    
                | _ ->
                    try
                        let l = filteredTimetables.Length
    
                        let counterAndProgressBar =
                            MailboxProcessor<MsgIncrement>
                                .StartImmediate
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
                                                    | _ -> () 
                                                }
                                        loop 0
                                    )
                
                        let uri, pathToFile =
                            filteredTimetables 
                            |> List.unzip
                        
                        checkCancel token

                        let result = 
                            (token, uri, pathToFile)
                            |||> List.Parallel.map2_IO_AW_Token_Async                        
                                (fun uri path
                                    ->
                                    async
                                        {
                                            checkCancel token
                                            counterAndProgressBar.Post <| Inc 1
                                            return! downloadWithResumeDPO uri path token
                                        }
                                )

                            |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
                    
                        match result |> List.length = l with
                        | true  ->
                                reportProgress (float l, float l)
                                counterAndProgressBar.Post Stop

                                result 
                                |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                                |> Option.defaultValue (Ok ())
                        | false ->
                                reportProgress (float l, float l)
                                counterAndProgressBar.Post Stop
                                Error FileDownloadErrorMHD
                    with                                            
                    | ex 
                        -> 
                        checkCancel token //toto reaguje pro vypnutem internetu pred aktivaci downloadAndSaveTimetables
                        runIO (postToLog2 <| string ex.Message <| "#0006-DPOBL") //in order not to log cancellation
                        runIO <| comprehensiveTryWithMHD 
                            LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                            FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
        )