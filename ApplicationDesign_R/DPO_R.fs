namespace ApplicationDesign_R
        
open System.IO
        
//**********************************
        
open FsToolkit.ErrorHandling
        
//**********************************
        
open Types.Types   
open Types.ErrorTypes
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation
        
open Helpers
open Helpers.Builders
        
open Api.Logging
open BusinessLogic_R.DPO_BL
open IO_Operations.IO_Operations
        
open Settings.Messages
open Settings.SettingsGeneral 
        
module WebScraping_DPO =
          
    type private State =  
        { 
            TimetablesDownloadedAndSaved : int //zatim nevyuzito
        }
        
    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = 0 //zatim nevyuzito
        }
        
    type private Actions =       
        | CopyOldTimetables
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterOnly      
        | DownloadSave    

    let internal webscraping_DPO_Filter reportProgress token pathToDir : IO<Result<(string * string) list, MHDErrors>> =

        IO (fun ()
                ->
                let stateReducer token (state : State) (action : Actions) =
                    
                    let dirList pathToDir = [ sprintf"%s/%s"pathToDir (ODIS_Variants.board.board I2 I2) ] //Android jen forward slash %s/%s  //list used due to uniformity
                            
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
                            |> Result.either (fun _ -> Ok []) (fun err -> Error err)
                        with
                        | ex 
                            ->
                            runIO (postToLog2 <| string ex.Message <| "#0001-DPO")
                            Ok [] //silently ignoring failed move operations
        
                    | DeleteOneODISDirectory 
                        ->    
                        runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I2) pathToDir
                        |> Result.either (fun _ -> Ok []) (fun err -> Error err)
                             
                    | CreateFolders         
                        -> 
                        try                                          
                            dirList pathToDir
                            |> List.iter (fun dir -> Directory.CreateDirectory dir |> ignore<DirectoryInfo>)   
                           
                            Ok []
                        with
                        | ex 
                            -> 
                            runIO (postToLog2 <| string ex.Message <| "#0002-DPO")
                            Error FileDownloadErrorMHD
        
                    | FilterOnly
                        ->
                        try                              
                            result 
                                {
                                    let! firstSubDir = 
                                        dirList pathToDir
                                        |> List.tryHead
                                        |> Option.toResult FileDownloadErrorMHD                                   

                                    return! runIO <| filterTimetables reportProgress firstSubDir token
                                }                           
                        with
                        | ex ->
                            runIO (postToLog2 <| string ex.Message <| "#0005-DPO")
                            Error FileDownloadErrorMHD

                    | DownloadSave 
                        -> 
                        Ok [] //not used in this function, satisfies compiler

                pyramidOfInferno
                    {        
                        let! _ = stateReducer token stateDefault CopyOldTimetables, fun err -> Error err       
                        let! _ = stateReducer token stateDefault DeleteOneODISDirectory, fun err -> Error err
                        let! _ = stateReducer token stateDefault CreateFolders, fun err -> Error err
                        let! result = stateReducer token stateDefault FilterOnly, fun err -> Error err  
        
                        return Ok result
                    }
        )

    let internal webscraping_DPO_Download reportProgress token filterResult = //pro jednotnost s ostatnimi download cases

        IO (fun () 
                -> runIO <| downloadAndSaveTimetables reportProgress token filterResult
        )

    let internal errMsg err = 

        match err with
        | BadRequest               -> "400 Bad Request"
        | InternalServerError      -> "500 Internal Server Error"
        | NotImplemented           -> "501 Not Implemented"
        | ServiceUnavailable       -> "503 Service Unavailable"        
        | NotFound                 -> "404 Page Not Found"
        | CofeeMakerUnavailable    -> "418 I'm a teapot. Look for a coffee maker elsewhere."
        | FileDownloadErrorMHD     -> runIO (deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I2) dpoPathTemp) |> Result.either (fun _ -> dpoMsg1) (fun _ -> dpoMsg0)
        | FolderCopyOrMoveErrorMHD -> folderCopyingError
        | ConnectionError          -> noNetConn
        | FileDeleteErrorMHD       -> fileDeleteError
        | StopDownloadingMHD       -> runIO (deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I2) dpoPathTemp) |> Result.either (fun _ -> dpoCancelMsg) (fun _ -> dpoCancelMsg1)
        | LetItBeMHD               -> letItBe
        | TlsHandshakeErrorMHD     -> tlsHandShakeErrorDpo
        | TimeoutErrorMHD          -> timeoutError