namespace MainFunctions

open System
open System.IO
open System.Net

//**********************************

open Types.Types   

open Helpers.Builders
   
open Settings.Messages 
open Settings.SettingsGeneral    

open SubmainFunctions.DPO_Submain

module WebScraping_DPO =

    //Design pattern for WebScraping_DPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 
    
    //************************Main code********************************************************************************
  
    type private State =  
        { 
            TimetablesDownloadedAndSaved: unit //zatim nevyuzito
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = () //zatim nevyuzito
        }

    type private Actions =       
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterDownloadSave  

    type private Environment = 
        {
            FilterTimetables : unit -> string -> (string * string) list
            DownloadAndSaveTimetables : (float * float -> unit) -> (string * string) list -> Result<unit, string>
        }

    let private environment: Environment =
        { 
            FilterTimetables = filterTimetables
            DownloadAndSaveTimetables = downloadAndSaveTimetables
        }    

    let internal webscraping_DPO reportProgress pathToDir =  

        let stateReducer (state: State) (action: Actions) (environment: Environment) =
            
            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir5 ] //Android jen forward slash %s/%s

            match action with       
            | DeleteOneODISDirectory ->                                     
                                                                          
                                      try
                                          let dirName = ODISDefault.OdisDir5

                                          //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                                          let dirInfo = DirectoryInfo pathToDir   
                                              in 
                                              dirInfo.EnumerateDirectories()
                                              |> Seq.filter (fun item -> item.Name = dirName) 
                                              |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce 
                                              |> Ok
                                      with
                                      | _ -> Error dpoMsg1                                         
                                    
            | CreateFolders          -> 
                                      try                                          
                                          dirList pathToDir
                                          |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)   
                                          |> Ok
                                      with
                                      | _ -> Error dpoMsg1

            | FilterDownloadSave     ->                                      
                                      try     
                                          let pathToSubdir = dirList pathToDir |> List.tryHead |> function Some value -> value | None -> String.Empty
                                          match pathToSubdir |> Directory.Exists with 
                                          | false ->
                                                   Error String.Empty                              
                                          | true  -> 
                                                   environment.FilterTimetables () pathToSubdir 
                                                   |> environment.DownloadAndSaveTimetables reportProgress 
                                      with
                                      | _ -> Error dpoMsg2                                               

        pyramidOfInferno
            {  
                let item = String.Empty //jen abych mohl vyuzit tento builder a netvorit novy

                let! _ = stateReducer stateDefault DeleteOneODISDirectory environment, fun item -> Error item
                let! _ = stateReducer stateDefault CreateFolders environment, fun item -> Error item
                
                return! stateReducer stateDefault FilterDownloadSave environment
            }