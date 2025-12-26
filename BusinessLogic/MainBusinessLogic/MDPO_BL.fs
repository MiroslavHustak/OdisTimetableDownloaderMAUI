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
open Types.Grid3Algebra

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
                            try   
                                use response =  
                                    http
                                        {
                                            GET url    
                                            config_cancellationToken token 
                                        }
                                    |> Request.send 
        
                                let htmlContent = Response.toString (Some 100000) response        
                                Some <| HtmlDocument.Parse htmlContent // Parse the HTML content using FSharp.Data                        
                            with
                            | ex 
                                -> 
                                //runIO (postToLog <| string ex.Message <| "#025")                       
                                None
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
                            let documentOption = fetchHtmlWithFsHttp >> runIO <| url
                            
                            match documentOption with
                            | Some document
                                -> document
                            | None
                                -> FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz                              

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
                                //chybne odkazy jsou pozdeji tise eliminovany
                                let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                                let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                                linkToPdf, pathToFile << lineName <| item2
                            )                          
                        |> Seq.distinct   
                        |> Seq.filter (fun (item1, item2) -> not (isNull item1 || isNull item2)) //just in case
                    )  
                |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map
        )
    
    //FsHttp
    let internal safeDownloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) filterTimetables =  

        IO (fun () 
                -> 
                let downloadFileTask (token : CancellationToken) (uri : string) (pathToFile : string) =  
       
                    IO (fun () 
                            ->                                                 
                            try

                                let response =           
                                                      
                                    pyramidOfDoom
                                        {         
                                            let existingFileLength =  
                                                // TOCTOU race problem is negligible here as the value is only for the Windows Machine mode / resuming downloads
                                                // Resuming downloading does not work under Android OS
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
                                                                config_timeoutInSeconds 30 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                config_cancellationToken token  
                                                                header "User-Agent" "FsHttp/Android7.1"
                                                                header headerContent1 headerContent2
                                                            }
                                                | false ->
                                                        http
                                                            {
                                                                GET uri
                                                                config_timeoutInSeconds 30 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                config_cancellationToken token 
                                                                header "User-Agent" "FsHttp/Android7.1"
                                                            }     

                                            //TOCTOU race -> try-with will catch
                                            //let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty

                                            let! response = (getSafe >> Request.send <| uri) |> Option.ofNull, Error String.Empty //Option.ofNull tady neni treba, ale aby to bylo jednotne....

                                            return Ok response         
                                        }                       

                                match response with
                                | Ok response
                                    ->      
                                    use response = response  
                        
                                    match response.statusCode with        //TODO logfile
                                    | HttpStatusCode.OK                  -> Ok <| response.SaveFile pathToFile    
                                    | HttpStatusCode.BadRequest          -> Error BadRequest     
                                    | HttpStatusCode.InternalServerError -> Error InternalServerError
                                    | HttpStatusCode.NotImplemented      -> Error NotImplemented
                                    | HttpStatusCode.ServiceUnavailable  -> Error ServiceUnavailable
                                    | HttpStatusCode.NotFound            -> Error NotFound
                                    | _                                  -> Error CofeeMakerUnavailable
                                                                             
                                                                 
                                | Error err 
                                    -> 
                                    runIO (postToLog <| err <| "#026")                           
                                    Error ConnectionError   
                           
                            with                                                         
                            | ex
                                ->
                                runIO (postToLog <| string ex.Message <| "#027")                       
                                Error FileDownloadErrorMHD  
                    )
    
                let downloadTimetables reportProgress (token : CancellationToken) = 

                    IO (fun () 
                            -> 
                            let filterTimetables = runIO filterTimetables

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
                                        counterAndProgressBar.Post <| Inc 1
                                        token.ThrowIfCancellationRequested () //tady rychlejsi, nez s config_cancellationToken
                                        runIO <| downloadFileTask token link pathToFile                                       
                                    )                                 
                                |> List.tryPick
                                    (Result.either
                                        (fun _
                                            ->
                                            None
                                        )
                                        (fun err 
                                            ->                                            
                                            runIO (postToLog <| string err <| "#28")
                                            Some (Error FileDownloadErrorMHD)  
                                        )
                                    )                                
                                |> Option.defaultValue (Ok ()) 

                            with                            
                            | ex                             
                                -> 
                                match Helpers.ExceptionHelpers.isCancellation ex with
                                | true
                                   ->
                                   Error StopDownloadingMHD
                                | false 
                                   ->
                                   runIO (postToLog <| string ex.Message <| "#281")
                                   Error FileDownloadErrorMHD  
                    )                
                    
                runIO <| downloadTimetables reportProgress token
        )

    //**************************************** UNSAFE CODE, NOT FOR PRODUCTION ****************************************
    
    //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)  
    let internal unsafeFilterTimetables pathToDir token = 

        IO (fun () 
                -> 
                let fetchHtmlWithFsHttp (url : string) =   
        
                    IO (fun () 
                            -> 
                            try
                                use response =
                                    http
                                        {
                                            GET url
                                            config_cancellationToken token  

                                            config_transformHttpClient
                                                (fun unsafeClient
                                                    ->
                                                    #if ANDROID
                                                    let unsafeHandler = new JavaInteroperabilityCode.UnsafeAndroidClientHandler()  //Option.ofNull je tady komplikovane, neb je to uvnitr CE, nechame to na try-with
                                                    #else
                                                    let unsafeHandler = new HttpClientHandler() //nelze use
                                                    unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)                                                
                                                    #endif
                                                    let unsafeClient = new HttpClient(unsafeHandler) 
                                                    unsafeClient
                                                )
                                        }
                                    |> Request.send 
                                            
                                let htmlContent = Response.toString (Some 100000) response
                                
                                Some <| HtmlDocument.Parse htmlContent
                   
                            with
                            | ex 
                                -> 
                                runIO (postToLog <| string ex.Message <| "#029")                       
                                None                  
                                 
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
                            let documentOption = fetchHtmlWithFsHttp >> runIO <| url
                
                            match documentOption with
                            | Some document
                                -> document
                            | None
                                -> FSharp.Data.HtmlDocument.Load url //tohle vyhodi net_http_ssl_connection_failed pro mdpo.cz                             
                
                        //Exceptions for FSharp.Data.HtmlDocument.Load url to be caught in MDPO.fs

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
                                                let! attr = attr.Value  |> Option.ofNullEmpty
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
                                let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                                let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                                linkToPdf, pathToFile << lineName <| item2
                            ) 
                        |> Seq.distinct    
                        |> Seq.filter (fun (item1, item2) -> not (isNull item1 || isNull item2)) //just in case                                         
                    )  
                |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map
        )

    //FsHttp
    //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)
    let internal unsafeDownloadAndSaveTimetables reportProgress (token : CancellationToken) (pathToDir : string) filterTimetables =  

        IO (fun () 
                -> 
                let downloadFileTask (token : CancellationToken) (uri : string) (pathToFile : string) =  

                    IO (fun () 
                            ->             
                            try             
                                let existingFileLength =         
                                    // TOCTOU race problem is negligible here as the value is only for the Windows Machine mode / resuming downloads
                                    // Resuming downloading does not work under Android OS
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
                                                        
                                                    config_timeoutInSeconds 30     //pouzije se kratsi cas, pokud zaroven token a timeout
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

                                                    header "User-Agent" "FsHttp/Android7.1"
                                                    header headerContent1 headerContent2
                                                }
                                    | false ->
                                            http
                                                {
                                                    GET uri

                                                    config_timeoutInSeconds 30     //pouzije se kratsi cas, pokud zaroven token a timeout
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

                                                    header "User-Agent" "FsHttp/Android7.1"
                                                }                                          
                                //TOCTOU race -> try-with will catch
                                //let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty
                                use response = (getUnsafe >> Request.send <| uri) //Option.ofNull tady neni treba                                    
                        
                                match response.statusCode with        //TODO logfile
                                | HttpStatusCode.OK                  -> Ok <| response.SaveFile pathToFile                                                                             
                                | HttpStatusCode.BadRequest          -> Error BadRequest  
                                | HttpStatusCode.InternalServerError -> Error InternalServerError
                                | HttpStatusCode.NotImplemented      -> Error NotImplemented
                                | HttpStatusCode.ServiceUnavailable  -> Error ServiceUnavailable
                                | HttpStatusCode.NotFound            -> Error NotFound
                                | _                                  -> Error CofeeMakerUnavailable
                               
                            with                                                         
                            | ex
                                ->
                                runIO (postToLog <| string ex.Message <| "#031")
                                Error FileDownloadErrorMHD  
                    )        
    
                let downloadTimetables reportProgress (token : CancellationToken) = 

                    IO (fun () 
                            -> 
                            let filterTimetables = runIO filterTimetables

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
                                        counterAndProgressBar.Post <| Inc 1
                                        token.ThrowIfCancellationRequested () 
                                        runIO <| downloadFileTask token link pathToFile
                                    )   
                                |> List.tryPick
                                    (Result.either
                                        (fun _
                                            ->
                                            None
                                        )
                                        (fun err 
                                            ->                                          
                                            runIO (postToLog <| string err <| "#32")
                                            Some (Error FileDownloadErrorMHD)  
                                        )
                                    )                                
                                |> Option.defaultValue (Ok ()) 

                            with                            
                            | ex                             
                                -> 
                                match Helpers.ExceptionHelpers.isCancellation ex with
                                | true
                                   ->
                                   Error StopDownloadingMHD
                                | false 
                                   ->
                                   runIO (postToLog <| string ex.Message <| "#33")
                                   Error FileDownloadErrorMHD  
                    )   
                    
                runIO <| downloadTimetables reportProgress token
        )