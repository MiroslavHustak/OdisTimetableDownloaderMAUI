namespace ApplicationDesign

open System
open System.IO
open System.Net
open System.Threading

open Microsoft.Maui.Devices

//**********************************

open Types.Types   
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open BusinessLogic.DPO_BL
   
open Settings.Messages 
open Settings.SettingsGeneral  

open FsToolkit.ErrorHandling

open IO_Operations.IO_Operations

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
                deleteOneODISDirectoryMHD ODISDefault.OdisDir5 pathToDir                                    
                                    
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
                    try  
                        let pathToSubdir =
                            dirList pathToDir 
                            |> List.tryHead 
                            |> Option.defaultValue String.Empty
                            in
                            match pathToSubdir |> Directory.Exists with 
                            | false ->
                                    Error FileDeleteErrorMHD                             
                            | true  -> 
                                    environment.FilterTimetables () pathToSubdir 
                                    |> environment.DownloadAndSaveTimetables reportProgress token
                    finally                        
                        ()                         
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
                    | FileDownloadErrorMHD  -> match deleteOneODISDirectoryMHD ODISDefault.OdisDir5 pathToDir with Ok _ -> dpoMsg1 | Error _ -> dpoMsg0 
                    | ConnectionError       -> noNetConn
                    | FileDeleteErrorMHD    -> fileDeleteError
                    | StopDownloadingMHD    -> match deleteOneODISDirectoryMHD ODISDefault.OdisDir5 pathToDir with Ok _ -> dpoCancelMsg | Error _ -> dpoCancelMsg1
                    | TestDuCase ex         -> ex

                let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                return Ok ()
            }

