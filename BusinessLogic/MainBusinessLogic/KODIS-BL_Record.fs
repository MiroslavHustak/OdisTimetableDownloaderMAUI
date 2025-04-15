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
           
    //************************ Main code ***********************************************************

    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress = //FsHttp
               
        let l = jsonLinkList |> List.length
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
            (jsonLinkList, pathToJsonList)
            ||> List.Parallel.map2
                (fun uri (pathToFile : string) 
                    ->    
                    async  //Async musi byt quli cancellation token
                        {    
                            counterAndProgressBar.Post <| Inc 1                           
                            
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
                                                config_cancellationToken token //token2  //funguje
                                                header headerContent1 headerContent2
                                            }
                                | false ->
                                        http
                                            {
                                                GET uri
                                                config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                config_cancellationToken token //token2 //funguje
                                            }
                            
                            //Async varianta musi byt quli cancellation token
                            use! response = get >> Request.sendAsync <| uri  

                            match response.statusCode with
                            | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                ->         
                                do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                            | _ ->
                                failwith String.Empty                             
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

                    | Error err
                        ->
                        match (string err.Message).Contains "The operation was canceled." with
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
                    let! context = fun env -> env 
            
                    let l = context.list |> List.length
                        in
                        let counterAndProgressBar =
                            MailboxProcessor<MsgIncrement>
                                .StartImmediate
                                    <|
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
                                    async  //Async musi byt quli cancellation token
                                        {    
                                            counterAndProgressBar.Post <| Inc 1
                                                                                       
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
                                                                        config_cancellationToken token //token2  //funguje
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri
                                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token //token2 //funguje
                                                                    }
                                                    
                                                    //Async varianta musi byt quli cancellation token 
                                                    use! response = get >> Request.sendAsync <| uri  
                                                                                                       
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
                                    |> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token)
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
                                        | true  -> Some <| Error StopDownloading
                                        | false -> Some <| Error FileDownloadError
                                )
                            |> Option.defaultValue (Ok ())
                             
                        with
                        | _ -> Error FileDownloadError  //TODO logfile                                                     
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
                                             in                                            
                                             match deleteAllODISDirectories pathToDir with
                                             | Ok _    -> Error err              //TODO logfile  
                                             | Error _ -> Error FileDeleteError  //TODO logfile 
                            with
                            | _ 
                                ->
                                //TODO logfile 
                                let pathToDir = kodisPathTemp                   
                                    in 
                                    match deleteAllODISDirectories pathToDir with
                                    | Ok _    -> Error FileDownloadError 
                                    | Error _ -> Error FileDeleteError  
            }               