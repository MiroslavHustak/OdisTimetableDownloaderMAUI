namespace ApplicationDesign4

open System
open System.Threading

//**********************************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open BusinessLogic4.KODIS_BL_Record4

open Helpers
open Helpers.Builders

open Api.Logging

open IO_Operations
open IO_Operations.IO_Operations
open IO_Operations.CreatingPathsAndNames

open Settings.Messages
open Settings.SettingsGeneral



open System
open System.IO
open System.Threading

//**********************************

open Types.Types   
open Types.FreeMonad
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.Builders
open Helpers.CopyOrMoveDirectories

open Api.Logging
open BusinessLogic.DPO_BL   
open IO_Operations.IO_Operations

open Settings.Messages
open Settings.SettingsGeneral 


// 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
// 28-12-2024 Nic neni trvalejsiho, nez neco docasneho...

//Vzhledem k pouziti Elmishe priste podumej nad timto designem, mozna bude lepsi pure transformation layer

module WebScraping_KODISFMRecord4 = 

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
            DeleteAllODISDirectories : string -> IO<Result<unit, PdfDownloadErrors>>
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> IO<Result<(string * string) list, PdfDownloadErrors>> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<unit, exn>> -> Result<string, PdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            DeleteAllODISDirectories = deleteAllODISDirectories   
            OperationOnDataFromJson = operationOnDataFromJson
            DownloadAndSave = downloadAndSave >> runIO
        }    

    let private stateReducer (token : CancellationToken) path dispatchWorkIsComplete dispatchIterationMessage reportProgress (state : State) (environment : Environment) =
              
        let errFn err =  

            match err with
            | RcError              -> rcError
            | NoFolderError        -> noFolderError
            | JsonFilteringError   -> jsonFilteringError
            | DataFilteringError   -> dataFilteringError
            | FileDeleteError      -> fileDeleteError 
            | CreateFolderError    -> createFolderError
            | CreateFolderError2   -> createFolderError2
            | FileDownloadError    -> match runIO <| environment.DeleteAllODISDirectories path with Ok _ -> dispatchMsg4 | Error _ -> dispatchMsg0
            | FolderMovingError    -> folderMovingError 
            | CanopyError          -> canopyError
            | TimeoutError         -> "timeout"
            | PdfConnectionError   -> cancelMsg2 
            | ApiResponseError err -> err
            | ApiDecodingError     -> canopyError
            | NetConnPdfError err  -> err
            | StopDownloading      -> match runIO <| environment.DeleteAllODISDirectories path with Ok _ -> cancelMsg4 | Error _ -> cancelMsg5
            | LetItBeKodis         -> String.Empty

        let result (context2 : Context2) =   

            dispatchWorkIsComplete dispatchMsg2
                     
            let dir = context2.DirList |> List.item context2.VariantInt  
            let list = runIO <| operationOnDataFromJson token context2.Variant dir 

            match list with
            | Ok list
                when
                    list <> List.empty
                        -> 
                        let context listMappingFunction = 
                            {
                                listMappingFunction = listMappingFunction //nepotrebne, ale ponechano jako template record s generic types (mrkni se na function signature)
                                reportProgress = reportProgress
                                dir = dir
                                list = list
                            }
                        
                        dispatchIterationMessage context2.Msg1

                        //nepotrebne, ale ponechano jako template record s generic types (mrkni se na function signature)                      
                        match list.Length >= 4 with //muj odhad, kdy uz je treba multithreading
                        | true  -> context List.Parallel.map2_IO
                        | false -> context List.map2

                        |> environment.DownloadAndSave token     

            | Ok _
                ->   
                dispatchIterationMessage context2.Msg2
                System.Threading.Thread.Sleep(6000) 
                Ok context2.Msg3 

            | Error err 
                ->
                 runIO (postToLog <| err <| "#4")
                 Error err  
            
        //try with blok zrusen
                 
        let dirList = createNewDirectoryPaths path listODISDefault4
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

        let configKodis =
            {
                source1 = path4 ODISDefault.OdisDir1 //@"g:\Users\User\Data4\JR_ODIS_aktualni_vcetne_vyluk\"
                source2 = path4 ODISDefault.OdisDir2 //@"g:\Users\User\Data4\JR_ODIS_pouze_budouci_platnost\"
                source3 = path4 ODISDefault.OdisDir4 //@"g:\Users\User\Data4\JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk\"
                destination = oldTimetablesPath4 //@"g:\Users\User\DataOld4\"
            }  

        let moveFolders source destination = 

            IO (fun () 
                    ->
                    pyramidOfInferno
                        {
                            let! _ = 
                                Directory.Exists source |> Result.fromBool () LetItBeKodis,
                                    fun _ -> Ok ()   
                    
                            let! _ =
                                Directory.Exists destination |> Result.fromBool () FolderMovingError,
                                    fun err 
                                        ->
                                        try
                                            pyramidOfInferno 
                                                {
                                                    let! _ =    
                                                        let dirInfo = Directory.CreateDirectory destination
                                                        Thread.Sleep 300 //wait for the directory to be created  

                                                        dirInfo.Exists |> Result.fromBool () FolderMovingError,
                                                            fun err
                                                                ->
                                                                runIO (postToLog <| err <| "#444-1")
                                                                Error FolderMovingError
                                                    let! _ =
                                                        runFreeMonad
                                                        <|
                                                        copyOrMoveFiles { source = source; destination = destination } Move,
                                                            fun err 
                                                                ->
                                                                runIO (postToLog <| err <| "#444-2")
                                                                Error FolderMovingError
                            
                                                    return Ok ()
                                                }                                 
                                        with 
                                        | ex 
                                            ->
                                            runIO (postToLog <| ex.Message <| "#444-3")
                                            Error err                       
           
                            let! _ = 
                                runFreeMonad 
                                <| 
                                copyOrMoveFiles { source = source; destination = destination } Move,   
                                    fun err
                                        ->
                                        runIO (postToLog <| err <| "#444-4")
                                        Error FolderMovingError
                         
                            return Ok ()
                         } 
            )
                                                   
        pyramidOfInferno
            {             
                #if ANDROID
                let!_ = runIO <| createTP_Canopy_Folder logDirTP_Canopy, errFn 
                #endif

                let!_ = runIO <| moveFolders configKodis.source1 configKodis.destination, errFn 
                let!_ = runIO <| moveFolders configKodis.source2 configKodis.destination, errFn
                let!_ = runIO <| moveFolders configKodis.source3 configKodis.destination, errFn

                let!_ = runIO <| environment.DeleteAllODISDirectories path, errFn  
                let!_ = runIO <| createFolders dirList, errFn 

                let! msg1 = result contextCurrentValidity, errFn
                let! msg2 = result contextFutureValidity, errFn
                let! msg3 = result contextWithoutReplacementService, errFn   

                let msg4 = 
                    match BusinessLogic.TP_Canopy_Difference.calculate_TP_Canopy_Difference >> runIO <| () with
                    | Ok _      -> String.Empty
                    | Error err -> err                    

                let separator = String.Empty

                let combinedMessage = 
                    [ msg1; msg2; msg3; msg4 ] 
                    |> List.choose Option.ofNullEmptySpace
                    |> List.map (fun msg -> sprintf "\n%s" msg)
                    |> String.concat separator       

                return sprintf "%s%s" dispatchMsg3 combinedMessage
            }          
    
    let internal stateReducerCmd4 token path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 

        IO (fun () 
                ->
                stateReducer token path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment 
        )