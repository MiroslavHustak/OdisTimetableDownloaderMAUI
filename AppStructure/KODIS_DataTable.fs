namespace MainFunctions

open System

//**********************************

open Types
open Types.Types
open Types.ErrorTypes

open Helpers.Builders

open SubmainFunctions

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral


module WebScraping_KODISFMRecords = 

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
            DownloadAndSaveJson : string list -> string list -> (float * float -> unit) -> Result<unit, JsonDownloadErrors>
            DeleteAllODISDirectories : string -> Result<unit, PdfDownloadErrors>
            OperationOnDataFromJson : unit -> Validity -> string -> Result<(string * string) list, PdfDownloadErrors> 
            DownloadAndSave : Context<string, string, Result<string, PdfDownloadErrors>> -> Result<string, PdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            DownloadAndSaveJson = KODIS_SubmainRecords.downloadAndSaveJson 
            DeleteAllODISDirectories = KODIS_SubmainRecords.deleteAllODISDirectories   
            OperationOnDataFromJson = KODIS_SubmainRecords.operationOnDataFromJson
            DownloadAndSave = KODIS_SubmainRecords.downloadAndSave
        }    

    let private stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress (state : State) (environment : Environment) (action : Actions) =

        let dirList pathToDir = [ sprintf"%s\%s"pathToDir ODISDefault.OdisDir5 ]

        match action with                                                   
        | DownloadAndSaveJson 
            -> 
             //Http request and IO operation (data from settings -> http request -> IO operation -> saving json files on HD)
             let downloadAndSaveJson reportProgress = 
                 let errFn err =  
                     match err with
                     | JsonDownloadError -> "Došlo k chybě, JSON soubory nebyly úspěšně staženy." 
                    
                 try
                     environment.DownloadAndSaveJson (jsonLinkList @ jsonLinkList2) (pathToJsonList @ pathToJsonList2) reportProgress
                     //environment.downloadAndSaveJson jsonLinkList2 pathToJsonList2 reportProgress
                     |> Ok
                 with
                 | ex ->
                       string ex.Message |> ignore  //TODO logfile
                       Error JsonDownloadError
                 
                 |> function
                     | Ok _      -> "Dokončeno stahování JSON souborů." 
                     | Error err -> errFn err

             downloadAndSaveJson reportProgress  

        | DownloadSelectedVariant 
            ->    
             let errFn err =  
                 match err with
                 | RcError     -> "Chyba při zpracování dat, JŘ ODIS nebyly úspěšně staženy." 
                 | JsonFilteringError -> "Chyba při zpracování JSON, JŘ ODIS nebyly úspěšně staženy." 
                 | DataFilteringError -> "Chyba při filtrování dat, JŘ ODIS nebyly úspěšně staženy." 
                 | FileDeleteError    -> "Chyba při mazání starých souborů, JŘ ODIS nebyly úspěšně staženy." 
                 | CreateFolderError  -> "Chyba při tvorbě adresářů, JŘ ODIS nebyly úspěšně staženy." 
                 | FileDownloadError  -> "Chyba při stahování pdf souborů, JŘ ODIS nebyly úspěšně staženy." 

             let result (context2 : Context2) =   

                    dispatchWorkIsComplete "Chvíli strpení, prosím, CPU se snaží, co může ..."
                     
                    let dir = context2.DirList |> List.item context2.VariantInt  
                    let list = KODIS_SubmainRecords.operationOnDataFromJson () context2.Variant dir 

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
                                 
                             dispatchIterationMessage context2.Msg1 //"Stahují se aktuálně platné JŘ ODIS ..."
                                         
                             match list.Length >= 8 with //eqv of 8 threads
                             | true  -> context List.Parallel.map2
                             | false -> context List.map2

                             |> environment.DownloadAndSave     

                    | Ok list
                            ->                                                               
                             dispatchIterationMessage context2.Msg2//"Momentálně nejsou dostupné odkazy na aktuálně platné JŘ ODIS." 
                             System.Threading.Thread.Sleep(6000) 

                             Ok context2.Msg3 //"Aktuálně platné JŘ ODIS nebyly k dispozici pro stažení."

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

                        return sprintf "Kompletní balík JŘ ODIS úspěšně stažen.\n%s\n%s\n%s" msg1 msg2 msg3
                     }
                   
             with
             | ex ->
                   string ex.Message |> ignore  //TODO logfile 
                   "Došlo k chybě, JŘ ODIS nebyly úspěšně staženy."            
    
    let stateReducerCmd1 path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadAndSaveJson

    let stateReducerCmd2 path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadSelectedVariant