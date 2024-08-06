namespace MainFunctions

open System
open System.IO

open Types.Types

open Helpers.Builders

open Settings.Messages
open Settings.SettingsGeneral

open SubmainFunctions.MDPO_Submain    

module WebScraping_MDPO =

    //Design pattern for WebScraping_MDPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 

    //************************Main code*******************************************************************************

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
            filterTimetables : string -> Map<string, string>
            downloadAndSaveTimetables : (float * float -> unit) -> string -> Map<string, string> -> Result<unit, string>
        }

    let private environment: Environment =
        { 
            filterTimetables = filterTimetables 
            downloadAndSaveTimetables = downloadAndSaveTimetables       
        }    

    let internal webscraping_MDPO reportProgress pathToDir =  

        let stateReducer (state: State) (action: Actions) (environment: Environment) =

            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.odisDir6 ]
           
            let errorHandling fn = 
                try Ok fn
                with ex -> Error <| string ex.Message  

            match action with      
            | DeleteOneODISDirectory ->                                     
                                      let dirName = ODISDefault.odisDir6                                    
                                      let myDeleteFunction =  
                                          //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                                          let dirInfo = new DirectoryInfo(pathToDir)    
                                              in 
                                              dirInfo.EnumerateDirectories()
                                              |> Seq.filter (fun item -> item.Name = dirName) 
                                              |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce    
                                          in errorHandling myDeleteFunction     
                                          
            | CreateFolders          -> 
                                      let myFolderCreation = 
                                          dirList pathToDir
                                          |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)                    
                                          in errorHandling myFolderCreation           
                              
            | FilterDownloadSave     -> 
                                      //filtering timetable links, downloading and saving timetables in the pdf format 
                                     let pathToSubdir = dirList pathToDir |> List.head    
                                     match pathToSubdir |> Directory.Exists with 
                                     | false -> 
                                              Error String.Empty                            
                                     | true  -> 
                                              environment.filterTimetables pathToSubdir 
                                             |> environment.downloadAndSaveTimetables reportProgress pathToSubdir   
                                                           
        pyramidOfInferno
            {  
                let item = "Došlo k chybě, všechny JŘ MDPO nebyly úspěšně staženy."

                let! _ = stateReducer stateDefault DeleteOneODISDirectory environment, fun item -> Error item
                let! _ = stateReducer stateDefault CreateFolders environment, fun item -> Error item
            
                return! stateReducer stateDefault FilterDownloadSave environment
            }

   