﻿namespace ApplicationDesign

open System
open System.IO
open System.Threading

//**********************************

open Types.Types   
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Helpers.Builders

open Api.Logging
open BusinessLogic.DPO_BL   
open IO_Operations.IO_Operations

open Settings.Messages
open Settings.SettingsGeneral 

module WebScraping_DPO =

    //Design pattern for WebScraping_DPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 
    
    //************************Main code********************************************************************************
  
    type private State =  
        { 
            TimetablesDownloadedAndSaved : int //zatim nevyuzito
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = 0 //zatim nevyuzito
        }

    type private Actions =       
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterDownloadSave  

    type private Environment = 
        {
            FilterTimetables : string -> IO<(string * string) list>
            DownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> IO<(string * string) list> -> IO<Result<unit, MHDErrors>>
        }

    let private environment: Environment =
        { 
            FilterTimetables = filterTimetables  
            DownloadAndSaveTimetables = downloadAndSaveTimetables   
        }    

    let internal webscraping_DPO reportProgress token pathToDir =  

        IO (fun () 
                ->           
                let stateReducer token (state : State) (action : Actions) (environment : Environment) =
            
                    let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir5 ] //Android jen forward slash %s/%s            

                    match action with       
                    | DeleteOneODISDirectory 
                        ->                                   
                        runIO <| deleteOneODISDirectoryMHD ODISDefault.OdisDir5 pathToDir                                    
                                    
                    | CreateFolders         
                        -> 
                        try                                          
                            dirList pathToDir
                            |> List.iter (fun dir -> Directory.CreateDirectory dir |> ignore<DirectoryInfo>)   
                            |> Ok
                        with
                        | ex 
                            -> 
                            runIO (postToLog <| string ex.Message <| "#10")
                            Error FileDownloadErrorMHD //dpoMsg1

                    | FilterDownloadSave   
                        ->     
                        //try-with nutny pro FSharp.Data.HtmlDocument.Load url v DPO-BL.fs
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
                                        let filterTmtb = environment.FilterTimetables pathToSubdir                                     
                                        runIO <| environment.DownloadAndSaveTimetables reportProgress token filterTmtb 
                        with
                        | ex 
                            ->
                            runIO (postToLog <| string ex.Message <| "#11")
                            Error FileDownloadErrorMHD //dpoMsg2    
                       
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
                            | FileDownloadErrorMHD  -> match runIO <| deleteOneODISDirectoryMHD ODISDefault.OdisDir5 pathToDir with Ok _ -> dpoMsg1 | Error _ -> dpoMsg0 
                            | ConnectionError       -> noNetConn
                            | FileDeleteErrorMHD    -> fileDeleteError
                            | StopDownloadingMHD    -> match runIO <| deleteOneODISDirectoryMHD ODISDefault.OdisDir5 pathToDir with Ok _ -> dpoCancelMsg | Error _ -> dpoCancelMsg1
                            | TestDuCase ex         -> ex

                        let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                        let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                        return Ok ()
                    }
        )