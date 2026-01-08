namespace BusinessLogic

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//******************************************

open FSharp.Data
open FsToolkit.ErrorHandling

//******************************************

open Helpers
open Helpers.Builders
open Helpers.Validation
open Helpers.DirFileHelper

open Api.Logging

open Types.Types
open Types.ErrorTypes  
open Types.Grid3Algebra

open Settings.SettingsDPO
open Settings.SettingsGeneral

open IO_Operations.IO_Operations
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

    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) filterTimetables =  
        
        IO (fun () 
                -> 
                let downloadFileTaskAsync (uri : string) (pathToFile : string) =  //client.GetAsync

                    IO (fun () 
                            -> 
                            async //API of HttpClient is async based
                                {                      
                                    try         
                                        // not <| File.Exists pathToFile TOCTOU race -> try-with will catch
                                                                               
                                        use client = new HttpClient()

                                        client.Timeout <- TimeSpan.FromSeconds 30.0
                                        client.DefaultRequestHeaders.UserAgent.ParseAdd "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"

                                        let existingFileLength =     
                                            // TOCTOU race problem is negligible here as the value is only for the Windows Machine mode / resuming downloads
                                            // Resuming downloading does not work under Android OS
                                            runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                            |> function
                                                | Some _ -> (FileInfo pathToFile).Length
                                                | None   -> 0L
                                                    
                                        let headerContent1 = "Range" 
                                        let headerContent2 = sprintf "bytes=%d-" existingFileLength   

                                        match existingFileLength > 0L with
                                        | true  -> client.DefaultRequestHeaders.Add(headerContent1, headerContent2)
                                        | false -> ()
                             
                                        use! response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token) |> Async.AwaitTask 
                          
                                        match response.IsSuccessStatusCode with
                                        | true  ->
                                                let pathToFile = pathToFile.Replace("?", String.Empty)

                                                //use fileStream = new FileStream(pathToFile, FileMode.CreateNew) 
                                    
                                                use fileStream =
                                                    match existingFileLength > 0L with 
                                                    | true  -> new FileStream(pathToFile, FileMode.Append) 
                                                    | false -> new FileStream(pathToFile, FileMode.CreateNew)

                                                use! stream = response.Content.ReadAsStreamAsync () |> Async.AwaitTask
                                                do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                    
                                                return Ok ()

                                        | false ->     
                                                return 
                                                    match response.StatusCode with
                                                    | HttpStatusCode.BadRequest          -> Error BadRequest
                                                    | HttpStatusCode.InternalServerError -> Error InternalServerError
                                                    | HttpStatusCode.NotImplemented      -> Error NotImplemented
                                                    | HttpStatusCode.ServiceUnavailable  -> Error ServiceUnavailable
                                                    | HttpStatusCode.NotFound            -> Error NotFound
                                                    | _                                  -> Error CofeeMakerUnavailable                            
                           
                                    with                                                         
                                    | ex
                                        ->
                                        runIO (postToLog <| string ex.Message <| "#035")
                                        return Error FileDownloadErrorMHD 
                                } 
                    )
    
                let downloadTimetables reportProgress (token : CancellationToken) : IO<Result<unit, MHDErrors>> = 

                    IO (fun () 
                            -> 
                            let filterTimetables = runIO <| filterTimetables

                            let l = filterTimetables |> List.length
                                in
                                let counterAndProgressBar =
                                    MailboxProcessor<MsgIncrement>
                                        .StartImmediate
                                            <|
                                            fun inbox 
                                                ->
                                                //use _ = token.Register (fun () -> inbox.Post (Unchecked.defaultof<MsgIncrement>))

                                                let rec loop n = 
                                                    async
                                                        {
                                                            try
                                                                let! Inc i = inbox.Receive()
                                                                reportProgress (float n, float l)
                                                                return! loop (n + i)
                                                            with
                                                            | ex -> runIO (postToLog <| string ex.Message <| "#900DPO-MP")
                                                        }
                                                loop 0      
            
                            try                               
                                filterTimetables 
                                |> List.Parallel.map_IO
                                    (fun (link, pathToFile)
                                        -> 
                                        async //API of HttpClient is async based
                                            {
                                                counterAndProgressBar.Post <| Inc 1
                                                token.ThrowIfCancellationRequested ()
                                                return! runIO <| downloadFileTaskAsync link pathToFile 
                                            }
                                        |> Async.Catch
                                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
                                        |> Result.ofChoice
                                    ) 
                                 |> List.tryPick
                                    (Result.either
                                        (fun _
                                            ->
                                            None
                                        )
                                        (fun ex 
                                            ->                                             
                                            match Helpers.ExceptionHelpers.isCancellation token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#123456YY")
                                                Some (Error <| StopDownloadingMHD)
                                            | _ 
                                                ->
                                                runIO (postToLog <| string ex.Message <| "#037-10")
                                                Some (Error <| FileDownloadErrorMHD)   
                                        )
                                    )                                
                                |> Option.defaultValue (Ok ()) 
               
                            with                            
                            | ex                             
                                -> 
                                match Helpers.ExceptionHelpers.isCancellation token ex with
                                | err 
                                    when err = StopDownloading
                                    ->
                                    runIO (postToLog <| string ex.Message <| "#123456Y")
                                    Error <| StopDownloadingMHD
                                | _ 
                                    ->
                                    runIO (postToLog <| string ex.Message <| "#037")
                                    Error <| FileDownloadErrorMHD   
                    )

                runIO <| downloadTimetables reportProgress token   
        )