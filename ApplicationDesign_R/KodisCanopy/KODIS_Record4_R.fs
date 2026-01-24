namespace ApplicationDesign4_R

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

open BusinessLogic_R.KODIS_BL_Record4
open BusinessLogic_R.KODIS_BL_Record4_Json

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
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> IO<Async<Result<(string * string) list, ParsingAndDownloadingErrors list>>> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<unit, exn>> -> Result<string, ParsingAndDownloadingErrors>
        }

    let private environment : Environment =
        { 
            DeleteAllODISDirectories = deleteAllODISDirectories   
            //OperationOnDataFromJson = operationOnDataFromJson4
            OperationOnDataFromJson = operationOnDataFromJson_resumable//operationOnDataFromJson4
            DownloadAndSave = fun token context -> runIO (downloadAndSave token context) 
        }    

    let private stateReducer (token : CancellationToken) path dispatchIterationMessage reportProgress (state : State) (environment : Environment) =
              
        let errFn err =  

            runIO (postToLog2 <| string err <| "#0008-K4")

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
            | PdfDownloadError2 LetItBe                -> letItBe 
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
        
            //dispatchCancelVisible false    
                             
            let dir = context2.DirList |> List.item context2.VariantInt  

            let list =
                runIO <| environment.OperationOnDataFromJson token context2.Variant dir 
                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)

            //dispatchRestartVisible false 
        
            match list with //to je strasne slozite davat to do Elmishe
            | Ok list
                when
                    list <> List.empty
                        -> 
                        let context = 
                            {
                                reportProgress = reportProgress
                                dir = dir
                                list = list
                            }
                                
                        dispatchIterationMessage context2.Msg1                        
                        environment.DownloadAndSave token context   
        
            | Ok _
                ->   
                dispatchIterationMessage context2.Msg2
                Ok context2.Msg3 
        
            | Error list 
                when list |> List.exists (fun item -> item = PdfDownloadError2 StopDownloading)
                ->
                runIO (postToLog2 <| string StopDownloading <| "#0011-K4")
                Error <| PdfDownloadError2 StopDownloading  

            | Error list 
                when list |> List.exists (fun item -> item = PdfDownloadError2 ApiResponseError)
                ->
                runIO (postToLog2 <| string ApiResponseError <| "#0001-K4")
                Error <| PdfDownloadError2 ApiResponseError  

            | Error list 
                when list |> List.exists (fun item -> item = PdfDownloadError2 ApiDecodingError)
                ->
                runIO (postToLog2 <| string ApiDecodingError <| "#0002-K4")
                Error <| PdfDownloadError2 ApiDecodingError  

            | Error list 
                when list |> List.exists (fun item -> item = PdfDownloadError2 FileDownloadError)
                ->
                runIO (postToLog2 <| string FileDownloadError <| "#0003-K4")
                Error <| PdfDownloadError2 FileDownloadError  
        
            | Error err                    
                ->
                runIO (postToLog2 <| sprintf "%A" err <| "#0004-K4")
                Error <| PdfDownloadError2 LetItBe                     
        
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
                runIO (postToLog2 <| msg1 <| "#0005-K4")
                let! msg2 = result contextFutureValidity, errFn
                runIO (postToLog2 <| msg2 <| "#0006-K4")
                let! msg3 = result contextLongTermValidity, errFn 
                runIO (postToLog2 <| msg3 <| "#0007-K4")

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
    
    let internal stateReducerCmd4 token path dispatchIterationMessage reportProgress = 

        IO (fun () 
                ->
                stateReducer token path dispatchIterationMessage reportProgress stateDefault environment 
        )

    let internal stateReducerCmd5 () = // For educational purposes
    
        IO (fun () 
                ->
                BusinessLogic_R.TP_Canopy_Difference.calculate_TP_Canopy_Difference >> runIO <| ()
        )