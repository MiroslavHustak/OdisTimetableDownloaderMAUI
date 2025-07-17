namespace ApplicationDesign

open System
open System.IO
open System.Net.Http
open System.Threading

//**********************************

open Types.Types
open Types.FreeMonad
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.Builders
open Helpers.CopyOrMoveDirectories

open Api.Logging
open BusinessLogic.MDPO_BL  
open IO_Operations.IO_Operations

open Settings.Messages
open Settings.SettingsGeneral  

module WebScraping_MDPO =

    //Design pattern for WebScraping_MDPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 

    //************************Main code*******************************************************************************

    type private State =  
        { 
            TimetablesDownloadedAndSaved : int  //zatim nevyuzito
        }
    
    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = 0 //zatim nevyuzito
        }

    type private Actions =
        | CopyOldTimetables
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterDownloadSave    

    type private Environment = 
        {
            SafeFilterTimetables : string -> CancellationToken -> IO<Map<string, string>>
            SafeDownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> string -> IO<Map<string, string>> -> IO<Result<unit, MHDErrors>>
            UnsafeFilterTimetables : string -> CancellationToken -> IO<Map<string, string>>
            UnsafeDownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> string -> IO<Map<string, string>> -> IO<Result<unit, MHDErrors>>
        }

    let private environment : Environment =
        { 
            SafeFilterTimetables = safeFilterTimetables 
            SafeDownloadAndSaveTimetables = safeDownloadAndSaveTimetables      
            UnsafeFilterTimetables = unsafeFilterTimetables 
            UnsafeDownloadAndSaveTimetables = unsafeDownloadAndSaveTimetables       
        }    

    let internal webscraping_MDPO reportProgress token pathToDir = 

        IO (fun () 
                ->           
                let stateReducer token (state : State) (action : Actions) (environment : Environment) =

                    let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir6 ] //due to uniformity

                    let config =
                        {
                            source = dirList pathToDir |> List.head 
                            destination = oldTimetablesPath4
                        }

                    match action with   
                    | CopyOldTimetables 
                        ->                          
                        pyramidOfInferno
                            {
                                let! _ = 
                                    Directory.Exists config.source |> Result.fromBool () LetItBeMHD,
                                        fun _ -> Ok ()     
                                        
                                let! _ =
                                    Directory.Exists config.destination |> Result.fromBool () FileCopyingErrorMHD,
                                        fun err 
                                            ->
                                            try
                                                Directory.CreateDirectory config.destination |> ignore<DirectoryInfo>
                                                Ok ()
                                            with 
                                            | ex 
                                                ->
                                                runIO (postToLog <| ex.Message <| "#10-3")
                                                Error err                       
                               
                                let! _ = 
                                    runFreeMonad 
                                    <| 
                                    copyOrMoveFiles config Copy,   
                                        fun err
                                            ->
                                            runIO (postToLog <| err <| "#10-4")
                                            Error FileCopyingErrorMHD
                         
                                return Ok ()
                             }                        
                  
                    | DeleteOneODISDirectory
                        ->  
                        runIO <| deleteOneODISDirectoryMHD ODISDefault.OdisDir6 pathToDir

                    | CreateFolders         
                        -> 
                        try                                          
                            dirList pathToDir
                            |> List.iter (fun dir -> Directory.CreateDirectory dir |> ignore<DirectoryInfo>)   
                            |> Ok
                        with
                        | ex 
                            -> 
                            runIO (postToLog <| ex.Message <| "#7")
                            Error FileDownloadErrorMHD //dpoMsg1
           
                    | FilterDownloadSave   //Quli problemum s certifikatem www.mdpo.cz zatim try with bloky vsade, kaj se da
                        ->                                      
                        try                     
                            let pathToSubdir =
                                dirList pathToDir 
                                |> List.tryHead 
                                |> Option.defaultValue String.Empty
                                in
                                match pathToSubdir |> Directory.Exists with 
                                | false ->
                                        runIO (postToLog <| FileDeleteErrorMHD <| "#8-1")
                                        Error FileDeleteErrorMHD                             
                                | true  -> 
                                        let filterTmtb = environment.SafeFilterTimetables pathToSubdir token
                                        runIO <| environment.SafeDownloadAndSaveTimetables reportProgress token pathToSubdir filterTmtb                                       
                        with
                        | ex 
                            ->
                            runIO (postToLog <| ex.Message <| "#8") //net_http_ssl_connection_failed

                            try
                                let pathToSubdir =
                                    dirList pathToDir 
                                    |> List.tryHead 
                                    |> Option.defaultValue String.Empty
                                    in
                                    match pathToSubdir |> Directory.Exists with 
                                    | false ->
                                            runIO (postToLog <| FileDeleteErrorMHD <| "#9-1") 
                                            Error FileDeleteErrorMHD                             
                                    | true  ->                                             
                                            let filterTmtb = environment.UnsafeFilterTimetables pathToSubdir token  
                                    
                                            (runIO <| environment.UnsafeDownloadAndSaveTimetables reportProgress token pathToSubdir filterTmtb)
                                            |> function
                                                | Ok _      -> 
                                                            Error <| TestDuCase "Staženo jen díky vypnutého ověřování certifikatu www.mdpo.cz"
                                                | Error err ->
                                                            runIO (postToLog <| err <| "#9-1") 
                                                            Error err       
                                            //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)
                            with
                            | ex 
                                ->
                                runIO (postToLog <| ex.Message <| "#9")
                                Error (TestDuCase (sprintf "%s%s" (string ex.Message) " X04")) //FileDownloadErrorMHD //mdpoMsg2 //quli ex je refactoring na result komplikovany                     
                                                                  
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
                            | FileDownloadErrorMHD  -> match runIO <| deleteOneODISDirectoryMHD ODISDefault.OdisDir6 pathToDir with Ok _ -> mdpoMsg1 | Error _ -> mdpoMsg0 
                            | FileCopyingErrorMHD   -> fileCopyingError
                            | ConnectionError       -> noNetConn
                            | FileDeleteErrorMHD    -> fileDeleteError
                            | StopDownloadingMHD    -> match runIO <| deleteOneODISDirectoryMHD ODISDefault.OdisDir6 pathToDir with Ok _ -> mdpoCancelMsg | Error _ -> mdpoCancelMsg1
                            | LetItBeMHD            -> String.Empty
                            | TestDuCase ex         -> ex 

                        let! _ = stateReducer token stateDefault CopyOldTimetables environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                        return Ok ()
                    }
        )