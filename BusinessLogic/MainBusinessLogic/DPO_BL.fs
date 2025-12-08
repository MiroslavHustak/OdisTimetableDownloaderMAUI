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
                                            option
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
                                        in   
                                        let lineName = 
                                            let s adaptedLineName = sprintf "%s_%s" (getLastThreeCharacters adaptedLineName) adaptedLineName  
                                            let s1 s = removeLastFourCharacters s 
                                            sprintf"%s%s" <| (s >> s1) adaptedLineName <| ".pdf"
                                            in                
                                            let pathToFile = 
                                                let item2 = item2.Replace("?", String.Empty)                                            
                                                sprintf "%s/%s" pathToDir lineName

                                    linkToPdf, pathToFile
                                )
                            |> Seq.toList
                            |> List.distinct
                    ) 
        )

    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) filterTimetables =  
        
        IO (fun () 
                -> 
                let downloadFileTaskAsync (uri : string) (pathToFile : string) =  

                    IO (fun () 
                            -> 
                            async
                                {                      
                                    try   
                                        let client = 
                            
                                            pyramidOfDoom
                                                {
                                                    let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty
                                                    let! client = new HttpClient() |> Option.ofNull, Error String.Empty

                                                    client.Timeout <- TimeSpan.FromSeconds <| float 300 //timeoutInSeconds

                                                    return Ok client        
                                                }
                        
                                        match client with  
                                        | Ok client
                                            ->  
                                            use client = client

                                            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)")

                                            let existingFileLength =                               
                                                runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                                |> function
                                                    | Some _ -> (FileInfo pathToFile).Length
                                                    | None   -> 0L
                                                    
                                            let headerContent1 = "Range" 
                                            let headerContent2 = sprintf "bytes=%d-" existingFileLength   

                                            match existingFileLength > 0L with
                                            | true  -> client.DefaultRequestHeaders.Add(headerContent1, headerContent2)
                                            | false -> ()
                             
                                            //Async varianta musi byt quli cancellation token
                                            use! response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token) |> Async.AwaitTask 
                          
                                            match response.IsSuccessStatusCode with
                                            | true  ->
                                                    let pathToFile = pathToFile.Replace("?", String.Empty)

                                                    //use fileStream = new FileStream(pathToFile, FileMode.CreateNew) 
                                    
                                                    use fileStream =
                                                        match existingFileLength > 0L with 
                                                        | true  -> new FileStream(pathToFile, FileMode.Append) 
                                                        | false -> new FileStream(pathToFile, FileMode.CreateNew)

                                                    //Async varianta musi byt quli cancellation token
                                                    let! stream = response.Content.ReadAsStreamAsync () |> Async.AwaitTask
                                                    do! stream.CopyToAsync fileStream |> Async.AwaitTask                                    
                                    
                                                    return Ok ()

                                            | false ->     
                                                    use client = client

                                                    return 
                                                        match response.StatusCode with
                                                        | HttpStatusCode.BadRequest          -> Error BadRequest
                                                        | HttpStatusCode.InternalServerError -> Error InternalServerError
                                                        | HttpStatusCode.NotImplemented      -> Error NotImplemented
                                                        | HttpStatusCode.ServiceUnavailable  -> Error ServiceUnavailable
                                                        | HttpStatusCode.NotFound            -> Error NotFound
                                                        | _                                  -> Error CofeeMakerUnavailable 
                           
                                        | Error err 
                                            -> 
                                            runIO (postToLog <| err <| "#034")
                                            return Error ConnectionError   
                           
                                    with                                                         
                                    | ex
                                        ->
                                        runIO (postToLog <| ex.Message <| "#035")
                                        return Error FileDownloadErrorMHD 
                                } 
                    )
    
                let downloadTimetables reportProgress (token : CancellationToken) = 

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
                                                let rec loop n = 
                                                    async { match! inbox.Receive() with Inc i -> reportProgress (float n, float l); return! loop (n + i) }
                                                loop 0
            
                            try
                                // Array.Parallel.map doesn’t propagate CancellationToken — there’s no built-in mechanism in Array.Parallel.map to cancel the in-flight computations.
                                // Therefore async with Async.RunSynchronously shall be used
                                // But: Array.Parallel.map with Async.RunSynchronously blocks thread pool threads    
                                // Array.Parallel.map is designed for CPU-bound work anyway
                                filterTimetables 
                                |> List.Parallel.map_IO
                                    (fun (link, pathToFile)
                                        -> 
                                        async
                                            {
                                                counterAndProgressBar.Post <| Inc 1
                                                token.ThrowIfCancellationRequested ()
                                                return! runIO <| downloadFileTaskAsync link pathToFile 
                                            }
                                        |> Async.Catch
                                        |> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token)
                                        |> Result.ofChoice
                                    )  
                                |> List.tryPick
                                    (function
                                        | Ok _ 
                                            -> 
                                            None

                                        | Error ex
                                            when (string ex.Message).Contains "The operation was canceled" 
                                            ->
                                            Some <| Error StopDownloadingMHD

                                        | Error ex 
                                            ->
                                            runIO (postToLog <| ex.Message <| "#036")
                                            Some <| Error FileDownloadErrorMHD
                                    )
                                |> Option.defaultValue (Ok ()) 
               
                            with
                            | ex      
                                ->
                                pyramidOfInferno
                                    {                                                                                
                                        let! _ =
                                            (not <| (string ex.Message).Contains "The operation was canceled") |> Result.fromBool () String.Empty,
                                                fun _ -> Ok ()
                                        
                                        runIO (postToLog <| ex.Message <| "#037")
                                        
                                        let!_ = 
                                            runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I2) mdpoPathTemp, 
                                                (fun _
                                                    ->
                                                    runIO (postToLog <| FileDeleteErrorMHD <| "#372")                             
                                                    Error FileDeleteErrorMHD
                                                )

                                        runIO (postToLog <| FileDownloadErrorMHD <| "#371") 

                                        return Error FileDownloadErrorMHD                                            
                                    }                              
                    )

                runIO <| downloadTimetables reportProgress token   
        )