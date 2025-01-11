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

        let deleteOneODISDirectory () = 
            try
                let dirName = ODISDefault.OdisDir6 

                //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                let dirInfo = DirectoryInfo pathToDir    
                    in 
                    dirInfo.EnumerateDirectories()
                    |> Seq.filter (fun item -> item.Name = dirName) 
                    |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce 
                    |> Ok
            with
            | _ -> Error FileDownloadErrorMHD // mdpoMsg1  

        let stateReducer token (state : State) (action: Actions) (environment : Environment) =

            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir6 ]
            
            match action with      
            | DeleteOneODISDirectory
                ->  
                deleteOneODISDirectory () 
               
            | CreateFolders        
                -> 
                try
                    dirList pathToDir
                    |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)   
                    |> Ok
                with
                | _ -> Error FileDownloadErrorMHD //mdpoMsg1
           
            | FilterDownloadSave   
                ->                                      
                try  
                    try          
                        #if ANDROID
                        KeepScreenOnManager.keepScreenOn true
                        #endif

                        let pathToSubdir =
                            dirList pathToDir 
                            |> List.tryHead 
                            |> function Some value -> value | None -> String.Empty
                            in
                            match pathToSubdir |> Directory.Exists with 
                            | false ->
                                    Error FileDeleteErrorMHD                             
                            | true  -> 
                                    environment.FilterTimetables () pathToSubdir 
                                    |> environment.DownloadAndSaveTimetables reportProgress token pathToSubdir
                    finally
                        #if ANDROID
                        KeepScreenOnManager.keepScreenOn false
                        #endif
                        ()                         
                with
                | _ -> Error FileDownloadErrorMHD //mdpoMsg2               
                                                           
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
                    | FileDownloadErrorMHD  -> match deleteOneODISDirectory () with Ok _ -> String.Empty | Error _ -> (); mdpoMsg1
                    | ConnectionError       -> noNetConn
                    | FileDeleteErrorMHD    -> fileDeleteError
                    | StopDownloadingMHD    -> match deleteOneODISDirectory () with Ok _ -> String.Empty | Error _ -> (); String.Empty

                let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                return Ok ()
            }