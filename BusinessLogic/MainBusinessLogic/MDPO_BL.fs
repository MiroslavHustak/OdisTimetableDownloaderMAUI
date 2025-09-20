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
open Helpers.DirFileHelper

open Api.Logging

open Types.Types
open Types.ErrorTypes

open Settings.SettingsMDPO
open Settings.SettingsGeneral    

open IO_Operations.IO_Operations
open Types.Haskell_IO_Monad_Simulation

module MDPO_BL = //FsHttp

    //************************Submain functions************************************************************************

    let internal safeFilterTimetables pathToDir token = 

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
                                                    config_cancellationToken token  //uz zbytecne, ale ponechavam jako template
                                                }
                                            |> Request.sendAsync //Async varianta musi byt quli cancellation token
        
                                        let! htmlContent = Response.toStringAsync (Some 100000) response        
                                        let document = HtmlDocument.Parse htmlContent // Parse the HTML content using FSharp.Data
        
                                        return Some document        
                        
                                    with
                                    | ex 
                                        -> 
                                        runIO (postToLog <| ex.Message <| "#025")
                       
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
                                        -> return document
                                    | None
                                        -> return FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz
                                }
                            |> Async.RunSynchronously

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
                                                let nodes : string = nodes
                                                let! attr = attr.Value () |> Option.ofNullEmpty
                                                let attrr : string = attr
                                                               
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
        )
    
    //FsHttp
    let internal safeDownloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) filterTimetables =  

        IO (fun () 
                -> 
                let downloadFileTaskAsync (token : CancellationToken) (uri : string) (pathToFile : string) =  
       
                    IO (fun () 
                            -> 
                            async
                                {                      
                                    try

                                        let response =           
                                                      
                                            pyramidOfDoom
                                                {         
                                                    let existingFileLength =                               
                                                        runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
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
                                                                        config_cancellationToken token  //uz zbytecne, ale ponechavam jako template
                                                                        header "User-Agent" "FsHttp/Android7.1"
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri
                                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token //uz zbytecne, ale ponechavam jako template
                                                                        header "User-Agent" "FsHttp/Android7.1"
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
                                                                 
                                        | Error err 
                                            -> 
                                            runIO (postToLog <| err <| "#026")
                           
                                            return Error ConnectionError   
                           
                                    with                                                         
                                    | ex
                                        ->
                                        runIO (postToLog <| ex.Message <| "#027")
                       
                                        return Error FileDownloadErrorMHD  
                                } 
                    )
    
                let downloadTimetables reportProgress (token : CancellationToken) = 

                    IO (fun () 
                            -> 
                            let filterTimetables = runIO <| filterTimetables

                            let l = filterTimetables |> Map.count
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
                                filterTimetables
                                |> Map.toList 
                                |> List.Parallel.map_IO
                                    (fun (link, pathToFile)
                                        -> 
                                        async
                                            {
                                                counterAndProgressBar.Post <| Inc 1
                                                token.ThrowIfCancellationRequested () //tady rychlejsi, nez s config_cancellationToken
                                                return! runIO <| downloadFileTaskAsync token link pathToFile
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
                                            runIO (postToLog <| ex.Message <| "#028")
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
                                        
                                        runIO (postToLog <| ex.Message <| "#281")
                                        
                                        let!_ =
                                            runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) mdpoPathTemp, 
                                                (fun _
                                                    ->
                                                    runIO (postToLog <| FileDeleteErrorMHD <| "#283")                             
                                                    Error FileDeleteErrorMHD
                                                )

                                        runIO (postToLog <| FileDownloadErrorMHD <| "#282") 

                                        return Error FileDownloadErrorMHD                                            
                                    }
                    )                     
                runIO <| downloadTimetables reportProgress token
        )
    
    //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)  
    let internal unsafeFilterTimetables pathToDir token = 

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
                                                    config_cancellationToken token  //uz zbytecne, ale ponechavam jako template

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
                                    | ex 
                                        -> 
                                        runIO (postToLog <| ex.Message <| "#029")
                       
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
                                        -> return document
                                    | None
                                        -> return FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz
                                }
                            |> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token)
                
                        //Exceptions for FSharp.Data.HtmlDocument.Load url and fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token)
                        //to be caught in MDPO.fs

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
                                let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                                let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                                linkToPdf, (pathToFile << lineName) item2
                            )                          
                        |> Seq.distinct                 
                    )  
                |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map
        )

    //FsHttp
    //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)
    let internal unsafeDownloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) filterTimetables =  

        IO (fun () 
                -> 
                let downloadFileTaskAsync (token : CancellationToken) (uri : string) (pathToFile : string) =  

                    IO (fun () 
                            -> 
                            async
                                {                      
                                    try  
                                        let response =           
                                                      
                                            pyramidOfDoom
                                                {                
                                                    let existingFileLength =                               
                                                        runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
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
                                                        
                                                                        config_timeoutInSeconds 300     //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token  //uz zbytecne, ale ponechavam jako template

                                                                        config_transformHttpClient
                                                                            (fun unsafeClient //Option.ofNull je tady komplikovane, neb je to uvnitr CE, nechame to na try-with
                                                                                ->
                                                                                //temporary code, use block not possible here
                                                                                let unsafeHandler = new HttpClientHandler()
                                                                                unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)
                                                                                let unsafeClient = new HttpClient(unsafeHandler)
                                                                                unsafeClient
                                                                            )

                                                                        header "User-Agent" "FsHttp/Android7.1"
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri

                                                                        config_timeoutInSeconds 300     //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token  //uz zbytecne, ale ponechavam jako template

                                                                        config_transformHttpClient
                                                                            (fun unsafeClient //Option.ofNull je tady komplikovane, neb je to uvnitr CE, nechame to na try-with
                                                                                ->
                                                                                //temporary code, use block not possible here
                                                                                let unsafeHandler = new HttpClientHandler() 
                                                                                unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)
                                                                                let unsafeClient = new HttpClient(unsafeHandler)
                                                                                unsafeClient
                                                                            )

                                                                        header "User-Agent" "FsHttp/Android7.1"
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
                                                                 
                                        | Error err 
                                            -> 
                                            runIO (postToLog <| err <| "#030")
                           
                                            return Error ConnectionError   
                           
                                    with                                                         
                                    | ex
                                        ->
                                        runIO (postToLog <| ex.Message <| "#031")
                      
                                        return Error FileDownloadErrorMHD  
                                } 
                    )        
    
                let downloadTimetables reportProgress (token : CancellationToken) = 

                    IO (fun () 
                            -> 
                            let filterTimetables = runIO <| filterTimetables

                            let l = filterTimetables |> Map.count
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
                                filterTimetables
                                |> Map.toList 
                                |> List.Parallel.map_IO
                                    (fun (link, pathToFile)
                                        -> 
                                        async
                                            {
                                                counterAndProgressBar.Post <| Inc 1
                                                token.ThrowIfCancellationRequested () //tady rychlejsi, nez s config_cancellationToken
                                                return! runIO <| downloadFileTaskAsync token link pathToFile
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
                                            runIO (postToLog <| ex.Message <| "#032")
                                            Some <| Error (TestDuCase (sprintf "%s%s" (string ex.Message) " X01")) // FileDownloadErrorMHD
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
                                        
                                        runIO (postToLog <| ex.Message <| "#033")
                                        
                                        let!_ = 
                                            runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) mdpoPathTemp, 
                                                (fun _
                                                    ->
                                                    runIO (postToLog <| FileDeleteErrorMHD <| "#332")                             
                                                    Error FileDeleteErrorMHD
                                                )

                                        runIO (postToLog <| FileDownloadErrorMHD <| "#331") 

                                        return Error FileDownloadErrorMHD                                            
                                    }
                    )                     
                runIO <| downloadTimetables reportProgress token
        )