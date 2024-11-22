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
open Helpers.FileInfoHelper

open Settings.Messages
open Settings.SettingsGeneral

open Api.CallApi
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  

    //*************************** Cancellation tokens ********************************
    
    // For educational purposes only. 

    let private cancellationActor = //Template 004a for cancellation tokens (actor)

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

    let private monitorConnectivity (token : CancellationToken) =  //zatim nepouzivano

        cancellationActor.Post(UpdateState true) //inicializace

        AsyncSeq.initInfinite (fun _ -> true)
        |> AsyncSeq.mapi (fun index _ -> index) 
        |> AsyncSeq.takeWhile ((=) true << fun index -> index >= 0) //indefinite sequence
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
                                        | true  -> ()
                                        | false -> () //cancellationActor.Post(UpdateState false)
                                    }    
                                |> Async.StartImmediate  
                            ) 
                                
                        do! Async.Sleep 600000 //zatim nepotrebujeme, token sice funguje, RunSynchronously ale umozni cancel az po pripojeni k netu     
                    }
            )
        |> Async.StartImmediate 

    let private tokenTrigger () = //Template 004b for cancellation tokens (tokenTrigger)
                  
        let token2 () = //Template 003 for cancellation tokens 
           
            let defaultToken = CancellationToken.None
        
            try
                // Create a new CancellationTokenSource
                match new CancellationTokenSource() |> Option.ofNull with
                | Some newCts ->
                    try
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

        cancellationActor.PostAndAsyncReply (fun replyChannel -> CheckState replyChannel)
        |> Async.RunSynchronously
        |> function    
            | true  -> CancellationToken.None   
            | false -> token2 () 

    //************************ Main code *********************************
        
    let internal operationOnDataFromJson token variant dir =  //v parametru je token jen quli educational code v App.fs

        try          
            getFromRestApi >> filterTimetableLinks variant dir <| ()
        with
        | ex 
            ->
            string ex.Message |> ignore //TODO logfile                 
            Error DataFilteringError 
                    
    let internal downloadAndSave token = //v parametru je token jen quli educational code v App.fs
        
        let downloadAndSaveTimetables (token : CancellationToken) =  //v parametru je token jen quli educational code v App.fs
            
            reader
                {                         
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