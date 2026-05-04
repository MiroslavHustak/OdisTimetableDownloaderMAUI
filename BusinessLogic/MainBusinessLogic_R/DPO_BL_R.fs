namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//******************************************

open FsHttp
open FSharp.Data
open FsToolkit.ErrorHandling

//******************************************

open Helpers
open Helpers.Validation
open Helpers.DirFileHelper
open Helpers.ProgressValues
open Helpers.ExceptionHelpers

open Api.Logging

open Settings.SettingsDPO
open Settings.SettingsGeneral

open Types.Types
open Types.ErrorTypes  
open Types.Haskell_IO_Monad_Simulation

module DPO_BL =
   
    let private resolveBaseUrl () =

        IO (fun () 
                ->   
                let candidates = [ pathDpoWeb3; pathDpoWeb1; pathDpoWeb4; pathDpoWeb2 ]
    
                let probeUrl (base' : string) =
                    try
                        use handler = new HttpClientHandler(AllowAutoRedirect = false)  //do not follow redirects
                        use client = new HttpClient(handler)
                        use response = client.GetAsync(sprintf "%s%s" base' pathDpoWebTimetablesBus).Result
                        response.IsSuccessStatusCode //pouze kdyz 200-299, redirekce se nebere v potaz diky AllowAutoRedirect = false                
                    with
                    | _ -> false
    
                candidates
                |> List.tryFind probeUrl
                |> Option.defaultValue String.Empty 
        )

    let private loadHtmlDocument pathDpoWeb =

        IO (fun () 
                -> 
                try
                    //resolveBaseUrl >> runIO >> urlList <| ()  
                    urlList pathDpoWeb
                    |> List.Parallel.map_IO_AW 
                        (fun url -> FSharp.Data.HtmlDocument.AsyncLoad url)
                    |> Ok
                with
                | ex
                    ->
                    runIO (postToLog2 <| string ex.Message <| "#6600-DPOBL") 
                    Error FileDownloadErrorMHD
        )

    let internal filterTimetables reportProgress pathToDir token =    

        IO (fun () 
                -> 
                let checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let getLastThreeCharacters input =
                    match String.length input <= 3 with
                    | true  -> input 
                    | false -> input.Substring(input.Length - 3)

                let removeLastFourCharacters input =
                    match String.length input <= 4 with
                    | true  -> String.Empty
                    | false -> input.[..(input.Length - 5)]    

                let pathDpoWeb = resolveBaseUrl >> runIO <| ()  

                let htmlDocList = 
                    loadHtmlDocument >> runIO <| pathDpoWeb
                
                match htmlDocList with
                | Error err
                    -> 
                    runIO (postToLog2 <| string err <| "#6601-DPOBL") 
                    Error err

                | Ok htmlDocList
                    ->
                    let counterAndProgressBar =
                        counterAndProgressBar (htmlDocList |> List.length) token checkCancel reportProgress

                    let result = 
                        htmlDocList
                        |> List.Parallel.map_CPU_AW_Token token
                            (fun document 
                                -> 
                                async
                                    {
                                        checkCancel token
                                
                                        //chytat tady exn je extremne pracne, zachyti to try-with blok v DPO.fs
                                        let! document = document

                                        counterAndProgressBar.Post <| Inc 1 

                                        return
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
                                                                    let! nodes = htmlNode.InnerText () |> Option.ofNullEmptySpace
                                                                    let nodes : string = nodes
                                                                    let! attr = attr.Value () |> Option.ofNullEmptySpace
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
                                                    let linkToPdf =   //https://dpo.cz // /jr/2023-04-01/024.pdf 
                                  
                                                        match Uri.IsWellFormedUriString(item2, UriKind.Absolute) with
                                                        | true  -> item2
                                                        | false -> sprintf "%s%s" pathDpoWeb item2

                                                        //zatim to ponech takto, nez DPO zvladne vyresit sve problemy
                                                        |> Option.ofNullEmptySpace
                                                        |> Option.bind isValidHttpsOption
                                                        |> Option.defaultValue String.Empty

                                                    let adaptedLineName =
                                                        let s (item2 : string) = 
                                                            item2
                                                                .Replace(@"/jr/", String.Empty)
                                                                .Replace(@"/", "?")
                                                                |> fun s -> s.Replace("AE", "_AE")
                                                                |> fun s -> System.Text.RegularExpressions.Regex.Replace(s, @"_\d{4}-\d{2}-\d{2}(?=\.pdf)", String.Empty)
                                                                |> fun s -> s.Replace(".pdf", String.Empty)
                                        
                                                        let xTail s =
                                                            let rec loop s =
                                                                match (getLastThreeCharacters s).Contains("?") with
                                                                | true  -> loop (sprintf "%s%s" s "_")
                                                                | false -> s
                                                            loop s
                                
                                                        (xTail << s) item2

                                                    let lineName =     
                                                        let s adaptedLineName = sprintf "%s_%s" (getLastThreeCharacters adaptedLineName) adaptedLineName  
                                                        let s1 s = removeLastFourCharacters s 
                                                        sprintf"%s%s" <| (s >> s1) adaptedLineName <| ".pdf"   
                                                    
                                                    let pathToFile = 
                                                        let _ = item2.Replace("?", String.Empty)                                            
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
                                    }
                                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
                            ) 

                    counterAndProgressBar.PostAndReply(fun reply -> StopAndReply reply) 
                    
                    result
                    |> List.concat
                    |> Ok
        )
    
    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) filteredTimetables : IO<Result<unit, MHDErrors>> =
    
        IO (fun () 
                ->    
                let checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let downloadWithResumeDPO (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, MHDErrors>> =
    
                    async
                        {
                            let maxRetries = maxRetries4
                            let initialBackoffMs = delayMs
    
                            let rec attempt retryCount (backoffMs : int) =

                                async
                                    {
                                        checkCancel token
    
                                        let existingFileLength =
                                            runIO <| checkFileCondition pathToFile _.Exists
                                            |> Option.map (fun _ -> FileInfo(pathToFile).Length)
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
                                                        runIO (postToLog2 <| string ex.Message <| "#0005-DPOBL")
                                                        return Error FileDownloadErrorMHD
                                                | false 
                                                    ->
                                                    runIO (postToLog2 "Max retries on ignored Range header" "#0006-DPOBL")
                                                    return Error FileDownloadErrorMHD
                                            | _, HttpStatusCode.OK
                                            | _, HttpStatusCode.PartialContent 
                                                ->
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask
                                                    let fileMode =
                                                        match existingFileLength > 0L with
                                                        | true  -> FileMode.Append
                                                        | false -> FileMode.Create
                                                    use fs = new FileStream(pathToFile, fileMode, FileAccess.Write, FileShare.None)
                                                    do! stream.CopyToAsync(fs, token) |> Async.AwaitTask
                                                    do! stream.FlushAsync token |> Async.AwaitTask

                                                    return Ok ()
                                                with                                                                                  
                                                | ex 
                                                    -> 
                                                    checkCancel token
                                                    runIO (postToLog2 <| string ex.Message <| "#0001-DPOBL") 
                                                    return
                                                        runIO <| comprehensiveTryWith 
                                                            LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                            FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                            | _, _ 
                                                ->
                                                //runIO (postToLog2 (string response.statusCode) "#0002-DPOBL")
                                                //runIO (postToLog2 uri "#0002-2-DPOBL")
                                                //tise ignorujeme vadne retezce na dpo.cz
                                                return Ok ()//Error FileDownloadErrorMHD
                                            
                                        | Choice2Of2 ex 
                                            ->
                                            match runIO <| isCancellationGeneric LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD token ex with
                                            | err when err = StopDownloadingMHD 
                                                ->
                                                runIO (postToLog2 <| string ex.Message <| "#0003-DPOBL") 
                                                return Error StopDownloadingMHD
                                            | _ when retryCount < maxRetries 
                                                ->
                                                do! Async.Sleep backoffMs
                                                return! attempt (retryCount + 1) (backoffMs * 2)
                                            | _ ->
                                                runIO (postToLog2 <| string ex.Message <| "#0004-DPOBL") 
                                                return 
                                                    runIO <| comprehensiveTryWith 
                                                        LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                        FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                    }
    
                            return! attempt 0 (umMiliSecondsToInt32 initialBackoffMs)
                        }
    
                let filteredTimetables =
                    filteredTimetables
                    |> List.distinct
    
                match filteredTimetables with
                | [] 
                    ->
                    Ok ()
    
                | _ ->
                    try
                        let l = filteredTimetables.Length
    
                        let counterAndProgressBar = counterAndProgressBar l token checkCancel reportProgress                         
                
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

                                runIO (postToLog3 <| result <| "#0006-DPOBL")

                                result 
                                |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                                |> Option.defaultValue (Ok ())
                        | false ->
                                reportProgress (float l, float l)
                                counterAndProgressBar.Post Stop
                                Error LetItBeMHD
                    with                                            
                    | ex 
                        -> 
                        checkCancel token //toto reaguje pro vypnutem internetu pred aktivaci downloadAndSaveTimetables
                        runIO (postToLog2 <| string ex.Message <| "#0007-DPOBL") 
                        runIO <| comprehensiveTryWith 
                            LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                            FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
        )