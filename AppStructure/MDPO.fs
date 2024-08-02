namespace MainFunctions

open System
open System.IO

open Types.DirNames
open Helpers.CloseApp

open Settings.Messages
open Settings.SettingsGeneral

open SubmainFunctions.MDPO_Submain    

module WebScraping_MDPO =

    //Design pattern for WebScraping_MDPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 

    //************************Main code*******************************************************************************

    type private State =  //not used
        { 
            TimetablesDownloadedAndSaved: unit
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = ()
        }

    type private Actions =
        | StartProcess
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterDownloadSave    
        | EndProcess

    type private Environment = 
        {
            filterTimetables : string -> Map<string, string>
            downloadAndSaveTimetables : string -> Map<string, string> -> unit
        }

    let private environment: Environment =
        { 
            filterTimetables = filterTimetables 
            downloadAndSaveTimetables = downloadAndSaveTimetables       
        }    

    let internal webscraping_MDPO pathToDir =  

         //tryWith block is in the main() function  

        let stateReducer (state: State) (action: Actions) (environment: Environment) =

            //let dirList pathToDir = [ sprintf"%s\%s"pathToDir ODISDefault.odisDir6 ]
            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.odisDir6 ]
           
            let errorHandling fn = 
                try
                    fn
                with
                | ex ->
                      ()//logInfoMsg <| sprintf "Err051 %s" (string ex.Message)
                      //closeItBaby msg16       

            match action with                                                   
            | StartProcess           -> ()
                                      (*
                                      let processStartTime =  
                                          Console.Clear()
                                          let processStartTime = sprintf "Začátek procesu: %s" <| DateTime.Now.ToString("HH:mm:ss") 
                                              in msgParam7 processStartTime 
                                          in errorHandling processStartTime    
                                       *)    

            | DeleteOneODISDirectory ->                                     
                                      let dirName = ODISDefault.odisDir6                                    
                                      let myDeleteFunction =  
                                          //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                                          let dirInfo = new DirectoryInfo(pathToDir)    
                                              in 
                                              dirInfo.EnumerateDirectories()
                                              |> Seq.filter (fun item -> item.Name = dirName) 
                                              |> Seq.iter _.Delete(true)//(fun item -> item.Delete(true)) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce    
                                          in errorHandling myDeleteFunction  
                                      //msg12 () 
                                    
            | CreateFolders          -> 
                                      let myFolderCreation = 
                                          dirList pathToDir
                                          |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)                    
                                          in errorHandling myFolderCreation           
                              
            | FilterDownloadSave     -> 
                                      //filtering timetable links, downloading and saving timetables in the pdf format 
                                      let filterDownloadSave = 
                                          let pathToSubdir = dirList pathToDir |> List.head    
                                          match pathToSubdir |> Directory.Exists with 
                                          | false -> ()                                              
                                                   //msgParam5 pathToSubdir   
                                                   //msg1 ()                                                
                                          | true  -> 
                                                   environment.filterTimetables pathToSubdir 
                                                   |> environment.downloadAndSaveTimetables pathToSubdir                                       
                                          in errorHandling filterDownloadSave           
                                                                                
            | EndProcess             -> ()
                                      (*
                                      let processEndTime =    
                                          let processEndTime = sprintf "Konec procesu: %s" <| DateTime.Now.ToString("HH:mm:ss")                       
                                              in msgParam7 processEndTime
                                          in errorHandling processEndTime 
                                      *)     
                                 
        stateReducer stateDefault StartProcess environment
        stateReducer stateDefault DeleteOneODISDirectory environment
        stateReducer stateDefault CreateFolders environment
        stateReducer stateDefault FilterDownloadSave environment
        stateReducer stateDefault EndProcess environment