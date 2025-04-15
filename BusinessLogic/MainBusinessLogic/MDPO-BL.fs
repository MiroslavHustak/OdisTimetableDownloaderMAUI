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

    let internal safeFilterTimetables () pathToDir token = 

        let fetchHtmlWithFsHttp (url : string) =           
              
            async
                {
                    try
                        use! response =  
                            http
                                {
                                    GET url    
                                    config_cancellationToken token
                                }
                            |> Request.sendAsync //Async varianta musi byt quli cancellation token
        
                        let! htmlContent = Response.toStringAsync (Some 100000) response        
                        let document = HtmlDocument.Parse htmlContent // Parse the HTML content using FSharp.Data
        
                        return Some document        
                        
                    with
                    | _ -> 
                        return None
                }           
                
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
                            let! documentOption = fetchHtmlWithFsHttp url
                
                            match documentOption with
                            | Some document
                                -> return document
                            | None
                                -> return FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz
                        }
                    |> Async.RunSynchronously
                
                //let document = FSharp.Data.HtmlDocument.Load url //exn to be caught in MDPO.fs
                //HtmlDocument -> web scraping -> extracting data from HTML pages
                                                                                    
                document.Descendants "a"                  
                |> Seq.choose 
                    (fun htmlNode   
                        ->
                        htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                        |> Option.bind
                            (fun attr 
                                -> 
                                option  //pyramidOfDoom with None
                                    {
                                        let! nodes = htmlNode.InnerText () |> Option.ofNullEmpty
                                        let! attr = attr.Value () |> Option.ofNullEmpty
                                                               
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
                        let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                        let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                        linkToPdf, (pathToFile << lineName) item2
                    )                          
                |> Seq.distinct                 
            )  
        |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map

    //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)  
    let internal unsafeFilterTimetables () pathToDir token = 

        let fetchHtmlWithFsHttp (url : string) =           
              
            async
                {
                    try
                        use! response =
                            http
                                {
                                    GET url
                                    config_cancellationToken token

                                    config_transformHttpClient
                                        (fun unsafeClient
                                            ->
                                            #if ANDROID
                                            let unsafeHandler = new UnsafeAndroidClientHandler()  //Option.ofNull je tady komplikovane, neb je to uvnitr CE, nechame to na try-with
                                            #else
                                            let unsafeHandler = new HttpClientHandler() //nelze use
                                            unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)                                                
                                            #endif
                                            let unsafeClient = new HttpClient(unsafeHandler) 
                                            unsafeClient
                                        )
                                }
                            |> Request.sendAsync //Async varianta musi byt quli cancellation token
        
                        let! htmlContent = Response.toStringAsync (Some 100000) response
        
                        let document = HtmlDocument.Parse htmlContent
        
                        return Some document                   
                   
                    with
                    | _ -> 
                        return None
                  
                }           
                
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
                            let! documentOption = fetchHtmlWithFsHttp url
                
                            match documentOption with
                            | Some document
                                -> return document
                            | None
                                -> return FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz
                        }
                    |> Async.RunSynchronously
                
                //let document = FSharp.Data.HtmlDocument.Load url //exn to be caught in MDPO.fs
                //HtmlDocument -> web scraping -> extracting data from HTML pages
                                                                                    
                document.Descendants "a"                  
                |> Seq.choose 
                    (fun htmlNode   
                        ->
                        htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                        |> Option.bind
                            (fun attr 
                                -> 
                                option  //pyramidOfDoom with None
                                    {
                                        let! nodes = htmlNode.InnerText () |> Option.ofNullEmpty
                                        let! attr = attr.Value () |> Option.ofNullEmpty
                                                               
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
                        let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                        let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                        linkToPdf, (pathToFile << lineName) item2
                    )                          
                |> Seq.distinct                 
            )  
        |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map

    //FsHttp
    let internal safeDownloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) (filterTimetables : Map<string, string>) =  

        let downloadFileTaskAsync (token : CancellationToken) (uri : string) (pathToFile : string) : Async<Result<unit, MHDErrors>> =  
       
            async
                {                      
                    try  

                        let response =           
                                                      
                            pyramidOfDoom
                                {                                   
                                    ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 //quli Android 7.1

                                    let existingFileLength =                               
                                        checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                        |> function
                                            | Some _ -> (FileInfo pathToFile).Length
                                            | None   -> 0L                                  
                                                    
                                    let getSafe uri = 

                                        let headerContent1 = "Range" 
                                        let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                          
                                        //config_timeoutInSeconds 300 -> 300 vterin, aby to nekolidovalo s odpocitavadlem (max 60 vterin) v XElmish 
                                        match existingFileLength > 0L with
                                        | true  -> 
                                                http
                                                    {
                                                        GET uri        
                                                        
                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        config_cancellationToken token  //CancellationToken.None

                                                        header headerContent1 headerContent2
                                                    }
                                        | false ->
                                                http
                                                    {
                                                        GET uri

                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        config_cancellationToken token
                                                    }     

                                    let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty

                                    //Async varianta musi byt quli cancellation token
                                    let! response = (getSafe >> Request.sendAsync <| uri) |> Option.ofNull, Error String.Empty //Option.ofNull tady neni treba, ale aby to bylo jednotne....

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
                let l = filterTimetables |> Map.count
                    in
                    filterTimetables
                    |> Map.toList 
                    |> List.mapi  //bohuzel s Map nelze Map.mapi nebo Map.iteri
                        (fun i (link, pathToFile) 
                            ->  
                            async   //Async musi byt quli cancellation token                                         
                                {   
                                    token.ThrowIfCancellationRequested ()
                                    reportProgress (float i + 1.0, float l)  
                                    return! downloadFileTaskAsync token link pathToFile
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
                                match (string err.Message).Contains "The operation was canceled." with
                                | true  -> Some <| Error StopDownloadingMHD
                                | false -> Some <| Error (TestDuCase (sprintf "%s%s" (string err.Message) " X01")) ////FileDownloadErrorMHD
                        )
                    |> Option.defaultValue (Ok ()) 

            with
            | ex    //TODO logfile 
                ->
                let dirName = ODISDefault.OdisDir6                       
                    in
                    match deleteOneODISDirectoryMHD dirName mdpoPathTemp with
                    | Ok _    -> Error (TestDuCase (sprintf "%s%s" (string ex.Message) " X02")) ////FileDownloadErrorMHD
                    | Error _ -> Error FileDeleteErrorMHD      
                                         
        downloadTimetables reportProgress token

    //FsHttp
    //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)
    let internal unsafeDownloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) (filterTimetables : Map<string, string>) =  

        let downloadFileTaskAsync (token : CancellationToken) (uri : string) (pathToFile : string) : Async<Result<unit, MHDErrors>> =  
       
            async
                {                      
                    try  
                        let response =           
                                                      
                            pyramidOfDoom
                                {                                   
                                    ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 //quli Android 7.1

                                    let existingFileLength =                               
                                        checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                        |> function
                                            | Some _ -> (FileInfo pathToFile).Length
                                            | None   -> 0L
                                                
                                    let getUnsafe uri = 

                                        let headerContent1 = "Range" 
                                        let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                          
                                        //config_timeoutInSeconds 300 -> 300 vterin, aby to nekolidovalo s odpocitavadlem (max 60 vterin) v XElmish 
                                        match existingFileLength > 0L with
                                        | true  -> 
                                                http
                                                    {
                                                        GET uri        
                                                        
                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        config_cancellationToken token  //CancellationToken.None

                                                        config_transformHttpClient
                                                            (fun unsafeClient //Option.ofNull je tady komplikovane, neb je to uvnitr CE, nechame to na try-with
                                                                ->
                                                                //temporary code, use block not possible here
                                                                let unsafeHandler = new HttpClientHandler()
                                                                unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)
                                                                let unsafeClient = new HttpClient(unsafeHandler)
                                                                unsafeClient
                                                            )

                                                        header headerContent1 headerContent2
                                                    }
                                        | false ->
                                                http
                                                    {
                                                        GET uri

                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        config_cancellationToken token

                                                        config_transformHttpClient
                                                            (fun unsafeClient //Option.ofNull je tady komplikovane, neb je to uvnitr CE, nechame to na try-with
                                                                ->
                                                                //temporary code, use block not possible here
                                                                let unsafeHandler = new HttpClientHandler() 
                                                                unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)
                                                                let unsafeClient = new HttpClient(unsafeHandler)
                                                                unsafeClient
                                                            )
                                                    }                                          

                                    let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty
                                    let! response = (getUnsafe >> Request.sendAsync <| uri) |> Option.ofNull, Error String.Empty //Option.ofNull tady neni treba, ale aby to bylo jednotne....

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
                let l = filterTimetables |> Map.count
                    in
                    filterTimetables
                    |> Map.toList 
                    |> List.mapi  //bohuzel s Map nelze Map.mapi nebo Map.iteri
                        (fun i (link, pathToFile) 
                            ->  
                            async  //Async musi byt quli cancellation token                                              
                                {   
                                    token.ThrowIfCancellationRequested ()
                                    reportProgress (float i + 1.0, float l)  
                                    return! downloadFileTaskAsync token link pathToFile
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
                                match (string err.Message).Contains "The operation was canceled." with
                                | true  -> Some <| Error StopDownloadingMHD
                                | false -> Some <| Error (TestDuCase (sprintf "%s%s" (string err.Message) " X01")) ////FileDownloadErrorMHD
                        )
                    |> Option.defaultValue (Ok ()) 

            with
            | ex    //TODO logfile 
                ->
                let dirName = ODISDefault.OdisDir6                       
                    in
                    match deleteOneODISDirectoryMHD dirName mdpoPathTemp with
                    | Ok _    -> Error (TestDuCase (sprintf "%s%s" (string ex.Message) " X02")) ////FileDownloadErrorMHD
                    | Error _ -> Error FileDeleteErrorMHD      
                                         
        downloadTimetables reportProgress token