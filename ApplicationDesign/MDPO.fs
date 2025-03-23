namespace ApplicationDesign

open System
open System.IO
open System.Threading

//**********************************

open Types.Types
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open BusinessLogic.MDPO_BL  

open Settings.Messages
open Settings.SettingsGeneral  

open FsToolkit.ErrorHandling

open IO_Operations.IO_Operations

module WebScraping_MDPO =

    //Design pattern for WebScraping_MDPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 

    //************************Main code*******************************************************************************

    type private State =  
        { 
            TimetablesDownloadedAndSaved: unit  //zatim nevyuzito
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
            FilterTimetables : unit -> string -> Map<string, string>
            DownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> string -> Map<string, string> -> Result<unit, MHDErrors>
        }

    let private environment : Environment =
        { 
            FilterTimetables = filterTimetables 
            DownloadAndSaveTimetables = downloadAndSaveTimetables       
        }    

    let internal webscraping_MDPO reportProgress token pathToDir =  
           
        let stateReducer token (state : State) (action: Actions) (environment : Environment) =

            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir6 ]
            
            match action with      
            | DeleteOneODISDirectory
                ->  
                deleteOneODISDirectoryMHD ODISDefault.OdisDir6 pathToDir

            | CreateFolders        
                ->               
                result
                    {                                          
                        return
                            dirList pathToDir
                            |> List.iter (fun dir -> Directory.CreateDirectory dir |> ignore)   
                    }   
                |> Result.mapError (fun _ -> FileDownloadErrorMHD) //dpoMsg1
           
            | FilterDownloadSave   
                ->                                      
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
                                |> environment.DownloadAndSaveTimetables reportProgress token pathToSubdir                                        
                with
                | ex -> Error (TestDuCase (sprintf "%s%s" (string ex.Message) " X03")) //FileDownloadErrorMHD //mdpoMsg2 //quli ex je refactoring na result komplikovany
                                                           
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
                    | FileDownloadErrorMHD  -> match deleteOneODISDirectoryMHD ODISDefault.OdisDir6 pathToDir with Ok _ -> mdpoMsg1 | Error _ -> mdpoMsg0 
                    | ConnectionError       -> noNetConn
                    | FileDeleteErrorMHD    -> fileDeleteError
                    | StopDownloadingMHD    -> match deleteOneODISDirectoryMHD ODISDefault.OdisDir6 pathToDir with Ok _ -> mdpoCancelMsg | Error _ -> mdpoCancelMsg1
                    | TestDuCase ex         -> ex 

                let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                return Ok ()
            }