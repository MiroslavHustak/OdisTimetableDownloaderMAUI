namespace ApplicationDesign

open System
open System.IO
open System.Net
open System.Threading

//**********************************

open Types.Types   

open Helpers.Builders
open Types.ErrorTypes

open BusinessLogic.DPO_BL
   
open Settings.Messages 
open Settings.SettingsGeneral  

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
            DownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> (string * string) list -> Result<unit, MHDErrors>
        }

    let private environment: Environment =
        { 
            FilterTimetables = filterTimetables
            DownloadAndSaveTimetables = downloadAndSaveTimetables
        }    

    let internal webscraping_DPO reportProgress token pathToDir =  

        let stateReducer token (state : State) (action : Actions) (environment : Environment) =
            
            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir5 ] //Android jen forward slash %s/%s

            match action with       
            | DeleteOneODISDirectory 
                ->                                   
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
                | _ -> Error FileDownloadErrorMHD //dpoMsg1                                         
                                    
            | CreateFolders         
                -> 
                try                                          
                    dirList pathToDir
                    |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)   
                    |> Ok
                with
                | _ -> Error FileDownloadErrorMHD //dpoMsg1

            | FilterDownloadSave   
                ->                                      
                try     
                    let pathToSubdir = dirList pathToDir |> List.tryHead |> function Some value -> value | None -> String.Empty
                    match pathToSubdir |> Directory.Exists with 
                    | false ->
                            Error FileDeleteErrorMHD                             
                    | true  -> 
                            environment.FilterTimetables () pathToSubdir 
                            |> environment.DownloadAndSaveTimetables reportProgress token
                with
                | _ -> Error FileDownloadErrorMHD //dpoMsg2                                               
                       
        pyramidOfInferno
            {  
                let errFn err =  

                    match err with
                    | BadRequest            -> "400 Bad Request"
                    | InternalServerError   -> "500 Internal Server Error"
                    | NotImplemented        -> "501 Not Implemented"
                    | ServiceUnavailable    -> "503 Service Unavailable"        
                    | NotFound              -> "404 Page Not Found"
                    | CofeeMakerUnavailable -> "418 I'm a teapot. Look for a coffee maker elsewhere."
                    | FileDownloadErrorMHD  -> dpoMsg1
                    | ConnectionError       -> noNetConn
                    | FileDeleteErrorMHD    -> fileDeleteError

                let item = String.Empty //jen abych mohl vyuzit tento builder a netvorit novy

                let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                return Ok ()
            }