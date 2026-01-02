namespace ApplicationDesign

open System
open System.IO
open System.Threading

open FsToolkit.ErrorHandling

//**********************************

open Types.Types
open Types.ErrorTypes
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.Builders

open Api.Logging
open BusinessLogic.MDPO_BL  
open IO_Operations.IO_Operations

open Settings.Messages
open Settings.SettingsGeneral  

module WebScraping_MDPO =

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
                        try
                            runIO <| moveFolders configMHD.source configMHD.destination LetItBeMHD FolderCopyOrMoveErrorMHD
                        with
                        | _ -> Error LetItBeMHD //silently ignoring failed move operations
                  
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
                            runIO (postToLog <| string ex.Message <| "#007")
                            Error FileDownloadErrorMHD //dpoMsg1                  
           
                    | FilterDownloadSave   
                        ->                                      
                        try        
                            dirList pathToDir
                            |> List.tryHead
                            |> Option.toResult "No subdirectory found in dirList (#008-1)"
                            |> function
                                | Ok pathToSubdir 
                                    -> 
                                    let filterTmtb = environment.SafeFilterTimetables pathToSubdir token
                                    runIO <| environment.SafeDownloadAndSaveTimetables reportProgress token pathToSubdir filterTmtb    
                                | Error _
                                    -> 
                                    Error FileDownloadErrorMHD  
                        with
                        | :? DirectoryNotFoundException 
                            ->
                            runIO (postToLog "Timetable directory not found or was deleted" "#008-10")
                            Error FileDeleteErrorMHD  

                        | ex 
                            when (string ex.Message).Contains "Timeout exceeded while getting response"
                            ->
                            runIO (postToLog <| string ex.Message <| "#008-X05") 
                            Error (TestDuCase (sprintf "%s%s" "V časovém limitu jsem neobdržel reakci z www.mdpo.cz. Pokud používáš starou verzi Androidu, asi se k JŘ MDPO takhle nedostaneš." " X05")) 

                        | ex                             
                            ->
                            // runIO (postToLog <| string ex.Message <| "#008") //net_http_ssl_connection_failed 

                            try
                                let pathToSubdir =
                                    dirList pathToDir 
                                    |> List.tryHead 
                                    |> Option.defaultValue String.Empty
                                                                   
                                let filterTmtb = environment.UnsafeFilterTimetables pathToSubdir token  
                                    
                                (runIO <| environment.UnsafeDownloadAndSaveTimetables reportProgress token pathToSubdir filterTmtb)
                                |> function
                                    | Ok _     
                                        ->                                                         
                                        try
                                            let dirInfo = DirectoryInfo pathToDir                                                       
                                                in
                                                dirInfo.EnumerateFiles() 
                                                |> Seq.length
                                                |> function
                                                    | 0 -> Error <| TestDuCase "Stažení se nezdařilo kvůli chybné konfiguraci serveru. Problém je na straně provozovatele www.mdpo.cz, nikoli této aplikace."
                                                    | _ -> Error <| TestDuCase "Staženo jen díky vypnutého ověřování certifikatu www.mdpo.cz"
                                        with 
                                        | ex 
                                            ->
                                            Error <| TestDuCase (string ex.Message)    
                                                    
                                    | Error err
                                        ->
                                        runIO (postToLog <| err <| "#009-2") 
                                        Error err       
                                //a temporary solution until the maintainers of mdpo.cz start doing something with the certifications :-)
                            with
                            | ex 
                                ->
                                //runIO (postToLog <| string ex.Message <| "#009")
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
                            | FileDownloadErrorMHD     -> runIO (deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) pathToDir) |> Result.either (fun _ -> mdpoMsg1) (fun _ -> mdpoMsg0)
                            | FolderCopyOrMoveErrorMHD -> folderCopyingError
                            | ConnectionError          -> noNetConn
                            | FileDeleteErrorMHD       -> fileDeleteError
                            | StopDownloadingMHD       -> runIO (deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) pathToDir) |> Result.either (fun _ -> mdpoCancelMsg) (fun _ -> mdpoCancelMsg1)
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