namespace MainFunctions

open System

open Types

open Settings.Messages
open Settings.SettingsGeneral
    
open Helpers.CloseApp  
open Helpers.FreeMonads

open SubmainFunctions
open SubmainFunctions.KODIS_SubmainDataTable
open Settings.SettingsKODIS

module WebScraping_KODISFMDataTable = 
    
    //FREE MONAD 

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