namespace ApplicationDesign4

open System
open System.Threading

open FsToolkit.ErrorHandling

//**********************************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.Builders

open Api.Logging

open IO_Operations
open IO_Operations.IO_Operations
open IO_Operations.CreatingPathsAndNames

open Settings.Messages
open Settings.SettingsGeneral

open ApplicationDesign4.KODIS_BL_Record4


//**********************************

// 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
// 28-12-2024 Nic neni trvalejsiho, nez neco docasneho...

module WebScraping_KODIS4 = 

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
            DeleteAllODISDirectories : string -> IO<Result<unit, JsonParsingAndPdfDownloadErrors>>
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> IO<Result<(string * string) list, JsonParsingAndPdfDownloadErrors>> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<unit, exn>> -> Result<string, JsonParsingAndPdfDownloadErrors>
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
            | PdfError RcError                 -> rcError
            | PdfError NoFolderError           -> noFolderError            
            | PdfError FileDeleteError         -> fileDeleteError 
            | PdfError CreateFolderError4      -> createFolderError
            | PdfError CreateFolderError2      -> createFolderError2
            | PdfError FileDownloadError       -> (environment.DeleteAllODISDirectories >> runIO) path |> Result.either (fun _ -> dispatchMsg4) (fun _ -> dispatchMsg0)
            | PdfError FolderMovingError4      -> folderMovingError 
            | PdfError CanopyError             -> canopyError
            | PdfError TimeoutError            -> "timeout"
            | PdfError PdfConnectionError      -> cancelMsg2 
            | PdfError (ApiResponseError err)  -> apiResponseError //err je strasne dluha hlaska
            | PdfError ApiDecodingError        -> canopyError
            | PdfError (NetConnPdfError err)   -> err
            | PdfError StopDownloading         -> (environment.DeleteAllODISDirectories >> runIO) path |> Result.either (fun _ -> cancelMsg4) (fun _ -> cancelMsg5)
            | PdfError LetItBeKodis4           -> String.Empty
            | PdfError NoPermissionError       -> String.Empty
            | JsonError JsonParsingError       -> jsonParsingError 
            | JsonError StopJsonParsing        -> (environment.DeleteAllODISDirectories >> runIO) path |> Result.either (fun _ -> cancelMsg4) (fun _ -> cancelMsg5) //tady nenastane
            | JsonError JsonDataFilteringError -> dataFilteringError 
                                     
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

            let contextLongTermValidity = 
                {
                    DirList = dirList
                    Variant = LongTermValidity
                    Msg1 = msg1LongTermValidity
                    Msg2 = msg2LongTermValidity
                    Msg3 = msg3LongTermValidity
                    VariantInt = 2
                }

        let configKodis =
            {
                source1 = path4 <| ODIS_Variants.board.board I1 I1 
                source2 = path4 <| ODIS_Variants.board.board I1 I2 
                source3 = path4 <| ODIS_Variants.board.board I2 I1 
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
        
        [| 
            moveTask1 ()
            moveTask2 ()
            moveTask3 ()
        |]
        |> Async.Parallel  
        |> Async.Catch   //silently ignoring failed move operations //// becomes Async<Result<Result<_,_>[], exn>>
        |> Async.Ignore<Choice<Result<unit, string> array, exn>>  //silently ignoring failed move operations
        |> Async.RunSynchronously
        
       // runIO (postToLog <| DateTime.Now.ToString("HH:mm:ss:fff") <| "Parallel end")  
       
        let result (context2 : Context2) =   
        
            dispatchWorkIsComplete dispatchMsg2
                             
            let dir = context2.DirList |> List.item context2.VariantInt  
            let list = runIO <| operationOnDataFromJson token context2.Variant dir 
        
            match list with //to je strasne slozite davat to do Elmishe
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
                when err <> PdfError StopDownloading
                ->
                Error err  
        
            | Error err                    
                ->
                runIO (postToLog <| err <| "#006-1")
                Error err                     
        
        pyramidOfInferno
            {             
                #if ANDROID
                let!_ = runIO <| createTP_Canopy_Folder logDirTP_Canopy, errFn 
                #endif

                let!_ = runIO <| environment.DeleteAllODISDirectories path, errFn  
                let!_ = runIO <| createFolders dirList, errFn 

                let! msg1 = result contextCurrentValidity, errFn
                let! msg2 = result contextFutureValidity, errFn
                let! msg3 = result contextLongTermValidity, errFn   

                let msg4 = String.Empty //viz App.fs a viz stateReducerCmd5 dole
                    //match BusinessLogic.TP_Canopy_Difference.calculate_TP_Canopy_Difference >> runIO <| () with
                    //| Ok _      -> String.Empty
                    //| Error err -> err                    

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

    let internal stateReducerCmd5 () = // For educational purposes
    
        IO (fun () 
                ->
                BusinessLogic.TP_Canopy_Difference.calculate_TP_Canopy_Difference >> runIO <| ()
        )