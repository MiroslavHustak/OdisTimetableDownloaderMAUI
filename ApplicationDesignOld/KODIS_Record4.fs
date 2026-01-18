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

open BusinessLogic.KODIS_BL_Record4

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
            DeleteAllODISDirectories : string -> IO<Result<unit, ParsingAndDownloadingErrors>>
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> IO<Result<(string * string) list, ParsingAndDownloadingErrors>> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<unit, exn>> -> Result<string, ParsingAndDownloadingErrors>
        }

    let private environment : Environment =
        { 
            DeleteAllODISDirectories = deleteAllODISDirectories   
            OperationOnDataFromJson = operationOnDataFromJson
            DownloadAndSave = downloadAndSave >> runIO
        }    

    let private stateReducer (token : CancellationToken) path dispatchCancelVisible dispatchRestartVisible dispatchWorkIsComplete dispatchIterationMessage reportProgress (state : State) (environment : Environment) =
              
        let errFn err =  

            match err with
            | PdfDownloadError2 RcError                -> rcError
            | PdfDownloadError2 NoFolderError          -> noFolderError            
            | PdfDownloadError2 FileDeleteError        -> fileDeleteError 
            | PdfDownloadError2 CreateFolderError4     -> createFolderError
            | PdfDownloadError2 CreateFolderError2     -> createFolderError2
            | PdfDownloadError2 FileDownloadError      -> (environment.DeleteAllODISDirectories >> runIO) path |> Result.either (fun _ -> dispatchMsg4) (fun _ -> dispatchMsg0)
            | PdfDownloadError2 FolderMovingError4     -> folderMovingError 
            | PdfDownloadError2 CanopyError            -> canopyError
            | PdfDownloadError2 TimeoutError           -> timeoutError
            | PdfDownloadError2 PdfConnectionError     -> cancelMsg2 
            | PdfDownloadError2 ApiResponseError       -> apiResponseError 
            | PdfDownloadError2 ApiDecodingError       -> canopyError
            | PdfDownloadError2 (NetConnPdfError err)  -> err
            | PdfDownloadError2 StopDownloading        -> (environment.DeleteAllODISDirectories >> runIO) path |> Result.either (fun _ -> cancelMsg4) (fun _ -> cancelMsg5)
            | PdfDownloadError2 LetItBeKodis4          -> String.Empty
            | PdfDownloadError2 NoPermissionError      -> String.Empty
            | PdfDownloadError2 TlsHandshakeError      -> tlsHandShakeErrorKodis4
            | JsonParsingError2 JsonParsingError       -> jsonParsingError 
            | JsonParsingError2 StopJsonParsing        -> (environment.DeleteAllODISDirectories >> runIO) path |> Result.either (fun _ -> cancelMsg44) (fun _ -> cancelMsg5) //tady nenastane
            | JsonParsingError2 JsonDataFilteringError -> dataFilteringError 
            | _                                        -> String.Empty
                                     
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
       
        let result (context2 : Context2) =           
           
            let dir = context2.DirList |> List.item context2.VariantInt  
            let list = runIO <| operationOnDataFromJson token context2.Variant dir 

            dispatchWorkIsComplete String.Empty

            //dispatchRestartVisible false 
        
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
        
                        // nyni zcela nepotrebne, ale ponechano jako template record s generic types (mrkni se na function signature)                      
                        match list.Length >= 4 with 
                        | true  -> context List.Parallel.map2_IO_AW
                        | false -> context List.map2
        
                        |> environment.DownloadAndSave token                         
        
            | Ok _
                ->                  
                dispatchIterationMessage context2.Msg2
                System.Threading.Thread.Sleep(6000) 
                Ok context2.Msg3 
        
            | Error err 
                when err <> PdfDownloadError2 StopDownloading
                ->
                Error err  
        
            | Error err                    
                ->
                runIO (postToLog2 <| err <| "#006-1")
                Error err                     
        
        pyramidOfInferno
            {             
                #if ANDROID
                let!_ = runIO <| createTP_Canopy_Folder logDirTP_Canopy, errFn 
                #endif
                
                //dispatchCancelVisible false
               
                let!_ = runIO <| moveAll configKodis token, errFn                  
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
    
    let internal stateReducerCmd4 token path dispatchCancelVisible dispatchRestartVisible dispatchWorkIsComplete dispatchIterationMessage reportProgress = 

        IO (fun () 
                ->
                stateReducer token path dispatchCancelVisible dispatchRestartVisible dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment 
        )

    let internal stateReducerCmd5 () = // For educational purposes
    
        IO (fun () 
                ->
                BusinessLogic.TP_Canopy_Difference.calculate_TP_Canopy_Difference >> runIO <| ()
        )