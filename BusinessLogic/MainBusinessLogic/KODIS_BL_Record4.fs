﻿namespace BusinessLogic4

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

open Api.Logging
open Api.FutureLinks

open Helpers
open Helpers.Builders
open Helpers.Connectivity
open Helpers.FileInfoHelper

open Settings.Messages
open Settings.SettingsGeneral

open Api.FutureLinks
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 Nic neni trvalejsiho, nez neco docasneho ...

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
                                
                        do! Async.Sleep 600000 //zatim nepotrebujeme
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
                | ex
                    ->
                    postToLog <| ex.Message <| "#12"
                    defaultToken               
        
        //Template //zatim nepouzivano
        cancellationActor.PostAndReply (fun replyChannel -> CheckState replyChannel)
        |> function    
            | true  -> CancellationToken.None   
            | false -> token2 () 

    //************************ Main code *********************************
        
    let internal operationOnDataFromJson token variant dir =  

        let url1 = "http://kodis.somee.com/api/"  // Trailing slash preserved
        let url2 = "http://kodis.somee.com/api/jsonLinks"
    
        let result1 : Async<Result<(string * string) list, PdfDownloadErrors>> = 
            async
                {
                    let! getFromRestApi = getFutureLinksFromRestApi url1
                    return filterTimetableLinks variant dir getFromRestApi
                }
    
        let result2 : Async<Result<(string * string) list, PdfDownloadErrors>> = 
            async
                {
                    let! getFromRestApi = getFutureLinksFromRestApi url2
                    match variant with
                    | FutureValidity -> return filterTimetableLinks variant dir getFromRestApi
                    | _              -> return Ok []
                }
       
        async 
            {
                let! results = 
                    [ result1; result2 ]
                    |> Async.Parallel
                    |> Async.Catch
    
                match results with
                | Choice1Of2 resultsArray 
                    ->
                    return
                        match List.ofArray resultsArray with
                        | [ Ok list1; Ok list2 ]
                            -> 
                            Ok (List.distinct (list1 @ list2))

                        | [ Error err; _ ]    
                            -> 
                            postToLog <| err <| "#13"
                            Error err

                        | [ _; Error err ] 
                            ->
                            postToLog <| err <| "#14"
                            Error err

                        | _                   
                            ->
                            postToLog <| DataFilteringError <| "#15"
                            Error DataFilteringError

                | Choice2Of2 ex
                    -> 
                    postToLog <| ex.Message <| "#16"
                    
                    return Error DataFilteringError  
            }
        |> Async.RunSynchronously

        //priprava na pripadne pouziti cancellation token, zabal to pak to try-with
        //|> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token) 
                    
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
                                (fun uri (pathToFile : string) 
                                    -> 
                                    //let token2 = tokenTrigger ()  //zatim nepotrebne
                                   
                                    async
                                        {    
                                            counterAndProgressBar.Post <| Inc 1

                                            token.ThrowIfCancellationRequested ()                                            

                                            let pathToFileExistFirstCheck = 
                                                checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                in
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
                                                                        config_cancellationToken token //CancellationToken.None //token2  //funguje
                                                                        header headerContent1 headerContent2
                                                                    }
                                                        | false ->
                                                                http
                                                                    {
                                                                        GET uri
                                                                        config_timeoutInSeconds 300 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                        config_cancellationToken token //CancellationToken.None //token2  //funguje
                                                                    }

                                                    use! response = get >> Request.sendAsync <| uri  

                                                    let statusCode = response.statusCode

                                                    match statusCode with
                                                    | HttpStatusCode.PartialContent | HttpStatusCode.OK  // 206 // 200
                                                        ->  
                                                        do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile

                                                    | HttpStatusCode.Forbidden 
                                                        ->
                                                        postToLogFile () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#1711") 
                                                        |> Async.RunSynchronously 
                                                        |> ignore<ResponsePost> 
                                                    | _ ->
                                                        postToLog <| statusCode <| "#1712"
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
                                        ->
                                        postToLog <| ex.Message <| "#171"
                                                                               
                                        match (string ex.Message).Contains "OperationCanceled" with 
                                        | true  -> Some <| Error StopDownloading
                                        | false -> Some <| Error FileDownloadError     //melo by byt pouze pro Async.RunSynchronously(workflow, cancellationToken = token)                                                  
                                )
                            |> Option.defaultValue (Ok ())
                        with
                        | ex 
                            ->
                            postToLog <| ex.Message <| "#17"
                                                          
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
                            postToLog <| NoFolderError <| "#181"
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
                                         postToLog <| err <| "#18"

                                         let pathToDir = kodisPathTemp4                   
                                             in                                        
                                             match deleteAllODISDirectories pathToDir with
                                             | Ok _    
                                                 -> 
                                                 postToLog <| err <| "#182"
                                                 Error err              
                                             | Error _ 
                                                 ->
                                                 postToLog <| FileDeleteError <| "#183"
                                                 Error FileDeleteError                                            
                            with
                            | ex 
                                ->
                                postToLog <| ex.Message <| "#19"
                               
                                let pathToDir = kodisPathTemp4                   
                                    in  
                                    match deleteAllODISDirectories pathToDir with
                                    | Ok _   
                                        -> 
                                        postToLog <| FileDownloadError <| "#191"
                                        Error FileDownloadError 
                                    | Error _ 
                                        -> 
                                        postToLog <| FileDeleteError <| "#192"
                                        Error FileDeleteError  
            }