namespace MainFunctions

open System

open Types
open Types.Types
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open SubmainFunctions

open Settings.SettingsKODIS
open Settings.SettingsGeneral


module WebScraping_KODISFMDataTable = 

    type private State =  
        { 
            TimetablesDownloadedAndSaved : unit
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = ()
        }

    type private Actions =
        | DownloadAndSaveJson
        | DownloadSelectedVariant        

    type private Environment = 
        {
            downloadAndSaveJson : string list -> string list -> (float * float -> unit) -> Result<unit, JsonDownloadErrors>
            deleteAllODISDirectories : string -> Result<unit, PdfDownloadErrors>
            operationOnDataFromJson : unit -> Data.DataTable -> Validity -> string -> Result<(string * string) list, PdfDownloadErrors> 
            downloadAndSave : Context<string, string, Result<string, PdfDownloadErrors>> -> Result<string, PdfDownloadErrors>
        }

    let private environment : Environment =
        { 
            downloadAndSaveJson = KODIS_SubmainDataTable.downloadAndSaveJson 
            deleteAllODISDirectories = KODIS_SubmainDataTable.deleteAllODISDirectories   
            operationOnDataFromJson = KODIS_SubmainDataTable.operationOnDataFromJson
            downloadAndSave = KODIS_SubmainDataTable.downloadAndSave
        }    

    let private stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress (state : State) (environment : Environment) (action : Actions) =

        let dirList pathToDir = [ sprintf"%s\%s"pathToDir ODISDefault.OdisDir5 ]

        match action with                                                   
        | DownloadAndSaveJson 
            -> 
             //Http request and IO operation (data from settings -> http request -> IO operation -> saving json files on HD)
             let downloadAndSaveJson reportProgress = 
                
                 try
                     //startNetChecking ()
                     //environment.downloadAndSaveJson (jsonLinkList @ jsonLinkList2) (pathToJsonList @ pathToJsonList2) reportProgress
                     environment.downloadAndSaveJson jsonLinkList2 pathToJsonList2 reportProgress
                     |> Ok
                 with
                 | _ -> Error String.Empty
                 
                 |> function
                     | Ok _    -> "Dokončeno stahování JSON souborů." 
                     | Error _ -> "Došlo k chybě, JSON soubory nebyly úspěšně staženy." 

             downloadAndSaveJson reportProgress  

        | DownloadSelectedVariant 
            ->    
             try 
                 let dirList = KODIS_SubmainDataTable.createNewDirectoryPaths path listODISDefault4
               
                 let dt = DataTable.CreateDt.dt() 
                 
                 let errFn err =  
                     match err with
                     | DataTableError     -> "Došlo k chybě při zpracování dat, JŘ ODIS nebyly úspěšně staženy." 
                     | DataFilteringError -> "Došlo k chybě při filtrování dat, JŘ ODIS nebyly úspěšně staženy." 
                     | FileDeleteError    -> "Došlo k chybě při mazání starých souborů, JŘ ODIS nebyly úspěšně staženy." 
                     | CreateFolderError  -> "Došlo k chybě při tvorbě adresářů, JŘ ODIS nebyly úspěšně staženy." 
                     | FileDownloadError  -> "Došlo k chybě při stahování pdf souborů, JŘ ODIS nebyly úspěšně staženy." 
                          
                 let resultCurrentValidity () =   

                    dispatchWorkIsComplete "Chvíli strpení, prosím, CPU se snaží, co může ..."
                     
                    let dir = dirList |> List.item 0 
 
                    let list = KODIS_SubmainDataTable.operationOnDataFromJson () dt CurrentValidity dir 

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
                                 
                             dispatchIterationMessage "Stahují se aktuálně platné JŘ ODIS ..."
                                         
                             match list.Length >= 8 with //eqv of 8 threads
                             | true  -> context List.Parallel.map2
                             | false -> context List.map2

                             |> environment.downloadAndSave     

                    | Ok list
                            ->                                                               
                             dispatchIterationMessage "Momentálně nejsou dostupné odkazy na aktuálně platné JŘ ODIS." 
                             System.Threading.Thread.Sleep(6000) 

                             Ok "Aktuálně platné JŘ ODIS nebyly k dispozici pro stažení."

                    | Error err 
                            ->
                             Error err            
                                                   
                 let resultFutureValidity () =   

                    dispatchWorkIsComplete "Chvíli strpení, prosím, CPU se snaží, co může ..."
                     
                    let dir = dirList |> List.item 1 
 
                    let list = KODIS_SubmainDataTable.operationOnDataFromJson () dt FutureValidity dir 

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
                                 
                             dispatchIterationMessage "Stahují se JŘ ODIS platné v budoucnosti ..."
                                         
                             match list.Length >= 8 with //eqv of 8 threads
                             | true  -> context List.Parallel.map2
                             | false -> context List.map2

                             |> environment.downloadAndSave     

                    | Ok list
                            ->                                                               
                             dispatchIterationMessage "Momentálně nejsou dostupné odkazy na JŘ ODIS platné v budoucnosti." 
                             System.Threading.Thread.Sleep(6000)

                             Ok "JŘ ODIS platné v budoucnosti nebyly k dispozici pro stažení."

                    | Error err 
                            ->
                             Error err               

                 let resultWithoutReplacementService () =   

                    dispatchWorkIsComplete "Chvíli strpení, prosím, CPU se snaží, co může ..."
                     
                    let dir = dirList |> List.item 2 
 
                    let list = KODIS_SubmainDataTable.operationOnDataFromJson () dt WithoutReplacementService dir 

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
                                 
                             dispatchIterationMessage "Stahují se teoreticky dlouhodobě platné JŘ ODIS ..."
                                         
                             match list.Length >= 8 with //eqv of 8 threads
                             | true  -> context List.Parallel.map2
                             | false -> context List.map2

                             |> environment.downloadAndSave     

                    | Ok list
                            ->                                                               
                             dispatchIterationMessage "Momentálně nejsou dostupné odkazy na dlouhodobě platné JŘ ODIS." 
                             System.Threading.Thread.Sleep(6000)

                             Ok "Dlouhodobě platné JŘ ODIS nebyly k dispozici pro stažení."

                    | Error err 
                            ->
                             Error err               
                                                   
                 pyramidOfInferno
                    {                                
                        let!_ = environment.deleteAllODISDirectories path, errFn  
                        let!_ = KODIS_SubmainDataTable.createFolders dirList, errFn 

                        let! msg1 = resultCurrentValidity (), errFn
                        let! msg2 = resultFutureValidity (), errFn
                        let! msg3 = resultWithoutReplacementService (), errFn   

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