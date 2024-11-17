namespace BusinessLogic4

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Net.NetworkInformation

//************************************************************

open FsHttp
open FsToolkit.ErrorHandling

//************************************************************

open Types.Types
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open Settings.Messages
open Settings.SettingsGeneral

open Api.CallApi
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    
    let private myDelete () = 

        let pathToDir = kodisPathTemp                   
                        
        match deleteAllODISDirectories pathToDir with
        | Ok _    -> ()
        | Error _ -> () //potlacime to, nestoji to za to s tim neco robit

    //************************Main code***********************************************************
        
    let internal operationOnDataFromJson token variant dir =   

        try          
            getFromRestApi >> filterTimetableLinks variant dir <| ()
        with
        | ex 
            ->
            string ex.Message |> ignore //TODO logfile                 
            Error DataFilteringError 
                    
    let internal downloadAndSave token = 

    (*
    let withTimeout timeout asyncWork =
    async {
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout))
        try
            return! Async.RunSynchronously(asyncWork, cancellationToken = cts.Token)
        with
        | :? OperationCanceledException -> failwith "Timeout"
    }

    *)

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
                        MailboxProcessor.Start <|
                            fun inbox 
                                ->
                                let rec loop n = 
                                    async { match! inbox.Receive() with Inc i -> context.reportProgress (float n, float l); return! loop (n + i) }
                                loop 0
                                                            
                    return    
                        try
                            use cts = new CancellationTokenSource()
                            
                            let timeLimitInSeconds = 10
                            
                            cts.CancelAfter(TimeSpan.FromSeconds(float timeLimitInSeconds))
                            
                            let token2 =    
                                try
                                    Some cts.Token               
                                with
                                | _ -> 
                                    cts.Dispose()
                                    None      

                            context.list
                            |> List.unzip             
                            ||> context.listMappingFunction
                                (fun uri (pathToFile: string) 
                                    -> 
                                    async
                                        {    
                                            counterAndProgressBar.Post(Inc 1)

                                            try
                                                        
                                                let pathToFileExistFirstCheck = 
                                                    checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        

                                                let comprehensiveCheck = 
                                                    match pathToFileExistFirstCheck, token2 with
                                                    | Some value1, Some value2
                                                        -> Some (value1, value2)
                                                    | _ 
                                                        -> None

                                                try
                                                    // enforcing TLS 1.2 and 1.3.
                                                    ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13
                        
                                                    match comprehensiveCheck with  //tady nelze |> function - smesuje to async a pyramidOfDoom computation expressions
                                                    | Some (_, token2)
                                                        -> 
                                                        let existingFileLength =                               
                                                            checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                                            |> function
                                                                | Some _ -> (FileInfo pathToFile).Length
                                                                | None   -> 0L

                                                        let get uri = 

                                                            let headerContent1 = "Range" 
                                                            let headerContent2 = sprintf "bytes=%d-" existingFileLength                                                           
                    
                                                            match existingFileLength > 0L with
                                                            | true  -> 
                                                                    http
                                                                        {
                                                                            GET uri
                                                                            config_timeoutInSeconds timeLimitInSeconds
                                                                            //config_cancellationToken token
                                                                            config_cancellationToken token2
                                                                            header headerContent1 headerContent2
                                                                        }
                                                            | false ->
                                                                    http
                                                                        {
                                                                            GET uri
                                                                            config_timeoutInSeconds timeLimitInSeconds
                                                                            //config_cancellationToken token
                                                                            config_cancellationToken token2
                                                                        }

                                                        use! response = get >> Request.sendAsync <| uri  
                                                
                                                        match response.statusCode with
                                                        | HttpStatusCode.PartialContent // 206
                                                        | HttpStatusCode.OK  // 200
                                                            ->         
                                                            do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                        | _ ->
                                                            failwith "FileDownloadError"
                                                    | None 
                                                        ->
                                                        failwith "FileDeleteError"   //TODO ono je to aji TokenError                 
                                                finally
                                                    ()
                                            with
                                            | :? AggregateException
                                                as aggEx
                                                    ->
                                                    match aggEx.InnerExceptions |> Seq.exists (fun ex -> ex :? TaskCanceledException) with
                                                    | true  -> failwith "Timeout"
                                                    | false -> failwith "FileDownloadError"
                                            | ex 
                                                -> 
                                                string ex.Message |> ignore //TODO logfile
                                                failwith "FileDownloadError"
                                        } 
                                    |> Async.RunSynchronously                                                        
                                )  
                            |> ignore
                            |> Ok
                            
                            (*
                            match token.IsCancellationRequested with
                            | false -> 
                                    Ok String.Empty
                            | true  ->
                                    Error CancelPdfProcess
                           *)

                        with
                        (*
                        | :? OperationCanceledException 
                            when 
                                token.IsCancellationRequested 
                                    -> 
                                    Error CancelPdfProcess   
                        *)            

                        | :? HttpRequestException as ex 
                            ->
                            match ex.InnerException with
                            | :? System.Security.Authentication.AuthenticationException 
                                -> 
                                Error <| NetConnPdfError netConnError
                            | _ 
                                -> 
                                Error <| NetConnPdfError unKnownError

                        | ex 
                            when ex.Message.Contains("Timeout")
                                -> 
                                Error <| Timeout 

                        | ex 
                            when ex.Message.Contains("FileDownloadError")
                                -> 
                                Error <| FileDownloadError
                        | ex 
                            when ex.Message.Contains("FileDeleteError")
                                -> 
                                Error <| FileDeleteError 
                       
                        | ex    
                                ->
                                string ex.Message |> ignore //TODO logfile         
                                Error <| FileDownloadError
                } 
        
        reader
            {    
                let! context = fun env -> env
                
                return
                    match context.dir |> Directory.Exists with 
                    | false ->
                            Error FileDownloadError                                             
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
                                         | Ok _    -> Error err
                                         | Error _ -> Error FileDeleteError
                            with
                            | ex 
                                ->
                                string ex.Message |> ignore //TODO logfile   
                                myDelete () 
                                Error FileDownloadError 
            }               