namespace MainFunctions

open System
open System.Threading

//**********************************

open Types
open Types.Types
open Types.ErrorTypes

open Helpers.Builders

open SubmainFunctions

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral

//Vzhledem k pouziti Elmishe se jeho MVU zda byti dostatecnym a dalsi pokus o design je zbytecny, nicmene hodi se to pro dekonstrukci Result type
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

    type private Actions =
        | DownloadAndSaveJson
        | DownloadSelectedVariant        

    type private Environment = 
        {
            DownloadAndSaveJson : string list -> string list -> CancellationToken -> (float * float -> unit) -> Result<unit, JsonDownloadErrors>
            DeleteAllODISDirectories : string -> Result<unit, PdfDownloadErrors>
            OperationOnDataFromJson : CancellationToken -> Validity -> string -> Result<(string * string) list, PdfDownloadErrors> 
            DownloadAndSave : CancellationToken -> Context<string, string, Result<string, PdfDownloadErrors>> -> Result<string, PdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            DownloadAndSaveJson = KODIS_SubmainRecords.downloadAndSaveJson 
            DeleteAllODISDirectories = KODIS_SubmainRecords.deleteAllODISDirectories   
            OperationOnDataFromJson = KODIS_SubmainRecords.operationOnDataFromJson
            DownloadAndSave = KODIS_SubmainRecords.downloadAndSave
        }    

    let private stateReducer (token : CancellationToken) path dispatchWorkIsComplete dispatchIterationMessage reportProgress (state : State) (environment : Environment) (action : Actions) =

        let dirList pathToDir = [ sprintf"%s\%s"pathToDir ODISDefault.OdisDir5 ]

        match action with                                                   
        | DownloadAndSaveJson 
            -> 
             //Http request and IO operation (data from settings -> http request -> IO operation -> saving json files on HD)
             let downloadAndSaveJson reportProgress token = 

                 let errFn err =  
                     match err with
                     | JsonDownloadError -> jsonDownloadError
                     | CancelJsonProcess -> "dummy text CancelJsonProcess"
                    
                 try
                     //environment.DownloadAndSaveJson (jsonLinkList1 @ jsonLinkList3) (pathToJsonList1 @ pathToJsonList3) reportProgress
                     environment.DownloadAndSaveJson jsonLinkList3 pathToJsonList3 token reportProgress                     
                 with
                 | ex ->
                       string ex.Message |> ignore  //TODO logfile
                       Error JsonDownloadError
                 
                 |> function
                     | Ok _      -> dispatchMsg1
                     | Error err -> errFn err

             downloadAndSaveJson reportProgress token   
             
        | DownloadSelectedVariant 
            ->    
             let errFn err =  
                 match err with
                 | RcError            -> rcError
                 | JsonFilteringError -> jsonFilteringError
                 | DataFilteringError -> dataFilteringError
                 | FileDeleteError    -> fileDeleteError 
                 | CreateFolderError  -> createFolderError
                 | FileDownloadError  -> fileDownloadError
                 | CancelPdfProcess   -> "dummy text CancelPdfProcess"

             let result (context2 : Context2) =   

                 if token.IsCancellationRequested then () else dispatchWorkIsComplete dispatchMsg2
                     
                 let dir = context2.DirList |> List.item context2.VariantInt  
                 let list = KODIS_SubmainRecords.operationOnDataFromJson token context2.Variant dir 

                 match list with
                 | Ok list
                     when list <> List.empty
                         -> 
                          let context listMappingFunction = 
                              {
                                  listMappingFunction = listMappingFunction
                                  reportProgress = reportProgress
                                  dir = dir
                                  list = list
                              }
                                
                          token.IsCancellationRequested |> function true -> () | false -> dispatchIterationMessage context2.Msg1
                                         
                          match list.Length >= 8 with //eqv of 8 threads
                          | true  -> context List.Parallel.map2
                          | false -> context List.map2

                          |> environment.DownloadAndSave token     

                 | Ok list
                         ->                                                               
                          token.IsCancellationRequested |> function true -> () | false -> dispatchIterationMessage context2.Msg2

                          System.Threading.Thread.Sleep(6000) 

                          Ok context2.Msg3 

                 | Error err 
                         ->
                          Error err  
                             
             try 
                 let dirList = KODIS_SubmainRecords.createNewDirectoryPaths path listODISDefault4

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
                        let!_ = KODIS_SubmainRecords.createFolders dirList, errFn 

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
             | ex ->
                   string ex.Message |> ignore  //TODO logfile 
                   dispatchMsg4            
    
    let stateReducerCmd1 token path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer token path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadAndSaveJson

    let stateReducerCmd2 token path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer token path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadSelectedVariant