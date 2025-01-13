namespace ApplicationDesign

open System
open System.Threading

//**********************************

open Types
open Types.Types
open Types.ErrorTypes

open BusinessLogic.KODIS_BL_Record

open Helpers
open Helpers.Builders

open IO_Operations.IO_Operations
open IO_Operations.CreatingPathsAndNames

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral

//Vzhledem k pouziti Elmishe priste podumej nad timto designem, mozna bude lepsi pure transformation layer

module WebScraping_KODISFMRecord = 

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
            DownloadAndSaveJson : string list -> string list -> CancellationToken -> (float * float -> unit) -> Result<unit, JsonDownloadErrors>
            DeleteAllODISDirectories : string -> Result<unit, PdfDownloadErrors>
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> Result<(string * string) list, PdfDownloadErrors> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<unit, exn>> -> Result<string, PdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            DownloadAndSaveJson = downloadAndSaveJson 
            DeleteAllODISDirectories = deleteAllODISDirectories   
            OperationOnDataFromJson = operationOnDataFromJson
            DownloadAndSave = downloadAndSave
        }    

    let internal stateReducerCmd1 (token : CancellationToken) path reportProgress =
       
        let downloadAndSaveJson reportProgress (token : CancellationToken) = 

            let errFn err =  
                match err with
                | JsonDownloadError    -> jsonDownloadError
                | JsonConnectionError  -> cancelMsg2
                | NetConnJsonError err -> err
                | JsonTimeoutError     -> jsonDownloadError  
                | StopJsonDownloading  -> jsonCancel
                    
            try
                try
                    #if ANDROID
                    KeepScreenOnManager.keepScreenOn true
                    #endif

                    //environment.DownloadAndSaveJson (jsonLinkList1 @ jsonLinkList3) (pathToJsonList1 @ pathToJsonList3) reportProgress
                    environment.DownloadAndSaveJson jsonLinkList3 pathToJsonList3 token reportProgress     
                finally
                    #if ANDROID
                    KeepScreenOnManager.keepScreenOn false
                    #endif
                    ()              
            with
            | _ -> Error JsonDownloadError  //TODO logfile
                 
            |> function
                | Ok _      -> Ok dispatchMsg1 
                | Error err -> Error <| errFn err

        downloadAndSaveJson reportProgress token          

    let internal stateReducerCmd2 (token : CancellationToken) path dispatchWorkIsComplete dispatchIterationMessage reportProgress =
    
            let errFn err =  

                match err with
                | RcError              -> rcError
                | NoFolderError        -> noFolderError
                | JsonFilteringError   -> jsonFilteringError
                | DataFilteringError   -> dataFilteringError
                | FileDeleteError      -> fileDeleteError 
                | CreateFolderError    -> createFolderError                
                | FileDownloadError    -> match environment.DeleteAllODISDirectories path with Ok _ -> dispatchMsg4 | Error _ -> dispatchMsg0
                | CanopyError          -> canopyError
                | TimeoutError         -> "timeout"
                | PdfConnectionError   -> cancelMsg2 
                | ApiResponseError err -> err
                | ApiDecodingError     -> canopyError
                | NetConnPdfError err  -> err
                | StopDownloading      -> match environment.DeleteAllODISDirectories path with Ok _ -> cancelMsg4 | Error _ -> cancelMsg5
    
            let result (context2 : Context2) =  
               
                dispatchWorkIsComplete dispatchMsg2
                         
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
                           
                            dispatchIterationMessage context2.Msg1
                          
                            match list.Length >= 8 with //eqv of 8 threads
                            | true  -> context List.Parallel.map2
                            | false -> context List.map2                          
    
                            |> environment.DownloadAndSave token     
    
                | Ok list
                    ->  
                    dispatchIterationMessage context2.Msg2    
                    System.Threading.Thread.Sleep(6000)     
                    Ok context2.Msg3 
    
                | Error err                    
                    ->
                    Error err  
                                 
            try 
                try
                    #if ANDROID
                    KeepScreenOnManager.keepScreenOn true
                    #endif

                    let dirList = createNewDirectoryPaths path listODISDefault4
    
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
                            let!_ = createFolders dirList, errFn 
    
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
                finally
                    #if ANDROID
                    KeepScreenOnManager.keepScreenOn false
                    #endif
                    ()
            with
            | ex
                ->
                string ex.Message |> ignore  //TODO logfile 
                dispatchMsg4   