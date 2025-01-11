namespace BusinessLogic

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//**********************************

open FsHttp
open FSharp.Data
open FsToolkit.ErrorHandling

//**********************************

open Helpers
open Helpers.Builders
open Helpers.FileInfoHelper

open Types.ErrorTypes

open Settings.Messages
open Settings.SettingsMDPO
open Settings.SettingsGeneral    

open IO_Operations.IO_Operations

module MDPO_BL = //FsHttp

    //************************Submain functions************************************************************************

    let internal filterTimetables () pathToDir = 
         
        let urlList = //aby to bylo jednotne s DPO
            [
                pathMdpoWebTimetables
            ]

        urlList    
        |> Seq.collect 
            (fun url 
                -> 
                let document = FSharp.Data.HtmlDocument.Load url //neni nullable, nesu exn
                //HtmlDocument -> web scraping -> extracting data from HTML pages
                                                                                    
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
                        item2.Contains @"/qr/" && item2.Contains ".pdf"
                    )
                |> Seq.map 
                    (fun (_ , item2) 
                        ->                                                                 
                        let linkToPdf = sprintf "%s%s" pathMdpoWeb item2  //https://www.mdpo.cz // /qr/201.pdf
                        let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                        let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                        linkToPdf, (pathToFile << lineName) item2
                    )                          
                |> Seq.distinct                 
            )  
        |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map

    //FsHttp
    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) (filterTimetables : Map<string, string>) =  

        let downloadFileTaskAsync (token : CancellationToken) (uri : string) (pathToFile : string) : Async<Result<unit, MHDErrors>> =  
       
            async
                {                      
                    try    
                        let response = 
                            
                            pyramidOfDoom
                                {
                                    // enforcing TLS 1.2 and 1.3.
                                    ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13

                                    let existingFileLength =                               
                                        checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                        |> function
                                            | Some _ -> (FileInfo pathToFile).Length
                                            | None   -> 0L
                                                    
                                    let get uri = 

                                        let headerContent1 = "Range" 
                                        let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                          
                                        //config_timeoutInSeconds 300 -> 300 vterin, aby to nekolidovalo s odpocitavadlem (max 60 vterin) v XElmish 
                                        match existingFileLength > 0L with
                                        | true  -> 
                                                http
                                                    {
                                                        GET uri        
                                                        #if WINDOWS
                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        config_cancellationToken CancellationToken.None //token
                                                        header headerContent1 headerContent2
                                                        #endif
                                                    }
                                        | false ->
                                                http
                                                    {
                                                        GET uri
                                                        #if WINDOWS
                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        config_cancellationToken CancellationToken.None //token
                                                        #endif
                                                    }
                                                    
                                    let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty
                                    let! response = get >> Request.sendAsync <| uri |> Option.ofNull, Error String.Empty //Option.ofNull tady neni treba, ale aby to bylo jednotne....

                                    return Ok response        
                                }
                        
                        match response with
                        | Ok response
                            ->      
                            use! response = response
                        
                            match response.statusCode with        //TODO logfile
                            | HttpStatusCode.OK                  ->                                                                   
                                                                 do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                                 return Ok () 
                            | HttpStatusCode.BadRequest          ->                                                                       
                                                                 return Error BadRequest
                            | HttpStatusCode.InternalServerError -> 
                                                                 return Error InternalServerError
                            | HttpStatusCode.NotImplemented      ->
                                                                 return Error NotImplemented
                            | HttpStatusCode.ServiceUnavailable  ->
                                                                 return Error ServiceUnavailable
                            | HttpStatusCode.NotFound            ->
                                                                 return Error NotFound  
                            | _                                  ->
                                                                 return Error CofeeMakerUnavailable 
                                                                 
                        | Error err 
                            -> 
                            err |> ignore  //TODO logfile
                            return Error ConnectionError   
                           
                    with                                                         
                    | ex
                        ->
                        string ex.Message |> ignore  //TODO logfile
                        return Error FileDownloadErrorMHD  
                } 
    
        let downloadTimetables reportProgress (token : CancellationToken) : Result<unit, MHDErrors> = 
        
            try
                let l = filterTimetables |> Map.count
        
                filterTimetables
                |> Map.toList 
                |> List.mapi  //bohuzel s Map nelze Map.mapi nebo Map.iteri
                    (fun i (link, pathToFile) 
                        ->  
                        async                                                
                            {   
                                token.ThrowIfCancellationRequested ()
                                reportProgress (float i + 1.0, float l)  
                                return! downloadFileTaskAsync token link pathToFile
                            } 
                        |> Async.RunSynchronously
                    )  
                |> ignore
                |> Ok

            with
            | ex    
                ->
                let dirName = ODISDefault.OdisDir6                       
                    in
                    match deleteOneODISDirectoryMHD dirName mdpoPathTemp with Ok _ -> () | Error _ -> ()      

                string ex.Message |> ignore //TODO logfile         
                Error StopDownloadingMHD 
                            
        downloadTimetables reportProgress token