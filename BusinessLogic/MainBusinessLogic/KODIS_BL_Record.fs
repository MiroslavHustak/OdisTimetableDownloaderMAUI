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

open Api.Logging
open Api.FutureLinks

open Helpers
open Helpers.Builders
open Helpers.DirFileHelper

open JsonData.SortJsonData
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks
open Types.Haskell_IO_Monad_Simulation

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
            (token, jsonLinkList, pathToJsonList)
            |||> List.Parallel.map2_IO_Token 
                (fun uri (pathToFile : string) 
                    ->    
                    async  //Async musi byt quli cancellation token
                        {    
                            counterAndProgressBar.Post <| Inc 1                           
                            
                            token.ThrowIfCancellationRequested ()                            
                                                                    
                            let existingFileLength =                               
                                runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
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
                                                //config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                config_cancellationToken token 
                                                header headerContent1 headerContent2
                                            }
                                | false ->
                                        http
                                            {
                                                GET uri
                                                //config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                config_cancellationToken token 
                                            }
                            
                            //Async varianta musi byt quli cancellation token
                            use! response = get >> Request.sendAsync <| uri  

                            let statusCode = response.statusCode

                            match statusCode with
                            | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                ->         
                                do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                            | HttpStatusCode.Forbidden 
                                ->
                                postToLogFile () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2111") 
                                |> Async.RunSynchronously 
                                |> ignore<ResponsePost> 
                            | _ ->
                                postToLog <| statusCode <| "#2112"
                            
                            token.ThrowIfCancellationRequested ()  
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
                        when (string ex.Message).Contains "SSL connection could not be established" 
                        ->
                        postToLog <| string ex.Message <| "#74764-20"
                        None               

                    | Error ex
                        ->
                        postToLog <| ex.Message <| "#20"

                        match (string ex.Message).Contains "OperationCanceled" with 
                        | true  -> Some <| Error StopJsonDownloading
                        | false -> Some <| Error JsonDownloadError                      
                )
            |> Option.defaultValue (Ok ())
                             
        with
        | ex  
            ->
            postToLog <| ex.Message <| "#21"
            
            match (string ex.Message).Contains "OperationCanceled" with 
            | true  -> Error StopJsonDownloading
            | false -> Error JsonDownloadError  
    
    let internal operationOnDataFromJson (reportProgress : float * float -> unit) token variant dir =   

        try               
            digThroughJsonStructure reportProgress token
            |> filterTimetableLinks variant dir 
        with
        | ex
            ->
            postToLog <| ex.Message <| "#22"
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
                            let uri, pathToFile = context.list |> List.unzip

                            (token, uri, pathToFile)
                            |||> List.Parallel.map2_IO_Token //context.listMappingFunction                            
                                (fun uri (pathToFile : string) 
                                    -> 
                                    // The external async block, combined with cancellation-aware async operations, makes code much more responsive to cancellation.
                                    async  // to support cancellation in the middle of asynchronous operations
                                        {    
                                            counterAndProgressBar.Post <| Inc 1
                                                   
                                            // Artificial checkpoint
                                            // Good practice to place it here if you have any synchronous or CPU-bound work between async calls.
                                            token.ThrowIfCancellationRequested () 

                                            let pathToFileExistFirstCheck = 
                                                runIO <| checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                in
                                                match pathToFileExistFirstCheck with  
                                                | Some _
                                                    -> 
                                                    let existingFileLength =                               
                                                        runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
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
                                                                        //config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token 
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri
                                                                        //config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token 
                                                                    }
                                                    
                                                    // Cancellation-aware async operation
                                                    use! response = get >> Request.sendAsync <| uri  
                                                                                                       
                                                    let statusCode = response.statusCode
                                                    
                                                    match statusCode with
                                                    | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                                        ->         
                                                        // Cancellation-aware async operation
                                                        do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                    | HttpStatusCode.Forbidden 
                                                        ->
                                                        postToLogFile () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2211") 
                                                        |> Async.RunSynchronously 
                                                        |> ignore<ResponsePost> 
                                                    | _ ->
                                                        postToLog <| statusCode <| "#2212"

                                                    token.ThrowIfCancellationRequested ()  

                                                | None 
                                                    ->
                                                    failwith "Failed pathToFileExistFirstCheck"     
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
                                        when (string ex.Message).Contains "SSL connection could not be established" 
                                        ->
                                        postToLog <| string ex.Message <| "#74764-23"
                                        None                                        

                                    | Error ex
                                        ->
                                        postToLog <| string ex.Message <| "#23"
                                                                        
                                        match (string ex.Message).Contains "OperationCanceled" with 
                                        | true  -> Some <| Error StopDownloading
                                        | false -> Some <| Error FileDownloadError  
                                )
                            |> Option.defaultValue (Ok ())
                             
                        with
                        | ex                             
                            -> 
                            postToLog <| string ex.Message <| "#24"
                            
                            match (string ex.Message).Contains "OperationCanceled" with 
                            | true  -> Error StopDownloading
                            | false -> Error FileDownloadError     //melo by byt pouze pro Async.RunSynchronously(workflow, cancellationToken = token)                                                        
                } 
        
        reader
            {    
                let! context = fun env -> env
                
                return
                    match context.dir |> Directory.Exists with 
                    | false ->
                            postToLog <| NoFolderError <| "#251"
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
                                         postToLog <| err <| "#25"
                                        
                                         let pathToDir = kodisPathTemp                   
                                             in                                            
                                             match deleteAllODISDirectories pathToDir with
                                             | Ok _    
                                                 -> 
                                                 postToLog <| err <| "#252"
                                                 Error err              
                                             | Error _ 
                                                 ->
                                                 postToLog <| err <| "#253"
                                                 Error FileDeleteError  

                            with
                            | ex 
                                ->
                                postToLog <| ex.Message <| "#26"
                                                               
                                let pathToDir = kodisPathTemp                   
                                    in 
                                    match deleteAllODISDirectories pathToDir with
                                    | Ok _    
                                        -> 
                                        postToLog <| FileDownloadError <| "#261"
                                        Error FileDownloadError 
                                    | Error _ 
                                        -> 
                                        postToLog <| FileDeleteError <| "#262"
                                        Error FileDeleteError  
            }               