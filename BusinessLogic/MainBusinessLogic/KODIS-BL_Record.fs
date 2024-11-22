namespace BusinessLogic

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Net.NetworkInformation

//************************************************************

open FsHttp
open FsToolkit.ErrorHandling

//************************************************************

open Types
open Types.Types
open Types.ErrorTypes

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral

open Helpers
open Helpers.Builders
open Helpers.FileInfoHelper

open JsonData.SortJsonData
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record =            
   
    let private myDelete () = 

        let pathToDir = kodisPathTemp                   
                        
        match deleteAllODISDirectories pathToDir with
        | Ok _    -> ()
        | Error _ -> () //potlacime to, nestoji to za to s tim neco robit

    //************************Main code***********************************************************

    //For testing purposes
    let internal downloadAndSaveJsonTest () =   
        
        let pathKodisWeb9 = @"https://kodis-backend-staging-85d01eccf627.herokuapp.com/api/linky-search?"           
        let jsonLink9 = sprintf "%s%s" pathKodisWeb9 "groups%5B0%5D=MHD%20Brunt%C3%A1l&groups%5B1%5D=MHD%20%C4%8Cesk%C3%BD%20T%C4%9B%C5%A1%C3%ADn&groups%5B2%5D=MHD%20Fr%C3%BDdek-M%C3%ADstek&groups%5B3%5D=MHD%20Hav%C3%AD%C5%99ov&groups%5B4%5D=MHD%20Karvin%C3%A1&groups%5B5%5D=MHD%20Krnov&groups%5B6%5D=MHD%20Nov%C3%BD%20Ji%C4%8D%C3%ADn&groups%5B7%5D=MHD%20Opava&groups%5B8%5D=MHD%20Orlov%C3%A1&groups%5B9%5D=MHD%20Ostrava&groups%5B10%5D=MHD%20Stud%C3%A9nka&groups%5B11%5D=MHD%20T%C5%99inec&groups%5B12%5D=NAD%20MHD&start=0&limit=12"       
                
        async
            {    
                use! response = get >> Request.sendAsync <| jsonLink9 
                do! response.SaveFileAsync >> Async.AwaitTask <| @"e:\FabulousMAUI\test.json"    

                return "Test"
            }    
   
    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress = //FsHttp
               
        let l = jsonLinkList |> List.length

        let counterAndProgressBar =
            MailboxProcessor.Start <|
                fun inbox 
                    ->
                    let rec loop n = 
                        async { match! inbox.Receive() with Inc i -> reportProgress (float n, float l); return! loop (n + i) }
                    loop 0
      
        try 
            (jsonLinkList, pathToJsonList)
            ||> List.Parallel.map2
                (fun uri (pathToFile: string) 
                    ->                
                        //monitorConnectivity (token : CancellationToken) 
                       
                       async
                            {    
                                //invoking config_timeoutInSeconds config_cancellationToken se projevi az po RunSynchronously, bohuzel...

                                counterAndProgressBar.Post(Inc 1)

                                try                                                                    
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
                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        //config_cancellationToken token2  //funguje
                                                        header headerContent1 headerContent2
                                                    }
                                        | false ->
                                                http
                                                    {
                                                        GET uri
                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                        //config_cancellationToken token2 //funguje
                                                    }

                                    use! response = get >> Request.sendAsync <| uri  

                                    match response.statusCode with
                                    | HttpStatusCode.PartialContent // 206
                                    | HttpStatusCode.OK  // 200
                                        ->         
                                        do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                    | _ ->
                                        ()                                           
                            
                                with
                                | ex 
                                    when ex.Message.Contains("A task was canceled") 
                                    -> 
                                    string ex.Message |> ignore //TODO logfile
                                    failwith "TimeoutError"                                

                                | ex 
                                    -> 
                                    string ex.Message |> ignore //TODO logfile
                                    failwith "JsonDownloadError"   
                            } 
                        |> Async.RunSynchronously //vsechny exn az po "dokonceni", tj. az po zapnuti internetu
                        //Async.RunSynchronously (asyncWork (), 5000, token2) nepomohlo, opet ceka na dokonceni, tj. po zapnuti internetu
                        //Async.Catch tady neni vhodny
                )  
            |> ignore
            |> Ok
                             
        with
        | ex 
            when ex.Message.Contains("TimeoutError")
                -> 
                string ex.Message |> ignore //TODO logfile
                Error JsonTimeoutError 

        | ex    
                ->
                string ex.Message |> ignore //TODO logfile         
                Error JsonDownloadError 
    
    let internal operationOnDataFromJson (token : CancellationToken) variant dir =   

        try               
            digThroughJsonStructure >> filterTimetableLinks variant dir <| token 
        with
        | ex
            ->
            string ex.Message |> ignore //TODO logfile                 
            Error DataFilteringError 
                    
    let internal downloadAndSave token = //v parametru je token jen quli educational code v App.fs
        
        let downloadAndSaveTimetables (token : CancellationToken) =  //v parametru je token jen quli educational code v App.fs
            
            reader
                {             
                    let checkFileCondition pathToFile condition =
                        
                        pyramidOfDoom
                            {
                                let filepath = pathToFile |> Path.GetFullPath |> Option.ofNullEmpty 
                                let! filepath = filepath, None
                                    
                                let fInfodat: FileInfo = FileInfo filepath
                                let! _ = condition fInfodat |> Option.ofBool, None  
                                                                 
                                return Some ()
                            }                    
                        
                    let! context = fun env -> env 
            
                    let l = context.list |> List.length
            
                    let counterAndProgressBar =
                        MailboxProcessor.Start <|
                            fun inbox 
                                ->
                                let rec loop n = 
                                    async { match! inbox.Receive() with Inc i -> context.reportProgress (float n, float l); return! loop (n + i) }
                                loop 0
                                                                
                    return    
                        try 
                            //monitorConnectivity (token : CancellationToken)
                                
                            context.list
                            |> List.unzip             
                            ||> context.listMappingFunction
                                (fun uri (pathToFile: string) 
                                    -> 
                                    //let token2 = tokenTrigger ()  //zatim nepotrebne
                                       
                                    async
                                        {    
                                            //invoking config_timeoutInSeconds config_cancellationToken se projevi az po RunSynchronously, bohuzel...

                                            counterAndProgressBar.Post(Inc 1)

                                            try                                                                    
                                                // enforcing TLS 1.2 and 1.3.
                                                ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13

                                                let pathToFileExistFirstCheck = 
                                                    checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                            
                                                match pathToFileExistFirstCheck with  //tady nelze |> function - smesuje to async a pyramidOfDoom computation expressions
                                                | Some _
                                                    -> 
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
                                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        //config_cancellationToken token2  //funguje
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri
                                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        //config_cancellationToken token2 //funguje
                                                                    }

                                                    use! response = get >> Request.sendAsync <| uri  

                                                    (*

                                                    let! response =
                                                        Async.StartChild
                                                            (
                                                                async
                                                                    {
                                                                        let get uri = 
                                                                        
                                                                            let headerContent1 = "Range" 
                                                                            let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                                                                                                
                                                                            match existingFileLength > 0L with
                                                                            | true  -> 
                                                                                    http
                                                                                        {
                                                                                            GET uri
                                                                                            config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                                            config_cancellationToken token2
                                                                                            header headerContent1 headerContent2
                                                                                        }
                                                                            | false ->
                                                                                    http
                                                                                        {
                                                                                            GET uri
                                                                                            config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                                            config_cancellationToken token2
                                                                                        }

                                                                        return! (get >> Request.sendAsync <| uri) 
                                                                    },
                                                                    5 * 1000
                                                            )
                                                        
                                                    let! response = response                                                       
                                                     *)

                                                    match response.statusCode with
                                                    | HttpStatusCode.PartialContent // 206
                                                    | HttpStatusCode.OK  // 200
                                                        ->         
                                                        do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                    | _ ->
                                                        ()

                                                | None 
                                                    ->
                                                    failwith "FileDeleteError"                 
                                                    
                                            with
                                            | ex 
                                                when ex.Message.Contains("A task was canceled") 
                                                -> 
                                                string ex.Message |> ignore //TODO logfile
                                                failwith "TimeoutError"   
                                                                                 
                                            | ex 
                                                when ex.Message.Contains("FileDeleteError") 
                                                -> 
                                                string ex.Message |> ignore //TODO logfile
                                                failwith "FileDeleteError"

                                            | ex 
                                                -> 
                                                string ex.Message |> ignore //TODO logfile
                                                failwith "FileDownloadError"   
                                        } 
                                    |> Async.RunSynchronously //vsechny exn az po "dokonceni", tj. az po zapnuti internetu
                                    //Async.RunSynchronously (asyncWork (), 5000, token2) nepomohlo, opet ceka na dokonceni, tj. po zapnuti internetu
                                    //Async.Catch tady neni vhodny
                                )  
                            |> ignore
                            |> Ok
                             
                        with
                        | ex 
                            when ex.Message.Contains("TimeoutError")
                                -> 
                                string ex.Message |> ignore //TODO logfile
                                Error TimeoutError 

                        | ex 
                            when ex.Message.Contains("FileDeleteError")
                                -> 
                                string ex.Message |> ignore //TODO logfile
                                Error FileDeleteError 

                        | ex    
                                ->
                                string ex.Message |> ignore //TODO logfile         
                                Error FileDownloadError //FileDownloadError
                } 
        
        reader
            {    
                let! context = fun env -> env
                
                return
                    match context.dir |> Directory.Exists with 
                    | false ->
                            noFolderError |> ignore //TODO logfile  
                            Error NoFolderError                                             
                    | true  ->
                            try
                                match context.list with
                                | [] -> 
                                     Ok String.Empty 

                                | _  ->       
                                     match downloadAndSaveTimetables token context with
                                     | Ok _     
                                         -> 
                                         Ok String.Empty

                                     | Error err 
                                         ->
                                         let pathToDir = kodisPathTemp                   
                                                                                     
                                         match deleteAllODISDirectories pathToDir with
                                         | Ok _   
                                             -> 
                                             string err |> ignore //TODO logfile  
                                             Error err

                                         | Error _ 
                                             ->
                                             "FileDeleteError" |> ignore //TODO logfile  
                                             Error FileDeleteError
                            with
                            | ex 
                                ->
                                string ex.Message |> ignore //TODO logfile   

                                let pathToDir = kodisPathTemp                   
                        
                                match deleteAllODISDirectories pathToDir with
                                | Ok _    -> Error FileDownloadError 
                                | Error _ -> Error FileDownloadError  //tady uz nestoji za to resit zaroven FileDeleteError
            }               