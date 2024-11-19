namespace ApplicationDesign4

open System
open System.Threading

//**********************************

open Types
open Types.Types
open Types.ErrorTypes

open BusinessLogic4.KODIS_BL_Record4

open Helpers.Builders

open IO_Operations.IO_Operations
open IO_Operations.CreatingPathsAndNames

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral

// 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  

//Vzhledem k pouziti Elmishe se jeho MVU zda byti dostatecnym a dalsi pokus o design je zbytecny, nicmene hodi se to pro dekonstrukci Result type
module WebScraping_KODISFMRecord4 = 

    type private State =  
        { 
            TimetablesDownloadedAndSaved : unit
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = ()
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
            DeleteAllODISDirectories : string -> Result<unit, PdfDownloadErrors>
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> Result<(string * string) list, PdfDownloadErrors> 
            DownloadAndSave : CancellationToken -> Context<string, string, unit> -> Result<string, PdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            DeleteAllODISDirectories = deleteAllODISDirectories   
            OperationOnDataFromJson = operationOnDataFromJson
            DownloadAndSave = downloadAndSave
        }    

    let private stateReducer (token : CancellationToken) path dispatchWorkIsComplete dispatchIterationMessage reportProgress (state : State) (environment : Environment) =

        let dirList pathToDir = [ sprintf"%s\%s"pathToDir ODISDefault.OdisDir5 ]
              
        let errFn err =  
            match err with
            | RcError                -> rcError
            | JsonFilteringError     -> jsonFilteringError
            | DataFilteringError     -> dataFilteringError
            | FileDeleteError        -> fileDeleteError 
            | CreateFolderError      -> createFolderError
            | FileDownloadError      -> fileDownloadError
            | CanopyError            -> canopyError
            | TimeoutError                -> "timeout"
            | PdfConnectionError     -> cancelMsg2 
            | CancelPdfProcess       -> cancelMsg2      
            | ApiResponseError err   -> err
            | ApiDecodingError       -> canopyError
            | NetConnPdfError err    -> err

        let result (context2 : Context2) =   

            match token.IsCancellationRequested with
            | true  -> environment.DeleteAllODISDirectories path |> ignore
            | false -> dispatchWorkIsComplete dispatchMsg2
                     
            let dir = context2.DirList |> List.item context2.VariantInt  
            let list = operationOnDataFromJson token context2.Variant dir 

            match list with
            | Ok list
                when
                    list <> List.empty
                        -> 
                        let context listMappingFunction = 
                            {
                                listMappingFunction = listMappingFunction
                                reportProgress = reportProgress
                                dir = dir
                                list = list
                            }
                                
                        match token.IsCancellationRequested with
                        | true  -> environment.DeleteAllODISDirectories path |> ignore
                        | false -> dispatchIterationMessage context2.Msg1
                                         
                        match list.Length >= 8 with //eqv of 8 threads
                        | true  -> context List.Parallel.map2
                        | false -> context List.map2

                        |> environment.DownloadAndSave token     

            | Ok list
                    ->                                                               
                    match token.IsCancellationRequested with
                    | true  -> environment.DeleteAllODISDirectories path |> ignore
                    | false -> dispatchIterationMessage context2.Msg2

                    System.Threading.Thread.Sleep(6000) 

                    Ok context2.Msg3 

            | Error err 
                    ->
                    Error err  
                             
        try 
            let dirList = IO_Operations.CreatingPathsAndNames.createNewDirectoryPaths path listODISDefault4

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
                    let!_ = environment.DeleteAllODISDirectories path, errFn  
                    let!_ = IO_Operations.IO_Operations.createFolders dirList, errFn 

                    let! msg1 = result contextCurrentValidity, errFn
                    let! msg2 = result contextFutureValidity, errFn
                    let! msg3 = result contextWithoutReplacementService, errFn   

                    let separator = String.Empty

                    let combinedMessage = 
                        [ msg1; msg2; msg3 ] 
                        |> List.filter (fun msg -> not (String.IsNullOrWhiteSpace msg)) //IsNullOrWhiteSpace si vsima aji empty string
                        |> List.map (fun msg -> sprintf "\n%s" msg)
                        |> String.concat separator                         

                    return sprintf "%s%s" dispatchMsg3 combinedMessage
                }
                   
        with
        | ex 
            ->
            string ex.Message |> ignore  //TODO logfile 
            dispatchMsg4            
    
    let stateReducerCmd4 token path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer token path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment 