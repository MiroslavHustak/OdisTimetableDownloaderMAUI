namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks

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
    
                                        return Some (HtmlDocument.Parse html)
                                    with
                                    | _ -> return None
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
                                        -> return document
                                    | None
                                        -> return FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz     
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
                                    isValidHttps linkToPdf
                                    |> Option.fromBool linkToPdf
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
                let downloadWithResumeMDPO (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, MHDErrors>> =
                        
                    async
                        {
                            let maxRetries = 5
                            let initialBackoffMs = 1000

                            let rec attempt retryCount (backoffMs : int) =

                                async
                                    {
                                        token.ThrowIfCancellationRequested()
                        
                                        let existingLength =
                                            runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
                                            |> Option.map (fun _ -> (FileInfo pathToFile).Length)
                                            |> Option.defaultValue 0L
                        
                                        let request =
                                            match existingLength > 0L with
                                            | true  ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds timeOutInSeconds2
                                                            config_cancellationToken token
                                                            header "User-Agent" "FsHttp/Android"
                                                            header "Range" (sprintf "bytes=%d-" existingLength)
                                                        }
                                            | false ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds timeOutInSeconds2
                                                            config_cancellationToken token
                                                            header "User-Agent" "FsHttp/Android"
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
                                                    use! stream =
                                                        response.content.ReadAsStreamAsync()
                                                        |> Async.AwaitTask
                        
                                                    use fileStream =
                                                        new FileStream(
                                                            pathToFile,
                                                            FileMode.Append,
                                                            FileAccess.Write,
                                                            FileShare.None
                                                        )
                        
                                                    do! stream.CopyToAsync(fileStream, token)
                                                        |> Async.AwaitTask
                        
                                                    return Ok ()
                                                with 
                                                | :? HttpRequestException as ex 
                                                    -> return comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex                                 
                                                | ex 
                                                    -> return comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                        
                                            | HttpStatusCode.Forbidden
                                                ->
                                                runIO (postToLog () (sprintf "%s Forbidden 403 #MDPO-403" uri))
                                                return Error FileDownloadErrorMHD
                        
                                            | _ ->
                                                runIO (postToLog (string response.statusCode) "#MDPO-STATUS")
                                                return Error FileDownloadErrorMHD
                        
                                        | Choice2Of2 ex 
                                            ->
                                            match isCancellationGeneric StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD token ex with
                                            | err when err = StopDownloadingMHD 
                                                ->
                                                runIO (postToLog <| ex.Message <| "#MDPO-CANCEL")
                                                return Error StopDownloadingMHD
                                            | _ ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO (postToLog <| ex.Message <| "#MDPO-RETRY")
                                                        return comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                    }
                        
                            return! attempt 0 initialBackoffMs
                        }
        
                let downloadAndSave reportProgress (token : CancellationToken) filterTimetables =
                        
                    IO (fun () 
                            ->    
                            let filterTimetables = runIO filterTimetables
                                
                            let l = filterTimetables |> Map.count
                        
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
                                                        | ex -> runIO (postToLog <| ex.Message <| "#900MDPO-MP")
                                                    }
                                            loop 0
                                        )
                        
                            try
                                let uri, pathToFile =
                                    filterTimetables 
                                    |> Map.toList
                                    |> List.unzip
                    
                                (token, uri, pathToFile)
                                |||> List.Parallel.map2_IO_AW_Token_Async
                                    (fun uri pathToFile 
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
                                                        match! downloadWithResumeMDPO uri pathToFile token with
                                                        | Ok _ 
                                                            ->
                                                            return Ok ()
                    
                                                        | Error err
                                                            ->
                                                            match err with
                                                            | err when err = StopDownloadingMHD 
                                                                ->
                                                                //runIO (postToLog <| string err <| "#123456G-MDPO")
                                                                return Error StopDownloadingMHD
                                                            | err
                                                                ->
                                                                runIO (postToLog <| string err <| "#7028-MDPO")
                                                                return Error err
                        
                                                with 
                                                | :? HttpRequestException as ex 
                                                    -> return comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex                                 
                                                | ex 
                                                    -> return comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex
                                            }                  
                                    )
                    
                                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
                            
                            with                                                    
                            | :? HttpRequestException as ex 
                                -> [ comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex ]
                            | ex
                                -> [ comprehensiveTryWith LetItBeMHD StopDownloadingMHD TimeoutErrorMHD FileDownloadErrorMHD TlsHandshakeErrorMHD token ex ]                         
                    )
                    
                runIO <| downloadAndSave reportProgress token filterTimetables
                |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                |> Option.defaultValue (Ok ())
        )