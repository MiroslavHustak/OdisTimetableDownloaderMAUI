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
open Helpers.FileInfoHelper

open Types.ErrorTypes  

open Settings.Messages
open Settings.SettingsDPO
open Settings.SettingsGeneral

open IO_Operations.IO_Operations

//HttpClient
module DPO_BL =

    //************************Submain functions************************************************************************
     
    let internal filterTimetables () pathToDir = 

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
                let document = FSharp.Data.HtmlDocument.Load url //neni nullable, nesu exn
                  
                document.Descendants "a"
                |> Seq.choose 
                    (fun htmlNode
                        ->
                        htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                        |> Option.bind
                            (fun attr
                                -> 
                                pyramidOfDoom
                                    {
                                        let! nodes = htmlNode.InnerText () |> Option.ofNullEmpty, None
                                        let! attr = attr.Value () |> Option.ofNullEmpty, None
                                                               
                                        return Some (nodes, attr)
                                    }                                                          
                            )            
                    )  
                |> Seq.filter
                    (fun (_ , item2)
                        ->
                        item2.Contains @"/jr/" && item2.Contains ".pdf" && not (item2.Contains "AE-en.pdf") 
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
                            (x << s) item2
                                        
                        let lineName = 
                            let s adaptedLineName = sprintf "%s_%s" (getLastThreeCharacters adaptedLineName) adaptedLineName  
                            let s1 s = removeLastFourCharacters s 
                            sprintf"%s%s" <| (s >> s1) adaptedLineName <| ".pdf"
                                            
                        let pathToFile = 
                            let item2 = item2.Replace("?", String.Empty)                                            
                            sprintf "%s/%s" pathToDir lineName

                        linkToPdf, pathToFile
                    )
                |> Seq.toList
                |> List.distinct
            ) 

    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) (filterTimetables : (string * string) list) =  

        let downloadFileTaskAsync (uri : string) (pathToFile : string) : Async<Result<unit, MHDErrors>> =  
       
            async
                {                      
                    try   
                        ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13  ////quli Android 7.1

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
                            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)")

                            let existingFileLength =                               
                                checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                |> function
                                    | Some _ -> (FileInfo pathToFile).Length
                                    | None   -> 0L
                                                    
                            let headerContent1 = "Range" 
                            let headerContent2 = sprintf "bytes=%d-" existingFileLength   

                            match existingFileLength > 0L with
                            | true  -> client.DefaultRequestHeaders.Add(headerContent1, headerContent2)
                            | false -> ()
                                                        
                            use! response = client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead) |> Async.AwaitTask
                            //use! response = client.GetAsync uri |> Async.AwaitTask

                            match response.IsSuccessStatusCode with
                            | true  ->
                                    let pathToFile = pathToFile.Replace("?", String.Empty)

                                    //use fileStream = new FileStream(pathToFile, FileMode.CreateNew) 
                                    
                                    use fileStream =
                                        match existingFileLength > 0L with 
                                        | true  -> new FileStream(pathToFile, FileMode.Append) 
                                        | false -> new FileStream(pathToFile, FileMode.CreateNew)

                                    let! stream = response.Content.ReadAsStreamAsync () |> Async.AwaitTask
                                    do! stream.CopyToAsync fileStream |> Async.AwaitTask
                                    
                                    return Ok ()

                            | false ->
                                    let errorType =
                                        match response.StatusCode with
                                        | HttpStatusCode.BadRequest          -> Error BadRequest
                                        | HttpStatusCode.InternalServerError -> Error InternalServerError
                                        | HttpStatusCode.NotImplemented      -> Error NotImplemented
                                        | HttpStatusCode.ServiceUnavailable  -> Error ServiceUnavailable
                                        | HttpStatusCode.NotFound            -> Error NotFound
                                        | _                                  -> Error CofeeMakerUnavailable 

                                    return errorType       
                           
                        | Error _ 
                            -> 
                            //TODO logfile
                            return Error ConnectionError   
                           
                    with                                                         
                    | _
                        ->
                        //TODO logfile
                        return Error FileDownloadErrorMHD 
                } 
    
        let downloadTimetables reportProgress (token : CancellationToken) : Result<unit, MHDErrors> = 
            
            try
                let l = filterTimetables |> List.length
        
                filterTimetables 
                |> List.mapi
                    (fun i (link, pathToFile)
                        ->  
                        async                                                
                            {   
                                token.ThrowIfCancellationRequested ()
                                reportProgress (float i + 1.0, float l)  
                                return! downloadFileTaskAsync link pathToFile 
                            } 
                        |> Async.Catch
                        |> Async.RunSynchronously  
                        |> Result.ofChoice  
                    ) 
                |> List.tryPick
                    (function
                        | Ok _ 
                            -> 
                            None

                        | Error err
                            ->
                            match (string err.Message).Contains("The operation was canceled.") with
                            | true  -> Some <| Error StopDownloadingMHD
                            | false -> Some <| Error FileDownloadErrorMHD
                    )
                |> Option.defaultValue (Ok ()) 
               
            with
            | _      //TODO logfile
                ->
                let dirName = ODISDefault.OdisDir5                       
                    in
                    match deleteOneODISDirectoryMHD dirName dpoPathTemp with
                    | Ok _    -> Error FileDownloadErrorMHD
                    | Error _ -> Error FileDeleteErrorMHD                 

        downloadTimetables reportProgress token    