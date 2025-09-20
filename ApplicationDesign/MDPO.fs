namespace ApplicationDesign

open System
open System.IO
open System.Threading

//**********************************

open Types.Types
open Types.ErrorTypes
open Types.TypeAlgebra
open Types.Haskell_IO_Monad_Simulation

open Helpers.Builders

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

                    let dirList pathToDir = [ sprintf"%s/%s"pathToDir (ODIS_Variants.board.board I2 I3) ] //due to uniformity

                    let configMHD =
                        {
                            source = dirList pathToDir |> List.head 
                            destination = oldTimetablesPath4
                        }

                    match action with   
                    | CopyOldTimetables 
                        ->                          
                        runIO <| moveFolders configMHD.source configMHD.destination LetItBeMHD FolderCopyOrMoveErrorMHD
                  
                    | DeleteOneODISDirectory
                        ->  
                        runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) pathToDir

                    | CreateFolders         
                        -> 
                        try                                          
                            dirList pathToDir
                            |> List.iter (fun dir -> Directory.CreateDirectory dir |> ignore<DirectoryInfo>)   
                            |> Ok
                        with
                        | ex 
                            -> 
                            runIO (postToLog <| ex.Message <| "#007")
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
                                        runIO (postToLog <| FileDeleteErrorMHD <| "#008-1")
                                        Error FileDeleteErrorMHD                             
                                | true  -> 
                                        let filterTmtb = environment.SafeFilterTimetables pathToSubdir token
                                        runIO <| environment.SafeDownloadAndSaveTimetables reportProgress token pathToSubdir filterTmtb                                       
                        with
                        | ex 
                            ->
                            runIO (postToLog <| ex.Message <| "#008") //net_http_ssl_connection_failed

                            try
                                let pathToSubdir =
                                    dirList pathToDir 
                                    |> List.tryHead 
                                    |> Option.defaultValue String.Empty
                                    in
                                    match pathToSubdir |> Directory.Exists with 
                                    | false ->
                                            runIO (postToLog <| FileDeleteErrorMHD <| "#009-1") 
                                            Error FileDeleteErrorMHD                             
                                    | true  ->                                             
                                            let filterTmtb = environment.UnsafeFilterTimetables pathToSubdir token  
                                    
                                            (runIO <| environment.UnsafeDownloadAndSaveTimetables reportProgress token pathToSubdir filterTmtb)
                                            |> function
                                                | Ok _      -> 
                                                            Error <| TestDuCase "Staženo jen díky vypnutého ověřování certifikatu www.mdpo.cz"
                                                | Error err ->
                                                            runIO (postToLog <| err <| "#009-2") 
                                                            Error err       
                                            //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)
                            with
                            | ex 
                                ->
                                runIO (postToLog <| ex.Message <| "#009")
                                Error (TestDuCase (sprintf "%s%s" (string ex.Message) " X04")) //FileDownloadErrorMHD //mdpoMsg2 //quli ex je refactoring na result komplikovany                     
                                                                  
                pyramidOfInferno
                    {  
                        let errFn err =  

                            match err with
                            | BadRequest               -> "400 Bad Request"
                            | InternalServerError      -> "500 Internal Server Error"
                            | NotImplemented           -> "501 Not Implemented"
                            | ServiceUnavailable       -> "503 Service Unavailable"        
                            | NotFound                 -> "404 Page Not Found"
                            | CofeeMakerUnavailable    -> "418 I'm a teapot. Look for a coffee maker elsewhere."
                            | FileDownloadErrorMHD     -> match runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) pathToDir with Ok _ -> mdpoMsg1 | Error _ -> mdpoMsg0 
                            | FolderCopyOrMoveErrorMHD -> folderCopyingError
                            | ConnectionError          -> noNetConn
                            | FileDeleteErrorMHD       -> fileDeleteError
                            | StopDownloadingMHD       -> match runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) pathToDir with Ok _ -> mdpoCancelMsg | Error _ -> mdpoCancelMsg1
                            | LetItBeMHD               -> String.Empty
                            | TestDuCase ex            -> ex 

                        // Kdyz se move nepovede, tak se vubec nic nedeje, proste nebudou starsi soubory,
                        // nicmene priprava na zpracovani err je provedena
                        stateReducer token stateDefault CopyOldTimetables environment |> ignore<Result<unit, MHDErrors>> //silently ignoring failed move operations

                        let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                        return Ok ()
                    }
        )