namespace ApplicationDesign

open System
open System.IO
open System.Threading

//**********************************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

open FsToolkit.ErrorHandling

open BusinessLogic.KODIS_BL_Record
open BusinessLogic.TP_Canopy_Difference

open Helpers
open Helpers.Builders

open Api.Logging

open IO_Operations
open IO_Operations.IO_Operations
open IO_Operations.CreatingPathsAndNames

open JsonData.ParseJsonData  
open Filtering.FilterTimetableLinks  

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral

open BusinessLogic.KODIS_BL_Record

//Vzhledem k pouziti Elmishe priste podumej nad timto designem, mozna bude lepsi pure transformation layer

module WebScraping_KODISFMRecord = 

    type private State =  
        { 
            TimetablesDownloadedAndSaved : int
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = 0
        }

    type private Context2 = 
        {
            DirList : string list 
            Variant : Validity
            Msg1 : string
            Msg2 : string
            Msg3 : string
            VariantInt : int
        }

    type private Environment = 
        {
            DownloadAndSaveJson : string list -> string list -> CancellationToken -> (float * float -> unit) -> IO<Result<unit, JsonDownloadErrors>>
            DeleteAllODISDirectories : string -> IO<Result<unit, JsonParsingAndPdfDownloadErrors>>
            ParseJsonStructure : (float * float -> unit) -> CancellationToken -> IO<Lazy<Result<string list, JsonParsingAndPdfDownloadErrors>>> 
            FilterTimetableLinks : Validity -> string -> Result<string list, JsonParsingAndPdfDownloadErrors> -> IO<Result<(string * string) list, JsonParsingAndPdfDownloadErrors>> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<unit, exn>> -> Result<string, JsonParsingAndPdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            DownloadAndSaveJson = downloadAndSaveJson 
            DeleteAllODISDirectories = deleteAllODISDirectories   
            ParseJsonStructure = parseJsonStructure  
            FilterTimetableLinks = filterTimetableLinks  
            DownloadAndSave = downloadAndSave >> runIO
        }    

    let internal stateReducerCmd1 (token : CancellationToken) reportProgress =

        let configKodis =
            {
                source1 = path0 <| ODIS_Variants.board.board I1 I1
                source2 = path0 <| ODIS_Variants.board.board I1 I2
                source3 = path0 <| ODIS_Variants.board.board I2 I1 
                destination = oldTimetablesPath 
            }          
    
        IO (fun () 
                ->       
                let downloadAndSaveJson reportProgress (token : CancellationToken) = 

                    let errFn err =                     
                        match err with
                        | JsonDownloadError    -> jsonDownloadError
                        | JsonConnectionError  -> cancelMsg2
                        | NetConnJsonError err -> err
                        | JsonTimeoutError     -> jsonDownloadError  
                        | StopJsonDownloading  -> jsonCancel
                        | FolderMovingError    -> folderMovingError
                        | LetItBeKodis         -> String.Empty
                    
                    try
                        try
                            let downloadTask () = 
                                async
                                    {
                                        //return! runIOAsync <| environment.DownloadAndSaveJson (jsonLinkList1 @ jsonLinkList3) (pathToJsonList1 @ pathToJsonList3) reportProgress
                                        return! runIOAsync <| environment.DownloadAndSaveJson jsonLinkList3 pathToJsonList3 token reportProgress 
                                    }
                            
                            let moveAllTask () = //staci jako celek, pri stahovani json souboru je casu dost
                                async 
                                    {
                                        // Kdyz se move nepovede, tak se vubec nic nedeje, proste nebudou starsi soubory,
                                        // nicmene priprava na zpracovani err je provedena (jeste vytvorit list s Result, ten bude v Array.last)
                                        let!_ = runIOAsync <| moveFolders configKodis.source1 configKodis.destination LetItBeKodis FolderMovingError
                                        let!_ = runIOAsync <| moveFolders configKodis.source2 configKodis.destination LetItBeKodis FolderMovingError
                                        let!_ = runIOAsync <| moveFolders configKodis.source3 configKodis.destination LetItBeKodis FolderMovingError

                                        return Ok ()  //silently ignoring failed move operations
                                    }
                            
                            [ 
                                downloadTask ()
                                moveAllTask ()
                            ]
                            |> Async.Parallel  //uz to mame v try with bloku, Async.Catch only if you don’t want one task to cancel all others on failure
                            |> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token)  
                            |> Array.head
                          
                        finally
                            ()              
                    with
                    | ex
                        -> 
                        runIO (postToLog <| ex.Message <| "#005") 
                        Error JsonDownloadError 
            
                    |> Result.map (fun _ -> dispatchMsg1) 
                    |> Result.mapError errFn

                downloadAndSaveJson reportProgress token 
        )

    let internal stateReducerCmd2 (token : CancellationToken) path dispatchWorkIsComplete dispatchIterationMessage reportProgress =

        IO (fun () 
                ->    
                let errFn err =  

                    match err with
                    | PdfError RcError                 -> rcError
                    | PdfError NoFolderError           -> noFolderError
                    | PdfError FileDeleteError         -> fileDeleteError 
                    | PdfError CreateFolderError4      -> createFolderError   
                    | PdfError CreateFolderError2      -> createFolderError2
                    | PdfError FileDownloadError       -> (environment.DeleteAllODISDirectories >> runIO) path |> function Ok _ -> dispatchMsg4 | Error _ -> dispatchMsg0
                    | PdfError FolderMovingError4      -> folderMovingError 
                    | PdfError CanopyError             -> canopyError
                    | PdfError TimeoutError            -> "timeout"
                    | PdfError PdfConnectionError      -> cancelMsg2 
                    | PdfError (ApiResponseError err)  -> err
                    | PdfError ApiDecodingError        -> canopyError
                    | PdfError (NetConnPdfError err)   -> err
                    | PdfError StopDownloading         -> (environment.DeleteAllODISDirectories >> runIO) path |> function Ok _ -> cancelMsg4 | Error _ -> cancelMsg5
                    | PdfError LetItBeKodis4           -> String.Empty
                    | PdfError NoPermissionError       -> String.Empty
                    | JsonError JsonParsingError       -> jsonParsingError 
                    | JsonError StopJsonParsing        -> (environment.DeleteAllODISDirectories >> runIO) path |> function Ok _ -> cancelMsg4 | Error _ -> cancelMsg5
                    | JsonError JsonDataFilteringError -> dataFilteringError 
                                                                 
                let result (lazyList : Lazy<Result<string list, JsonParsingAndPdfDownloadErrors>>) (context2 : Context2) =  
                   
                    dispatchWorkIsComplete dispatchMsg2

                    let dir = context2.DirList |> List.item context2.VariantInt 
                        
                    let list = 
                        try  
                            runIO <| filterTimetableLinks context2.Variant dir lazyList.Value //lazyList.Value vraci Result<string list, PdfDownloadErrors>                                       
                        with
                        | ex
                            ->
                            runIO (postToLog <| ex.Message <| "#22-2")
                            Error <| JsonError JsonDataFilteringError 
                                
                    match list with
                    | Ok list
                        when
                            list <> List.empty
                                -> 
                                let context listMappingFunction : Context<'a, 'b, 'c> = 
                                    {
                                        listMappingFunction = listMappingFunction //nepotrebne, ale ponechano jako template record s generic types
                                        reportProgress = reportProgress
                                        dir = dir
                                        list = list
                                    } 
                               
                                dispatchIterationMessage context2.Msg1
                              
                                //nepotrebne, ale ponechano jako template record s generic types (mrkni se na function signature)
                                //**********************************************************************
                                match list.Length >= 4 with //muj odhad, kdy uz je treba multithreading
                                | true  -> context List.Parallel.map2_IO
                                | false -> context List.map2  
                                //**********************************************************************
                                 
                                |> environment.DownloadAndSave token   
        
                    | Ok _
                        ->  
                        dispatchIterationMessage context2.Msg2    
                        System.Threading.Thread.Sleep(6000)     
                        Ok context2.Msg3 
        
                    | Error err 
                        when err <> JsonError StopJsonParsing || err <> PdfError StopDownloading
                        ->
                        Error err  

                    | Error err                    
                        ->
                        runIO (postToLog <| err <| "#006")
                        Error err 
                       
                let dirList = createNewDirectoryPaths path listOfODISVariants
                    in
                    let contextCurrentValidity = 
                        {
                            DirList = dirList
                            Variant = CurrentValidity
                            Msg1 = msg1CurrentValidity
                            Msg2 = msg2CurrentValidity
                            Msg3 = msg3CurrentValidity
                            VariantInt = 0
                        }
        
                    let contextFutureValidity = 
                        {
                            DirList = dirList
                            Variant = FutureValidity
                            Msg1 = msg1FutureValidity
                            Msg2 = msg2FutureValidity
                            Msg3 = msg3FutureValidity
                            VariantInt = 1
                        }
        
                    let contextWithoutReplacementService = 
                        {
                            DirList = dirList
                            Variant = WithoutReplacementService
                            Msg1 = msg1WithoutReplacementService
                            Msg2 = msg2WithoutReplacementService
                            Msg3 = msg3WithoutReplacementService
                            VariantInt = 2
                        }
                                                           
                pyramidOfInferno
                    {       
                        #if ANDROID
                        let!_ = runIO <| createTP_Canopy_Folder logDirTP_Canopy, errFn 
                        #endif
                        let!_ = runIO <| environment.DeleteAllODISDirectories path, errFn  
                        let!_ = runIO <| createFolders dirList, errFn 

                        let lazyList = 
                            //laziness jen jako priprava pro pripadne threadsafe multitasking, zatim zadny rozdil oproti eager + parameterless (krome trochu vetsiho overhead u lazy)
                            try               
                                runIO <| environment.ParseJsonStructure reportProgress token  //TODO pri tvorbe profi UI/UX toto dej jako stateReducerCmd2, ostatni jako stateReducerCmd3
                            with
                            | ex
                                ->
                                runIO (postToLog <| ex.Message <| "#22-1")
                                Error <| JsonError JsonParsingError
                                |> Lazy<Result<string list, JsonParsingAndPdfDownloadErrors>>                       

                        let! msg1 = result lazyList contextCurrentValidity, errFn
                        let! msg2 = result lazyList contextFutureValidity, errFn
                        let! msg3 = result lazyList contextWithoutReplacementService, errFn   

                        let msg4 = 
                            match calculate_TP_Canopy_Difference >> runIO <| () with
                            | Ok _      -> String.Empty
                            | Error err -> err      
                                
                        #if ANDROID     
                        deleteAllJsonFilesInDirectory >> runIO <| partialPathJsonTemp 
                        #endif

                        let separator = String.Empty
                            
                        let combinedMessage = 
                            [ msg1; msg2; msg3; msg4 ] 
                            |> List.choose Option.ofNullEmptySpace
                            |> List.map (fun msg -> sprintf "\n%s" msg)
                            |> String.concat separator   
        
                        return sprintf "%s%s" dispatchMsg3 combinedMessage
                    }
        )
     