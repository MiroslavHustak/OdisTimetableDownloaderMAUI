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

//HttpClient
module DPO_BL =

    //************************Submain functions************************************************************************
     
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
                                            runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
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
                                                    match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                                    | err when err = StopDownloading 
                                                        ->
                                                        runIO (postToLog <| string ex.Message <| "#123456J-DPO")
                                                        return Error StopDownloading
                                                    | err 
                                                        ->
                                                        runIO (postToLog <| string ex.Message <| "#3352-DPO")
                                                        return Error err
    
                                            | HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog () (sprintf "%s Forbidden 403 #2211-DPO" uri)
                                                return Error FileDownloadError
    
                                            | status 
                                                ->
                                                runIO <| postToLog (string status) "#2212-DPO"
                                                return Error FileDownloadError
    
                                        | Choice2Of2 ex 
                                            ->
                                            match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                            | err when err = StopDownloading
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#123456H-DPO")
                                                return Error StopDownloading
                                            | err
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog (string ex.Message) (sprintf "#7024-DPO (retry %d)" retryCount)
                                                        return Error err
                                    }
    
                            return! attempt 0 initialBackoffMs
                        }
    
                // -------------------------------------------
                // Download all timetables / PDFs in context
                // -------------------------------------------
                let downloadAndSaveTimetables (token : CancellationToken) context =
    
                    let l = context.list |> List.length

                    let counterAndProgressBar =
                        MailboxProcessor<MsgIncrement>
                            .StartImmediate
                                <| fun inbox
                                    ->
                                    let rec loop n =
                                        async
                                            {
                                                try
                                                    let! Inc i = inbox.Receive()
                                                    context.reportProgress (float n, float l)
                                                    return! loop (n + i)
                                                with
                                                | ex -> runIO (postToLog <| ex.Message <| "#903-MP-DPO")
                                            }
                                    loop 0
    
                    let removeDuplicatePathPairs uri pathToFile =
                        (uri, pathToFile) ||> List.zip
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
    
                                            let pathExists =
                                                runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
    
                                            match pathExists with
                                            | Some _ 
                                                ->
                                                return Ok ()

                                            | None 
                                                ->
                                                let! result = downloadWithResume uri pathToFile token
                                                match result with
                                                | Ok _ 
                                                    ->
                                                    return Ok ()

                                                | Error err
                                                    ->
                                                    match err with
                                                    | err when err = StopDownloading 
                                                        ->
                                                        runIO (postToLog <| string err <| "#123456G-DPO")
                                                        return Error <| PdfError StopDownloading
                                                    | err
                                                        ->
                                                        runIO (postToLog <| string err <| "#7028-DPO")
                                                        return Error <| PdfError err
    
                                        with 
                                        | ex
                                            ->
                                            match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                            | err when err = StopDownloading 
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#123456F-DPO")
                                                return Error <| PdfError StopDownloading
                                            | err
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#024-DPO")
                                                return Error <| PdfError err
                                    }
                            )

                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
    
                    with
                    | ex ->
                        match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                        | err when err = StopDownloading 
                            ->
                            runIO (postToLog (string ex.Message) "#123456E-DPO")
                            [ Error (PdfError StopDownloading) ]
                        | err 
                            ->
                            runIO (postToLog (ex.Message) "#024-6-DPO")
                            [ Error (PdfError err) ]
    
                // -------------------------------------------
                // Main logic
                // -------------------------------------------
                match context.dir |> Directory.Exists with
                | false ->
                        runIO (postToLog NoFolderError "#251-DPO")
                        Error (PdfError NoFolderError)
                | true  ->
                        match context.list with
                        | []
                            ->
                            Ok ()
                        | _ 
                            ->
                            downloadAndSaveTimetables token context 
                            |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                            |> Option.defaultValue (Ok ())
        )
    