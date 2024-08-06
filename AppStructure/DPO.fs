namespace MainFunctions

open System
open System.IO
open System.Net

open Types.Types   

open Helpers.Builders
open Helpers.CloseApp
   
open Settings.Messages
open Settings.SettingsGeneral    

open SubmainFunctions.DPO_Submain

module WebScraping_DPO =

    //Design pattern for WebScraping_DPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 
    
    //************************Main code********************************************************************************
  
    type private State =  
        { 
            TimetablesDownloadedAndSaved: string
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = String.Empty //Podumat nad default textem
        }

    type private Actions =       
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterDownloadSave  

    type private Environment = 
        {
            filterTimetables : string -> (string * string) list
            downloadAndSaveTimetables : (float * float -> unit) -> (string * string) list -> Result<unit, string>
        }

    let private environment: Environment =
        { 
            filterTimetables = filterTimetables
            downloadAndSaveTimetables = downloadAndSaveTimetables
        }    

    let internal webscraping_DPO reportProgress pathToDir =  

        let stateReducer (state: State) (action: Actions) (environment: Environment) =
            
            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.odisDir5 ] //Android jen forward slash %s/%s

            match action with       
            | DeleteOneODISDirectory ->                                     
                                                                          
                                      try
                                          let dirName = ODISDefault.odisDir5

                                          //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                                          let dirInfo = new DirectoryInfo(pathToDir)   
                                              in 
                                              dirInfo.EnumerateDirectories()
                                              |> Seq.filter (fun item -> item.Name = dirName) 
                                              |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce 
                                              |> Ok
                                      with
                                      | _ -> Error "Došlo k chybě, JŘ DPO nebyly staženy."                                           
                                    
            | CreateFolders          -> 
                                      try                                          
                                          dirList pathToDir
                                          |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)   
                                          |> Ok
                                      with
                                      | _ -> Error "Došlo k chybě, JŘ DPO nebyly staženy." 

            | FilterDownloadSave     ->                                      
                                      try     
                                          let pathToSubdir = dirList pathToDir |> List.head    
                                          match pathToSubdir |> Directory.Exists with 
                                          | false ->
                                                   Error String.Empty                              
                                          | true  -> 
                                                   environment.filterTimetables pathToSubdir 
                                                   |> environment.downloadAndSaveTimetables reportProgress 
                                      with
                                      | _ -> Error "Došlo k chybě, všechny JŘ DPO nebyly úspěšně staženy."                                                 

        pyramidOfInferno
            {  
                let item = String.Empty //jen abych mohl vyuzit tento builder a netvorit novy

                let! _ = stateReducer stateDefault DeleteOneODISDirectory environment, fun item -> Error item
                let! _ = stateReducer stateDefault CreateFolders environment, fun item -> Error item
                
                return! stateReducer stateDefault FilterDownloadSave environment
            }

       