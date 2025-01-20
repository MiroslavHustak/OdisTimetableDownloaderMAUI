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

open Types
open Types.Types
open Types.ErrorTypes

//*******************

open Helpers
open Helpers.Builders
open Helpers.Connectivity
open Helpers.FileInfoHelper

#if ANDROID
open Helpers.AndroidDownloadService
#endif 

open Settings.Messages
open Settings.SettingsGeneral

open Api.CallApi
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 nic neni trvalejsiho, nez neco docasneho ...

    //*************************** Cancellation token templates ********************************
    
    let private cancellationActor = //Template 004a for cancellation tokens (actor)

        MailboxProcessor<ConnectivityMessage>
            .StartImmediate
                (fun inbox
                    ->
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

        cancellationActor.Post <| UpdateState true //inicializace

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
                                        | false -> () //cancellationActor.Post <| UpdateState false
                                    }    
                                |> Async.StartImmediate  
                            ) 
                                
                        do! Async.Sleep 600000 //zatim nepotrebujeme, token sice funguje, RunSynchronously ale umozni cancel az po pripojeni k netu     
                    }
            )
        |> Async.StartImmediate 

    let private tokenTrigger () = //Template //zatim nepouzivano
                  
        let token2 () = //Template //zatim nepouzivano
           
            let defaultToken = CancellationToken.None
                in
                try
                    match new CancellationTokenSource() |> Option.ofNull with
                    | Some newCts 
                        ->
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
        
        //Template //zatim nepouzivano
        cancellationActor.PostAndAsyncReply (fun replyChannel -> CheckState replyChannel)
        |> Async.RunSynchronously
        |> function    
            | true  -> CancellationToken.None   
            | false -> token2 () 

    //************************ Main code *********************************
        
    let internal operationOnDataFromJson token variant dir =  

        try   
            let url1 = "http://kodis.somee.com/api/"             // nezapomen na trailing slash po api 
            let url2 = "http://kodis.somee.com/api/jsonLinks"     

            let result1 : Result<(string * string) list, PdfDownloadErrors> = 
                getFromRestApi >> filterTimetableLinks variant dir <| url1

            let result2 : Result<(string * string) list, PdfDownloadErrors> =
                match variant with
                | FutureValidity -> getFromRestApi >> filterTimetableLinks variant dir <| url2 // links filtered from json files, only future validity
                | _              -> Ok []
            
            (*
            match result1, result2 with
            | Ok list1, Ok list2 -> Ok (list1 @ list2) |> List.distinct
            | Error err, _       -> Error err               
            | _, Error err       -> Error err 
            *)
                      
            Result.bind 
                (fun list1 ->
                           result2 
                           |> Result.map 
                               (fun list2 -> (list1 @ list2) |> List.distinct) 
                ) result1 
            
        with
        | _ 
            ->
            //TODO logfile                 
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

                                            counterAndProgressBar.Post <| Inc 1

                                            token.ThrowIfCancellationRequested ()
                                                                                      
                                            ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 //quli Android 7.1

                                            let pathToFileExistFirstCheck = 
                                                checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                in
                                                match pathToFileExistFirstCheck with  //tady nelze |> function - smesuje to async a pyramidOfDoom computation expressions
                                                | Some _
                                                    -> 
                                                    (*
                                                    //!!! vytvorit adresar pod downloads (bez /storage/emulated/0), napr. /FabulousTimetables4/
                                                    #if ANDROID 
                                                        
                                                    let result = downloadManager uri pathToFile 

                                                    let downloadId =
                                                        result
                                                        |> function Some downloadId -> downloadId | None -> failwith "FileDownloadError"                                                            
                                                        
                                                    downloadId |> ignore 

                                                    #else 
                                                    *)

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
                                                                        //config_cancellationToken CancellationToken.None //token2  //funguje
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri
                                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        //config_cancellationToken CancellationToken.None //token2  //funguje
                                                                    }

                                                    use! response = get >> Request.sendAsync <| uri  

                                                    match response.statusCode with
                                                    | HttpStatusCode.PartialContent | HttpStatusCode.OK  // 206 // 200
                                                        ->         
                                                        do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                    | _ ->
                                                        failwith String.Empty  

                                                    // #endif  

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
                        | _ 
                            -> Error FileDownloadError  //TODO logfile   
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