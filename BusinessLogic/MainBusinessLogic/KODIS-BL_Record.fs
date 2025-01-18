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
    
    //************************ For testing purposes ***********************************************************

    let internal downloadAndSaveJsonTest () =   
        
        let pathKodisWeb9 = @"https://kodis-backend-staging-85d01eccf627.herokuapp.com/api/linky-search?"           
        let jsonLink9 = sprintf "%s%s" pathKodisWeb9 "groups%5B0%5D=MHD%20Brunt%C3%A1l&groups%5B1%5D=MHD%20%C4%8Cesk%C3%BD%20T%C4%9B%C5%A1%C3%ADn&groups%5B2%5D=MHD%20Fr%C3%BDdek-M%C3%ADstek&groups%5B3%5D=MHD%20Hav%C3%AD%C5%99ov&groups%5B4%5D=MHD%20Karvin%C3%A1&groups%5B5%5D=MHD%20Krnov&groups%5B6%5D=MHD%20Nov%C3%BD%20Ji%C4%8D%C3%ADn&groups%5B7%5D=MHD%20Opava&groups%5B8%5D=MHD%20Orlov%C3%A1&groups%5B9%5D=MHD%20Ostrava&groups%5B10%5D=MHD%20Stud%C3%A9nka&groups%5B11%5D=MHD%20T%C5%99inec&groups%5B12%5D=NAD%20MHD&start=0&limit=12"       
                
        async
            {    
                use! response = get >> Request.sendAsync <| jsonLink9 
                do! response.SaveFileAsync >> Async.AwaitTask <| @"e:\FabulousMAUI\test.json"    

                return "Test"
            }    
   
    //************************ Main code ***********************************************************

    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress = //FsHttp
               
        let l = jsonLinkList |> List.length

        let counterAndProgressBar =
            MailboxProcessor<MsgIncrement>.StartImmediate <|
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
                    async
                        {    
                            counterAndProgressBar.Post(Inc 1)
                           
                            token.ThrowIfCancellationRequested ()
                           
                            ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 //quli Android 7.1
                                                                    
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
                            | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                ->         
                                do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                            | _ ->
                                failwith String.Empty                             
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
                        | true  -> Some <| Error StopJsonDownloading
                        | false -> Some <| Error JsonDownloadError
                )
            |> Option.defaultValue (Ok ())
                             
        with
        | _  ->  Error JsonDownloadError //TODO logfile              
    
    let internal operationOnDataFromJson (token : CancellationToken) variant dir =   

        try               
            digThroughJsonStructure >> filterTimetableLinks variant dir <| token 
        with
        | ex
            ->
            string ex.Message |> ignore //TODO logfile                 
            Error DataFilteringError 
                    
    let internal downloadAndSave token = 
        
        let downloadAndSaveTimetables (token : CancellationToken) =  
            
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
                        MailboxProcessor<MsgIncrement>.StartImmediate <|
                            fun inbox 
                                ->
                                let rec loop n = 
                                    async { match! inbox.Receive() with Inc i -> context.reportProgress (float n, float l); return! loop (n + i) }
                                loop 0
                                                                
                    return    
                        try 
                            context.list
                            |> List.unzip             
                            ||> context.listMappingFunction
                                (fun uri (pathToFile: string) 
                                    -> 
                                    async
                                        {    
                                            counterAndProgressBar.Post(Inc 1)
                                                                                       
                                            token.ThrowIfCancellationRequested ()

                                            ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 //quli Android 7.1

                                            let pathToFileExistFirstCheck = 
                                                checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                in
                                                match pathToFileExistFirstCheck with  
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
                                                    | HttpStatusCode.PartialContent | HttpStatusCode.OK  // 206    // 200
                                                        ->         
                                                        do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                    | _ ->
                                                        failwith String.Empty  

                                                | None 
                                                    ->
                                                    failwith String.Empty                                             
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
                                        | true  -> Some <| Error StopDownloading
                                        | false -> Some <| Error FileDownloadError
                                )
                            |> Option.defaultValue (Ok ())
                             
                        with
                        | _ ->  Error FileDownloadError  //TODO logfile                                                     
                } 
        
        reader
            {    
                let! context = fun env -> env
                
                return
                    match context.dir |> Directory.Exists with 
                    | false ->
                            //TODO logfile  
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
                                         | Ok _    -> Error err              //TODO logfile  
                                         | Error _ -> Error FileDeleteError  //TODO logfile 
                            with
                            | _ 
                                ->
                                //TODO logfile 
                                let pathToDir = kodisPathTemp                   
                        
                                match deleteAllODISDirectories pathToDir with
                                | Ok _    -> Error FileDownloadError 
                                | Error _ -> Error FileDeleteError  
            }               