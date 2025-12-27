namespace IO_Operations

open System.IO
open System.Threading

open FsToolkit.ErrorHandling    

//************************************************************

open Types
open Types.Types
open Types.FreeMonad
open Types.ErrorTypes
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

open Api.Logging

open Helpers
open Helpers.Builders
open Helpers.FreeMonadInterpret

open CreatingPathsAndNames

open Settings.SettingsKODIS
open Settings.SettingsGeneral

module IO_Operations =    
    
    let internal deleteAllODISDirectories pathToDir = 

        IO (fun () 
                ->  
                let deleteIt : Reader<string list, Result<unit, JsonParsingAndPdfDownloadErrors>> = 
        
                    reader //Reader monad for educational purposes only, no real benefit here  
                        {
                            let! getDefaultRecordValues = fun env -> env 
                        
                            return 
                                try
                                    //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                                    let dirInfo = DirectoryInfo pathToDir                                                       
                                        in
                                        dirInfo.EnumerateDirectories() 
                                        |> Seq.filter (fun item -> getDefaultRecordValues |> List.contains item.Name) //prunik dvou kolekci (plus jeste Seq.distinct pro unique items)
                                        |> Seq.distinct 
                                        |> Seq.toList
                                        |> List.Parallel.iter_IO (fun item -> item.Delete true)
                                        |> Ok
                                        //smazeme pouze adresare obsahujici stare JR, ostatni ponechame              
                                with 
                                | ex 
                                    ->
                                    runIO (postToLog <| string ex.Message <| "#038")
                                    Error <| PdfError FileDeleteError
                        }
    
                deleteIt listOfODISVariants  
        )

    let internal deleteOneODISDirectory variant pathToDir =
    
        //smazeme pouze jeden adresar obsahujici stare JR, ostatni ponechame

        IO (fun () 
                ->  
                let deleteIt : Reader<string list, Result<unit, JsonParsingAndPdfDownloadErrors>> =  
    
                    reader //Reader monad for educational purposes only, no real benefit here  
                        {   
                            let! getDefaultRecordValues = fun env -> env
                                                              
                            return 
                                try
                                    //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                                    let dirInfo = DirectoryInfo pathToDir        
                                        in
                                        dirInfo.EnumerateDirectories()
                                        |> Seq.filter (fun item -> item.Name = createDirName variant getDefaultRecordValues) 
                                        |> Seq.toList
                                        |> List.Parallel.iter_IO (fun item -> item.Delete true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce  
                                        |> Ok               
                                    
                                with 
                                | ex
                                    ->
                                    runIO (postToLog <| string ex.Message <| "#039")
                                    Error <| PdfError FileDeleteError                       
                        }
    
                deleteIt listOfODISVariants   
        )
        
    let internal deleteOneODISDirectoryMHD dirName pathToDir = 

        IO (fun () 
                ->  
                try      
                    //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                    let dirInfo = DirectoryInfo pathToDir   
                        in 
                        dirInfo.EnumerateDirectories()
                        |> Seq.filter (fun item -> (=) item.Name dirName) 
                        |> Seq.toList
                        |> List.Parallel.iter_IO (fun item -> item.Delete true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce  
                        |> Ok
                with
                | _ 
                    ->
                    runIO (postToLog <| pathToDir <| "#040")
                    Error FileDownloadErrorMHD //dpoMsg1  
        )   
    
    let internal deleteAllJsonFilesInDirectory pathToDir =

        IO (fun () 
                ->  
                try      
                    //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                    let dirInfo = DirectoryInfo pathToDir   
                        in 
                        dirInfo.EnumerateFiles()
                        |> Seq.toList
                        |> List.Parallel.iter_IO (fun item -> item.Delete())                     
                with
                | _ 
                    ->
                    ()
                    //runIO (postToLog <| pathToDir <| "#40-1")
                    //proste se nic nestane, tak se nesmazou, no...
        )  
        
    let internal deleteOld () = //Async.Catch is in App.fs

        IO (fun () 
                ->  
                let dirInfo = DirectoryInfo oldTimetablesPath

                match dirInfo.Exists with //TOCTOU race condition does not have any impact on the code logic here
                | true
                    -> 
                    deleteAllODISDirectories >> runIO <| oldTimetablesPath |> ignore<Result<unit, JsonParsingAndPdfDownloadErrors>>
                    dirInfo.Delete()
                | false 
                    ->
                    ()    
        )
                          
    let internal deleteOld4 () = //Async.Catch is in App.fs

        IO (fun () //Time-of-Check-To-Time-Of-Use TOCTOU   
                ->  
                let dirInfo = DirectoryInfo oldTimetablesPath4

                match dirInfo.Exists with //TOCTOU race condition does not have any impact on the code logic here
                | true
                    -> 
                    deleteAllODISDirectories >> runIO <| oldTimetablesPath4 |> ignore<Result<unit, JsonParsingAndPdfDownloadErrors>>

                    runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I2) oldTimetablesPath4 |> ignore<Result<unit, MHDErrors>>
                    runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) oldTimetablesPath4 |> ignore<Result<unit, MHDErrors>>

                    dirInfo.Delete()
                | false 
                    ->
                    ()             
        )
      
    let internal createFolders dirList =  
        IO (fun () 
                ->  
                try
                    dirList
                    |> List.iter
                        (fun (dir : string) 
                            ->                
                            match dir.Contains currentValidity || dir.Contains longTermValidity with 
                            | true  ->    
                                    sortedLines 
                                    |> List.iter
                                        (fun item
                                            -> 
                                            let dir = dir.Replace("_vyluk", sprintf "%s/%s" "_vyluk" item)
                                            Directory.CreateDirectory dir |> ignore<DirectoryInfo>
                                        )           
                            | false -> 
                                    Directory.CreateDirectory dir |> ignore<DirectoryInfo>           
                        ) 
                    |> Ok
    
                with 
                | ex
                    ->
                    runIO (postToLog <| string ex.Message <| "#041")
                    Error <| PdfError CreateFolderError4   
        )
        
    let internal ensureMainDirectoriesExist permissionGranted =

        IO (fun () 
                ->  
                match permissionGranted with
                | true 
                    ->
                    try
                        [
                            partialPathJsonTemp 
                            kodisPathTemp 
                            kodisPathTemp4 
                            dpoPathTemp 
                            mdpoPathTemp
                            oldTimetablesPath
                            oldTimetablesPath4
                        ]        
                        |> List.iter
                            (fun pathDir 
                                -> 
                                // If the directory already exists, nothing happens — no exception, no overwrite, no change.
                                Directory.CreateDirectory pathDir |> ignore<DirectoryInfo>
                            )
                        |> Ok  
                    with 
                    | ex
                        ->
                        runIO (postToLog <| string ex.Message <| "#042")
                        Error <| PdfError CreateFolderError4   
                | false 
                    -> 
                    Error <| PdfError NoPermissionError //jen quli dodrzeni typu, neni tra robit vubec nic
        )

    let internal createTP_Canopy_Folder pathDir = 

        IO (fun () 
                ->  
                try   
                    // If the directory already exists, nothing happens — no exception, no overwrite, no change.
                    Directory.CreateDirectory pathDir
                    |> ignore<DirectoryInfo>              
                    |> Ok  
                with 
                | ex
                    ->
                    runIO (postToLog <| string ex.Message <| "#421")
                    Error <| PdfError CreateFolderError2   
        )

    let internal moveFolders source destination err1 err2 =

        IO (fun ()
                ->
                result
                    {
                        try
                            Directory.CreateDirectory destination |> ignore<DirectoryInfo>
                        with 
                        | ex
                            ->
                            runIO (postToLog (string ex.Message) "#444-create-dest")
                            return ()
    
                        do!
                            runFreeMonad
                            <| 
                            copyOrMoveFiles { source = source; destination = destination } Move
                            |> Result.mapError 
                                (fun moveErr
                                    ->
                                    //runIO (postToLog moveErr "#444-move-files")
                                    err2  
                                )
    
                        return ()
                    }
                |> Result.mapError 
                    (fun finalErr 
                        ->
                        //runIO (postToLog (string finalErr) "#444-final-error")
                        finalErr
                    )  
    )