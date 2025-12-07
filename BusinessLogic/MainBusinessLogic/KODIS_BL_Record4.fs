namespace BusinessLogic4

open System
open System.IO
open System.Net
open System.Threading

//*******************

open FsHttp
open FSharp.Control
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes

//*******************

open Api.Logging
open Api.FutureLinks

open Helpers
open Helpers.Builders
open Helpers.DirFileHelper

open Settings.SettingsGeneral
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks
open Types.Haskell_IO_Monad_Simulation

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 Nic neni trvalejsiho, nez neco docasneho ...
            
    let internal operationOnDataFromJson token variant dir = 
    
        IO (fun () 
                ->
                let result1 : Async<Result<(string * string) list, PdfDownloadErrors>> = 
                    async
                        {
                            match! getFutureLinksFromRestApi >> runIO <| urlApi with
                            | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                            | Error err -> return Error err
                        }
    
                let result2 : Async<Result<(string * string) list, PdfDownloadErrors>> = 
                    async
                        {
                            match variant with
                            | FutureValidity 
                                ->
                                match! getFutureLinksFromRestApi >> runIO <| urlJson with
                                | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                                | Error err -> return Error err
                            | _              
                                -> return Ok []
                        }
       
                async 
                    {
                        let! results = 
                            [ 
                                result1
                                result2 
                            ]
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
                                    runIO (postToLog <| err <| "#013")                                     
                                    Error err

                                | [ _; Error err ] 
                                    ->                                    
                                    runIO (postToLog <| err <| "#014")   
                                    Error err

                                | _                   
                                    ->
                                    runIO (postToLog <| DataFilteringError <| "#015")                                    
                                    Error DataFilteringError

                        | Choice2Of2 ex
                            -> 
                            runIO (postToLog <| ex.Message <| "#016")                      
                            return Error DataFilteringError  
                    }
                |> Async.RunSynchronously //priprava na pripadne pouziti cancellation token, zabal to pak to try-with
                                          //|> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token) 
        )
                    
    let internal downloadAndSave token = 

        IO (fun () 
                ->   
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

                                    //mel jsem 2x stejnou linku s jinym jsGeneratedString, takze uri bylo unikatni, ale cesta k souboru 2x stejna
                                    let removeDuplicatePathPairs uri pathToFile =
                                        (uri, pathToFile)
                                        ||> List.zip 
                                        |> List.distinctBy snd
                            
                                    let uri, pathToFile =
                                        context.list
                                        |> List.distinct
                                        |> List.unzip
                                        |> fun (uri, pathToFile) -> removeDuplicatePathPairs uri pathToFile 
                                        |> List.unzip
                            
                                    (token, uri, pathToFile)     
                                    |||> List.Parallel.map2_IO_Token //context.listMappingFunction    
                                        (fun uri (pathToFile : string) 
                                            -> 
                                            //let token2 = tokenTrigger ()  //zatim nepotrebne
                                   
                                            async
                                                {    
                                                    counterAndProgressBar.Post <| Inc 1

                                                    token.ThrowIfCancellationRequested ()                                            
                                            
                                                    let pathToFileExistFirstCheck = 
                                                        runIO <| checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                        in
                                                        match pathToFileExistFirstCheck with  //tady nelze |> function - smesuje to async a pyramidOfDoom computation expressions
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
                                                                                //config_timeoutInSeconds 30 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                                config_cancellationToken token 
                                                                                header "User-Agent" "FsHttp/Android7.1"
                                                                                header headerContent1 headerContent2
                                                                            }
                                                                | false ->
                                                                        http
                                                                            {
                                                                                GET uri
                                                                                //config_timeoutInSeconds 30 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                                config_cancellationToken token 
                                                                                header "User-Agent" "FsHttp/Android7.1"
                                                                            }

                                                            use! response = get >> Request.sendAsync <| uri  

                                                            let statusCode = response.statusCode

                                                            match statusCode with
                                                            | HttpStatusCode.PartialContent | HttpStatusCode.OK  // 206 // 200
                                                                ->  
                                                                do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                            | HttpStatusCode.Forbidden 
                                                                ->
                                                                runIO <| postToLogFile () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#1711") 
                                                                |> Async.Ignore<ResponsePost>
                                                                |> Async.StartImmediate 
                                                            | _ ->
                                                                runIO (postToLog <| statusCode <| "#1712")

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
                                                runIO (postToLog <| ex.Message <| "#74764-171")
                                                None

                                            | Error ex
                                                when (string ex.Message).Contains "The operation was canceled" 
                                                ->
                                                Some <| Error StopDownloading

                                            | Error ex 
                                                ->
                                                runIO (postToLog <| ex.Message <| "#1722")
                                                Some <| Error FileDownloadError
                                        )
                                    |> Option.defaultValue (Ok ())
                                with
                                | ex 
                                    ->
                                    match (string ex.Message).Contains "The operation was canceled" with 
                                    | true  
                                        ->
                                        Error StopDownloading
                                    | false
                                        ->
                                        runIO (postToLog <| ex.Message <| "#171")
                                        Error FileDownloadError  
                        } 
        
                reader
                    {    
                        let! context = fun env -> env
            
                        return
                            match context.dir |> Directory.Exists with 
                            | false ->
                                    runIO (postToLog <| NoFolderError <| "#181")
                                    Error NoFolderError    
                            
                            | true  ->
                                    try                                        
                                        match context.list with
                                        | []
                                            -> 
                                             Ok String.Empty 

                                        | _ 
                                            ->       
                                            match downloadAndSaveTimetables token context with
                                            | Ok _ 
                                                -> 
                                                Ok String.Empty
                                            
                                            | Error StopDownloading 
                                                -> 
                                                Ok String.Empty
                                            
                                            | Error err 
                                                ->
                                                runIO (postToLog <| err <| "#018")
                                            
                                                match runIO <| deleteAllODISDirectories kodisPathTemp4 with
                                                | Ok _ 
                                                    ->
                                                    runIO (postToLog <| err <| "#182")
                                                    Error err

                                                | Error _
                                                    ->
                                                    runIO (postToLog <| FileDeleteError <| "#183")
                                                    Error FileDeleteError
                                            
                                    with
                                    | ex 
                                        ->
                                        pyramidOfInferno
                                            {                                                                                
                                                let! _ =
                                                    (not <| (string ex.Message).Contains "The operation was canceled") |> Result.fromBool () String.Empty,
                                                        fun _ -> Ok String.Empty
                                        
                                                runIO (postToLog <| ex.Message <| "#019")
                                        
                                                let!_ =
                                                    runIO <| deleteAllODISDirectories kodisPathTemp4, 
                                                        (fun _
                                                            ->
                                                            runIO (postToLog <| FileDeleteError <| "#192")                             
                                                            Error FileDeleteError
                                                        )

                                                runIO (postToLog <| FileDownloadError <| "#191") 

                                                return Error FileDownloadError                                            
                                            }
                    }              
        )