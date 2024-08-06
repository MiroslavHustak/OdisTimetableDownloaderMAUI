namespace MainFunctions

open System

open Types
open Types.Types

open SubmainFunctions

open Settings.SettingsKODIS
open Settings.SettingsGeneral

module WebScraping_KODISFMDataTable = 

    type private State =  //not used
        { 
            TimetablesDownloadedAndSaved: unit
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = ()
        }

    type private Actions =
        | DownloadAndSaveJsonFM
        | DownloadSelectedVariantFM        

    type private Environment = 
        {
            downloadAndSaveJson : string list -> string list -> (float * float -> unit) -> unit
            deleteAllODISDirectories : string -> unit
            operationOnDataFromJson : Data.DataTable -> Validity -> string -> (string * string) list
            downloadAndSave : Context<string, string, unit> -> unit
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
                                
            |> function
                | Ok value  -> value  
                | Error err -> ()

        match action with                                                   
        | DownloadAndSaveJsonFM 
            -> 
             //Http request and IO operation (data from settings -> http request -> IO operation -> saving json files on HD)
             let downloadAndSaveJson reportProgress =  
                 //startNetChecking ()

                 environment.downloadAndSaveJson (jsonLinkList @ jsonLinkList2) (pathToJsonList @ pathToJsonList2) reportProgress 
                                                                           
                 in errorHandling <| downloadAndSaveJson reportProgress

        | DownloadSelectedVariantFM 
            ->                                     
             let dt = DataTable.CreateDt.dt() 
                                  
             environment.deleteAllODISDirectories path
                                  
             let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4
             let variantList = [ CurrentValidity; FutureValidity; WithoutReplacementService ]
             let msgList =
                 [
                     "Stahují se aktuálně platné JŘ ODIS"
                     "Stahují se JŘ ODIS platné v budoucnosti"
                     "Stahují se teoreticky dlouhodobě platné JŘ ODIS"
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
              |> ignore     
    
    let stateReducerCmd1 path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
        stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadAndSaveJsonFM

    let stateReducerCmd2 path dispatchWorkIsComplete dispatchIterationMessage reportProgress = 
          stateReducer path dispatchWorkIsComplete dispatchIterationMessage reportProgress stateDefault environment DownloadSelectedVariantFM
   

    
    //FREE MONAD 

    (*

    let internal webscraping_KODISFMDataTable1 pathToDir (variantList: Validity list) reportProgress = 
           
        let rec interpret clp  = 

            let errorHandling fn = 
                try
                    fn
                with
                | ex ->
                      ()//logInfoMsg <| sprintf "Err049 %s" (string ex.Message)
                      //closeItBaby msg16           

            match clp with
            | Pure x                                -> 
                                                     x //nevyuzito

            | Free (StartProcessFM next)            -> 
                                                     (*
                                                     let processStartTime =    
                                                         Console.Clear()
                                                         let processStartTime = 
                                                             try 
                                                                 startNetChecking ()
                                                                 sprintf "Začátek procesu: %s" <| DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")                                                                  
                                                             with
                                                             | ex ->       
                                                                   logInfoMsg <| sprintf "Err503 %s" (string ex.Message)
                                                                   sprintf "Začátek procesu nemohl býti ustanoven."   
                                                             in msgParam7 processStartTime 
                                                         in errorHandling processStartTime
                                                     *)  
                                                     let param = next ()
                                                     interpret param
                                                     

            | Free (DownloadAndSaveJsonFM next)     ->      
                                                     //Http request and IO operation (data from settings -> http request -> IO operation -> saving json files on HD)
                                                     let downloadAndSaveJson reportProgress =  
                                                         //startNetChecking ()
                                                         
                                                         //msg2 ()    
                                                         //msg15 ()
        
                                                         //Console.Write("\r" + new string(' ', (-) Console.WindowWidth 1) + "\r")
                                                         //Console.CursorLeft <- 0  

                                                         KODIS_SubmainDataTable.downloadAndSaveJson (jsonLinkList @ jsonLinkList2) (pathToJsonList @ pathToJsonList2) reportProgress 
                                                         
                                                         //msg3 ()   
                                                         //msg11 ()    
                                                         
                                                         in errorHandling <| downloadAndSaveJson reportProgress                                                          

                                                     let param = next ()
                                                     interpret param                                                
                                                
            | Free (DownloadSelectedVariantFM next) -> 
                                                     let dt = DataTable.CreateDt.dt() 
                                                     
                                                     let downloadSelectedVariant = 
                                                         match variantList |> List.length with
                                                         //SingleVariantDownload
                                                         | 1 -> 
                                                              let variant = variantList |> List.head

                                                              //IO operation
                                                              KODIS_SubmainDataTable.deleteOneODISDirectory variant pathToDir 
                                                                                                                           
                                                              //operation on data 
                                                              let dirList =                                                                    
                                                                  KODIS_SubmainDataTable.createOneNewDirectory  //list -> aby bylo mozno pouzit funkci createFolders bez uprav  
                                                                  <| pathToDir 
                                                                  <| KODIS_SubmainDataTable.createDirName variant listODISDefault4 

                                                              //IO operation 
                                                              KODIS_SubmainDataTable.createFolders dirList

                                                              //msg10 () 

                                                              //operation on data 
                                                              //input from saved json files -> change of input data -> output into array -> input from array -> change of input data -> output into datatable -> data filtering (link*path)  
                                                              let activity = KODIS_SubmainDataTable.operationOnDataFromJson dt variant (dirList |> List.head) 

                                                              //IO operation (data filtering (link*path) -> http request -> saving pdf files on HD)
                                                              activity |> KODIS_SubmainDataTable.downloadAndSave reportProgress (dirList |> List.head) 

                                                         //BulkVariantDownload       
                                                         | _ ->

                                                              //IO operation
                                                              KODIS_SubmainDataTable.deleteAllODISDirectories pathToDir                                                              
                                                              
                                                              //operation on data 
                                                              let dirList = KODIS_SubmainDataTable.createNewDirectories pathToDir listODISDefault4
                                                              
                                                              //IO operation 
                                                              KODIS_SubmainDataTable.createFolders dirList 

                                                              //msg10 ()
                                                              
                                                              (variantList, dirList)
                                                              ||> List.iter2 
                                                                  (fun variant dir 
                                                                      -> 
                                                                       //operation on data 
                                                                       //input from saved json files -> change of input data -> output into array -> input from array -> change of input data -> output into datatable -> data filtering (link*path)  
                                                                       let activity = KODIS_SubmainDataTable.operationOnDataFromJson dt variant dir 

                                                                       //IO operation (data filtering (link*path) -> http request -> saving pdf files on HD)
                                                                       activity |> KODIS_SubmainDataTable.downloadAndSave reportProgress dir   
                                                                  )     
                                                                                                              
                                                         in errorHandling downloadSelectedVariant  

                                                     let param = next ()
                                                     interpret param

            | Free (EndProcessFM _)                 -> ()
                                                     (*
                                                     let processEndTime =    
                                                         let processEndTime = 
                                                             try                                                                
                                                                 sprintf "Konec procesu: %s" <| DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")  
                                                             with
                                                             | ex ->       
                                                                   logInfoMsg <| sprintf "Err504 %s" (string ex.Message)
                                                                   sprintf "Konec procesu nemohl býti ustanoven."   
                                                             in msgParam7 processEndTime
                                                         in errorHandling processEndTime
                                                      *)  
        cmdBuilder
            {
                let! _ = Free (StartProcessFM Pure)
                let! _ = Free (DownloadAndSaveJsonFM Pure)
                let! _ = Free (DownloadSelectedVariantFM Pure)

                return! Free (EndProcessFM Pure)
            } |> interpret 

*)