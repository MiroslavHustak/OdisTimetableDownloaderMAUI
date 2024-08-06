namespace MainFunctions

open System

open Types
open Types.Types

open Helpers

open SubmainFunctions

open Settings.SettingsKODIS
open Settings.SettingsGeneral


module WebScraping_KODISFMDataTable = 

    type private State =  
        { 
            TimetablesDownloadedAndSaved: string
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = String.Empty //Podumat nad default textem
        }

    type private Actions =
        | DownloadAndSaveJsonFM
        | DownloadSelectedVariantFM        

    type private Environment = 
        {
            downloadAndSaveJson : string list -> string list -> (float * float -> unit) -> unit
            deleteAllODISDirectories : string -> unit
            operationOnDataFromJson : Data.DataTable -> Validity -> string -> (string * string) list
            downloadAndSave : Context<string, string, Result<unit, string>> -> Result<unit, string>
        }

    let private environment: Environment =
        { 
            downloadAndSaveJson = KODIS_SubmainDataTable.downloadAndSaveJson 
            deleteAllODISDirectories = KODIS_SubmainDataTable.deleteAllODISDirectories   
            operationOnDataFromJson = KODIS_SubmainDataTable.operationOnDataFromJson
            downloadAndSave = KODIS_SubmainDataTable.downloadAndSave
        }    

    let private stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress (state: State) (environment: Environment) (action: Actions) =

        let dirList pathToDir = [ sprintf"%s\%s"pathToDir ODISDefault.odisDir5 ]

        let errorHandling fn = 
            try Ok fn
            with ex -> Error <| string ex.Message            

        match action with                                                   
        | DownloadAndSaveJsonFM 
            -> 
             //Http request and IO operation (data from settings -> http request -> IO operation -> saving json files on HD)
             let downloadAndSaveJson reportProgress = 
                
                 //startNetChecking ()

                 environment.downloadAndSaveJson (jsonLinkList @ jsonLinkList2) (pathToJsonList @ pathToJsonList2) reportProgress                                                                            
                 in 
                 errorHandling <| downloadAndSaveJson reportProgress
                 |> function
                     | Ok _    -> "Dokončeno stahování JSON souborů." 
                     | Error _ -> "Došlo k chybě, JSON soubory nebyly úspěšně staženy." 

        | DownloadSelectedVariantFM 
            ->                                     
             let dt = DataTable.CreateDt.dt() 
                                  
             environment.deleteAllODISDirectories path
                                  
             let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4
             let variantList = [ CurrentValidity; FutureValidity; WithoutReplacementService ]
             let msgList =
                 [
                     "Stahují se aktuálně platné JŘ ODIS ..."
                     "Stahují se JŘ ODIS platné v budoucnosti ..."
                     "Stahují se teoreticky dlouhodobě platné JŘ ODIS ..."
                 ]

             KODIS_SubmainDataTable.createFolders dirList   
                                  
             (variantList, dirList, msgList)
             |||> List.map3
                 (fun variant dir message 
                     ->
                      dispatchWorkIsComplete "Chvíli strpení, prosím, CPU se smaží ..."
                                           
                      let list = KODIS_SubmainDataTable.operationOnDataFromJson dt variant dir 

                      let context listMappingFunction = 
                          {
                              listMappingFunction = listMappingFunction
                              reportProgress = reportProgress
                              dir = dir
                              list = list
                          }

                      dispatchIterationMessage message
                                           
                      match variant with
                      | FutureValidity -> context List.map2 
                      | _              -> context List.Parallel.map2 

                      |> environment.downloadAndSave
                  )
             |> Result.sequence  
             |> function
                 | Ok _    -> "Kompletní JŘ ODIS úspěšně staženy." 
                 | Error _ -> "Došlo k chybě, JŘ ODIS nebyly úspěšně staženy."              
    
    let stateReducerCmd1 path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadAndSaveJsonFM

    let stateReducerCmd2 path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
          stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadSelectedVariantFM