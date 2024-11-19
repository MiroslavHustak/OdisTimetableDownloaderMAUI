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
open FSharp.Control
open FsToolkit.ErrorHandling

//************************************************************

open Types.Types
open Types.ErrorTypes

open Helpers
open Helpers.Builders
open Helpers.Connectivity

open Settings.Messages
open Settings.SettingsGeneral

open Api.CallApi
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks
open Fabulous.Maui

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  

    let private actor = //actor model

        MailboxProcessor.StartImmediate(fun inbox ->

            let rec loop (isConnected : bool) = 
                async
                    {
                        match! inbox.Receive() with
                        | UpdateState newState
                            ->
                            return! loop newState

                        | CheckState replyChannel
                            ->                            
                            replyChannel.Reply(isConnected) 
                            return! loop isConnected
                    }
            
            loop true // Start the loop with whatever initial value
        )

    let private monitorConnectivity (token : CancellationToken) =  

        actor.Post(UpdateState true) //inicializace

        AsyncSeq.initInfinite (fun _ -> true)
        |> AsyncSeq.mapi (fun index _ -> index) 
        |> AsyncSeq.takeWhile ((=) true << fun index -> index >= 0) // indefinite sequence
        |> AsyncSeq.iterAsync 
            (fun index 
                ->        
                async 
                    {                                 
                        connectivityListener2 
                            (fun isConnected 
                                ->
                                async
                                    {
                                        match isConnected with
                                        | true  ->
                                                ()
                                        | false ->
                                                do! Async.Sleep 120000

                                                match isConnected with
                                                | true  -> ()
                                                | false -> actor.Post(UpdateState false)
                                    }    
                                |> Async.StartImmediate  
                            ) 
                                
                        do! Async.Sleep 20    
                    }
            )
        |> Async.StartImmediate  

    let private tokenTrigger () = 
                  
        let token2 () =
           
            let defaultToken = CancellationToken.None
        
            try
                // Create a new CancellationTokenSource
                match new CancellationTokenSource() |> Option.ofNull with
                | Some newCts ->
                    try
                        // Try to get the token
                        let newToken =
                            try
                                Some newCts.Token
                            with
                            | _ -> None
        
                        match newToken with
                        | Some newToken 
                            ->
                            newCts.Cancel() // This signal is irreversible once sent.
                            newToken

                        | None 
                            ->
                            newCts.Dispose()
                            defaultToken
                    finally
                        newCts.Dispose()
                | None
                    ->
                    defaultToken
            with
            | _ ->
                defaultToken               

        actor.PostAndAsyncReply (fun replyChannel -> CheckState replyChannel)
        |> Async.RunSynchronously
        |> function    
            | true  -> CancellationToken.None   
            | false -> token2 () 
    
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
                                monitorConnectivity (token : CancellationToken)
                                
                                context.list
                                |> List.unzip             
                                ||> context.listMappingFunction
                                    (fun uri (pathToFile: string) 
                                        -> 
                                        let token2 = tokenTrigger ()
                                       
                                        async
                                            {    
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
                                                        (* 
                                                        let get uri = 

                                                            let headerContent1 = "Range" 
                                                            let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                        
                                                            match existingFileLength > 0L with
                                                            | true  -> 
                                                                    http
                                                                        {
                                                                            GET uri
                                                                            config_timeoutInSeconds 5
                                                                            config_cancellationToken token2
                                                                            header headerContent1 headerContent2
                                                                        }
                                                            | false ->
                                                                    http
                                                                        {
                                                                            GET uri
                                                                            config_timeoutInSeconds 5
                                                                            config_cancellationToken token2
                                                                        }

                                                        use! response = get >> Request.sendAsync <| uri  
                                                        *)
                                                        
                                                        let! response =
                                                            Async.StartChild(
                                                                async {
                                                                        let get uri = 

                                                                            let headerContent1 = "Range" 
                                                                            let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                        
                                                                            match existingFileLength > 0L with
                                                                            | true  -> 
                                                                                    http
                                                                                        {
                                                                                            GET uri
                                                                                            //config_timeoutInSeconds 120
                                                                                            config_cancellationToken token2
                                                                                            header headerContent1 headerContent2
                                                                                        }
                                                                            | false ->
                                                                                    http
                                                                                        {
                                                                                            GET uri
                                                                                            //config_timeoutInSeconds 120
                                                                                            config_cancellationToken token2
                                                                                        }

                                                                        return! (get >> Request.sendAsync <| uri) 
                                                                }, 120 * 1000
                                                            )
                                                        
                                                        let! response = response
                                                    
                                                        match response.statusCode with
                                                        | HttpStatusCode.PartialContent // 206
                                                        | HttpStatusCode.OK  // 200
                                                            ->         
                                                            do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                        | _ ->
                                                            failwith "FileDownloadError"
                                                    | None 
                                                        ->
                                                        failwith "FileDeleteError"                   
                                                    
                                                with
                                                | :? TimeoutException 
                                                    -> 
                                                    failwith "Timeout" 

                                                | :? System.Net.Http.HttpRequestException as ex when ex.Message.Contains("timeout") 
                                                    -> 
                                                    failwith "Timeout" 

                                                | :? OperationCanceledException 
                                                    when 
                                                        token.IsCancellationRequested 
                                                            -> 
                                                            failwith "Timeout" 
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
                             
                            with
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