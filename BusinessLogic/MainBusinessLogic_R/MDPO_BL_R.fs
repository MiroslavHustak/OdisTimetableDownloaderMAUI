namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Threading

//**********************************

open FsHttp
open FSharp.Data
open FsToolkit.ErrorHandling

//**********************************

open Helpers
open Helpers.Validation
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

open Api.Logging

open Settings.SettingsMDPO
open Settings.SettingsGeneral

open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

module MDPO_BL = //FsHttp

    let internal filterTimetables pathToDir token =
    
        IO (fun () 
                ->
                let fetchHtmlWithFsHttp (url : string) =
                    IO (fun ()
                            ->
                            async
                                {
                                    try
                                        use! response =
                                            http
                                                {
                                                    GET url
                                                    config_cancellationToken token
                                                }
                                            |> Request.sendAsync
    
                                        let! html =
                                            Response.toStringAsync (Some 100000) response
    
                                        return Some << HtmlDocument.Parse <| html  
                                    with
                                    | ex 
                                        ->
                                        runIO (postToLog2 <| string ex.Message <| "#0001-MDPOBL")  
                                        return None
                                }
                        )
    
                let urlList = //aby to bylo jednotne s DPO
                    [
                        pathMdpoWebTimetables
                    ]

                urlList    
                |> Seq.collect 
                    (fun url 
                        -> 
                        let document =    
                            async
                                {
                                    let! documentOption = fetchHtmlWithFsHttp >> runIO <| url
                        
                                    match documentOption with
                                    | Some document
                                        -> 
                                        return document
                                    | None
                                        ->
                                        runIO (postToLog2 <| "HtmlDocument Error" <| "#0002-MDPOBL") 
                                        return FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz     
                                }
                            |> (fun a -> Async.RunSynchronously(a, cancellationToken = token))
    
                        //HtmlDocument -> web scraping -> extracting data from HTML pages
                                                                                
                        document.Descendants "a"                  
                        |> Seq.choose 
                            (fun htmlNode   
                                ->
                                htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                                |> Option.bind
                                    (fun attr 
                                        -> 
                                        option  //moje paranoia na null nebo prazdne retezce
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
                                item2.Contains @"/qr/" && item2.Contains ".pdf"
                            )
                        |> Seq.map 
                            (fun (_ , item2) 
                                ->                                                                 
                                let linkToPdf = sprintf "%s%s" pathMdpoWeb item2  //https://www.mdpo.cz // /qr/201.pdf
                                //chybne odkazy jsou pozdeji tise eliminovany
                                let linkToPdf = 
                                    isValidHttpsOption linkToPdf
                                    |> Option.defaultValue String.Empty

                                let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                                let pathToFile lineName = sprintf "%s/%s" pathToDir lineName

                                linkToPdf, pathToFile << lineName <| item2
                            )                          
                        |> Seq.distinct   
                        |> Seq.filter 
                            (fun (item1, item2)
                                -> 
                                not (String.IsNullOrWhiteSpace item1) && not (String.IsNullOrWhiteSpace item2) //just in case                                         
                            )             
                    )  
                |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map
        )    

    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) filterTimetables = 
        
        IO (fun () 
                ->     
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let filterTimetables = runIO filterTimetables                
                let l = filterTimetables |> Map.count

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
                                        | _ -> () 
                                    }
                            loop 0
                        )

                let downloadWithResumeMDPO (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, MHDErrors>> =
                        
                    async
                        {
                            let maxRetries = maxRetries4
                            let initialBackoffMs = delayMs

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
                                                            header "User-Agent" "FsHttp/Android"
                                                            header "Range" (sprintf "bytes=%d-" existingFileLength)
                                                        }
                                            | false ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds (umSecondsToFloat timeOutInSeconds2)
                                                            config_cancellationToken token
                                                            header "User-Agent" "FsHttp/Android"
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
                                                        runIO (postToLog2 <| string ex.Message <| "#0007-MDPOBL")
                                                        return Error FileDownloadErrorMHD
                                                | false 
                                                    ->
                                                    runIO (postToLog2 "Max retries on ignored Range header" "#0008-MDPOBL")
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
                                                    use fileStream = new FileStream(pathToFile, fileMode, FileAccess.Write, FileShare.None)                        
                                                    do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                    do! fileStream.FlushAsync(token) |> Async.AwaitTask  
                                                    
                                                    return Ok ()
                                                with                                                 
                                                | ex 
                                                    -> 
                                                    checkCancel token
                                                    //runIO (postToLog2 <| string ex.Message <| "#0003-MDPOBL")  
                                                    return 
                                                        runIO <| comprehensiveTryWith 
                                                            LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                            FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                                                
                                            | _, HttpStatusCode.Forbidden
                                                ->
                                                runIO (postToLog2 () (sprintf "%s Forbidden 403 #0004-MDPOBL" uri))
                                                return Error FileDownloadErrorMHD
                                                                
                                            | _, _ 
                                                ->
                                                runIO (postToLog2 (string response.statusCode) "#0005-MDPOBL")
                                                return Error FileDownloadErrorMHD
                                                                
                                        | Choice2Of2 ex 
                                            ->
                                            match runIO <| isCancellationGeneric LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD token ex with
                                            | err when err = StopDownloadingMHD 
                                                ->
                                                runIO (postToLog2 <| string ex.Message <| "#0006-MDPOBL") 
                                                return Error StopDownloadingMHD
                                            | _ ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO (postToLog2 <| string ex.Message <| "#0009-MDPOBL") 
                                                        return 
                                                            runIO <| comprehensiveTryWith 
                                                                LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                                FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                    }
                        
                            return! attempt 0 (umMiliSecondsToInt32 initialBackoffMs)
                        }
        
                let downloadAndSave (token : CancellationToken) =
                        
                    IO (fun () 
                            ->                           
                            try
                                let uri, pathToFile =
                                    filterTimetables 
                                    |> Map.toList
                                    |> List.unzip
                                
                                checkCancel token 

                                (token, uri, pathToFile)
                                |||> List.Parallel.map2_IO_AW_Token_Async
                                    (fun uri pathToFile 
                                        ->
                                        async
                                            {
                                                try
                                                    checkCancel token 

                                                    let pathExists =
                                                        runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
                        
                                                    match pathExists with
                                                    | Some _ 
                                                        ->
                                                        return Ok ()
                    
                                                    | None 
                                                        ->
                                                        match! downloadWithResumeMDPO uri pathToFile token with
                                                        | Ok _ 
                                                            ->
                                                            checkCancel token 
                                                            counterAndProgressBar.Post <| Inc 1
                                                            return Ok ()
                    
                                                        | Error err
                                                            ->
                                                            checkCancel token

                                                            match err with
                                                            | err when err = StopDownloadingMHD 
                                                                ->
                                                                //runIO (postToLog2 <| string err <| "#0007-MDPOBL")                                                                 
                                                                return Error StopDownloadingMHD
                                                            | err
                                                                ->
                                                                runIO (postToLog2 <| string err <| "#0008-MDPOBL")                                                               
                                                                return Error err
                                                with           
                                                | ex 
                                                    ->
                                                    checkCancel token
                                                    //runIO (postToLog2 <| string ex.Message <| "#0009-MDPOBL") 
                                                    return
                                                        runIO <| comprehensiveTryWith 
                                                            LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                                            FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                            }                  
                                    )                    
                                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)

                            with 
                            | ex
                                ->
                                checkCancel token 
                                //runIO (postToLog2 <| string ex.Message <| "#0010-MDPOBL") 
                                [ 
                                    runIO <| comprehensiveTryWith 
                                        LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                        FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                ]                         
                    )
                    
                let result = runIO <| downloadAndSave token                
                
                match result |> List.length = l with
                | true  ->
                        reportProgress (float l, float l)
                        counterAndProgressBar.Post Stop

                        runIO (postToLog3 <| result <| "#0011-MDPOBL")

                        result 
                        |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                        |> Option.defaultValue (Ok ())
                | false ->
                        reportProgress (float l, float l)
                        counterAndProgressBar.Post Stop
                        Error LetItBeMHD
        )