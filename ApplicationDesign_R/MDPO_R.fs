namespace ApplicationDesign_R

open System
open System.IO
open System.Threading

open Xamarin.Essentials

open FsToolkit.ErrorHandling

//**********************************

open Types.Types
open Types.ErrorTypes
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.Builders
open Helpers.ExceptionHelpers

open Api.Logging

open BusinessLogic_R.MDPO_BL  

open IO_Operations.IO_Operations

open Settings.Messages
open Settings.SettingsGeneral  

module WebScraping_MDPO =

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
            FilterTimetables : string -> CancellationToken -> IO<Map<string, string>>
            DownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> string -> IO<Map<string, string>> -> IO<Result<unit, MHDErrors>>
        }

    let private environment : Environment =
        { 
            FilterTimetables = filterTimetables 
            DownloadAndSaveTimetables = downloadAndSaveTimetables    
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
                            runIO (postToLog <| string ex.Message <| "#0001-MDPO")
                            Error FileDownloadErrorMHD
           
                    | FilterDownloadSave   
                        ->  
                        let downloadTimetables pathToDir = 
                            result 
                                {
                                    let! firstSubDir = 
                                        dirList pathToDir
                                        |> List.tryHead
                                        |> Option.toResult FileDownloadErrorMHD
                                    
                                    let filter = environment.FilterTimetables firstSubDir token
                        
                                    return! runIO <| environment.DownloadAndSaveTimetables reportProgress token firstSubDir filter
                                }
                        
                        try        
                           downloadTimetables pathToDir
                        with
                        | :? DirectoryNotFoundException as ex 
                            ->
                            runIO (postToLog <| string ex.Message <| "#0002-MDPO")
                            Error FileDeleteErrorMHD  
                        | ex 
                            -> 
                            runIO (postToLog <| string ex.Message <| "#0003-MDPO") // commented out so that cancellation is not logged
                            comprehensiveTryWithMHD 
                                LetItBeMHD StopDownloadingMHD TimeoutErrorMHD 
                                FileDownloadErrorMHD TlsHandshakeErrorMHD token ex                                    

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
                            | TlsHandshakeErrorMHD     -> tlsHandShakeErrorMdpo
                            | TimeoutErrorMHD          -> timeoutError

                        // Kdyz se move nepovede, tak se vubec nic nedeje, proste nebudou starsi soubory,
                        // nicmene priprava na zpracovani err je provedena
                        stateReducer token stateDefault CopyOldTimetables environment |> ignore<Result<unit, MHDErrors>> //silently ignoring failed move operations

                        let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                        return Ok ()
                    }
        )