﻿namespace ApplicationDesign4

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

//**********************************

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
            | CreateFolderError4   -> createFolderError
            | CreateFolderError2   -> createFolderError2
            | FileDownloadError    -> match runIO <| environment.DeleteAllODISDirectories path with Ok _ -> dispatchMsg4 | Error _ -> dispatchMsg0
            | FolderMovingError4   -> folderMovingError 
            | CanopyError          -> canopyError
            | TimeoutError         -> "timeout"
            | PdfConnectionError   -> cancelMsg2 
            | ApiResponseError err -> err
            | ApiDecodingError     -> canopyError
            | NetConnPdfError err  -> err
            | StopDownloading      -> match runIO <| environment.DeleteAllODISDirectories path with Ok _ -> cancelMsg4 | Error _ -> cancelMsg5
            | LetItBeKodis4        -> String.Empty
            | NoPermissionError    -> String.Empty

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
                source1 = path4 ODISDefault.OdisDir1 
                source2 = path4 ODISDefault.OdisDir2 
                source3 = path4 ODISDefault.OdisDir4 
                destination = oldTimetablesPath4 
            }

        // Kdyz se move nepovede, tak se vubec nic nedeje, proste nebudou starsi soubory,
        // nicmene priprava na zpracovani err je provedena  
        let moveTask1 () = 
            async
                {
                    let!_ = runIOAsync <| moveFolders configKodis.source1 configKodis.destination LetItBeKodis4 FolderMovingError4
                    return Ok () 
                }
        
        let moveTask2 () = 
            async 
                {    
                    let!_ = runIOAsync <| moveFolders configKodis.source2 configKodis.destination LetItBeKodis4 FolderMovingError4
                    return Ok ()  
                }

        let moveTask3 () = 
            async
                {
                    let!_ = runIOAsync <| moveFolders configKodis.source3 configKodis.destination LetItBeKodis4 FolderMovingError4
                    return Ok ()  
                }     
               
        //runIO (postToLog <| DateTime.Now.ToString("HH:mm:ss:fff") <| "Parallel start")
        
        [ 
            moveTask1 ()
            moveTask2 ()
            moveTask3 ()
        ]
        |> Async.Parallel  
        |> Async.Catch   //silently ignoring failed move operations //// becomes Async<Result<Result<_,_>[], exn>>
        |> Async.Ignore  //silently ignoring failed move operations
        |> Async.RunSynchronously
        
       // runIO (postToLog <| DateTime.Now.ToString("HH:mm:ss:fff") <| "Parallel end")                                           
        
        pyramidOfInferno
            {             
                #if ANDROID
                let!_ = runIO <| createTP_Canopy_Folder logDirTP_Canopy, errFn 
                #endif

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