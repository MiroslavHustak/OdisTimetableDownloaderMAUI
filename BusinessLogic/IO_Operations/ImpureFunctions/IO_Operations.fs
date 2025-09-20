namespace IO_Operations

open System.IO
open System.Threading

//************************************************************

open Types
open Types.Types
open Types.FreeMonad
open Types.ErrorTypes
open Types.TypeAlgebra
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
                let deleteIt : Reader<string list, Result<unit, PdfDownloadErrors>> = 
        
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
                                    runIO (postToLog <| ex.Message <| "#038")
                                    Error FileDeleteError
                        }
    
                deleteIt listOfODISVariants  
        )

    let internal deleteOneODISDirectory variant pathToDir =
    
        //smazeme pouze jeden adresar obsahujici stare JR, ostatni ponechame

        IO (fun () 
                ->  
                let deleteIt : Reader<string list, Result<unit, PdfDownloadErrors>> =  
    
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
                                    runIO (postToLog <| ex.Message <| "#039")
                                    Error FileDeleteError                       
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
                        |> Seq.filter (fun item -> item.Name = dirName) 
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

                match dirInfo.Exists with
                | true
                    -> 
                    deleteAllODISDirectories >> runIO <| oldTimetablesPath |> ignore<Result<unit, PdfDownloadErrors>>
                    dirInfo.Delete()
                | false 
                    ->
                    ()    
        )
                          
    let internal deleteOld4 () = //Async.Catch is in App.fs

        IO (fun () 
                ->  
                let dirInfo = DirectoryInfo oldTimetablesPath4

                match dirInfo.Exists with
                | true
                    -> 
                    deleteAllODISDirectories >> runIO <| oldTimetablesPath4 |> ignore<Result<unit, PdfDownloadErrors>>

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
                        (fun (dir: string) 
                            ->                
                            match dir.Contains "JR_ODIS_aktualni_vcetne_vyluk" || dir.Contains "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk" with 
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
                    runIO (postToLog <| ex.Message <| "#041")
                    Error CreateFolderError4   
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
                                match Directory.Exists pathDir with
                                | true  -> () 
                                | false -> Directory.CreateDirectory pathDir |> ignore<DirectoryInfo> 
                            )
                        |> Ok  
                    with 
                    | ex
                        ->
                        runIO (postToLog <| ex.Message <| "#042")
                        Error CreateFolderError4   
                | false 
                    -> 
                    Error NoPermissionError //jen quli dodrzeni typu, neni tra robit vubec nic
    )

    let internal createTP_Canopy_Folder pathDir = 

        IO (fun () 
                ->  
                try           
                    match Directory.Exists pathDir with
                    | true  -> () 
                    | false -> Directory.CreateDirectory pathDir |> ignore<DirectoryInfo> 
              
                    |> Ok  
                with 
                | ex
                    ->
                    runIO (postToLog <| ex.Message <| "#421")
                    Error CreateFolderError2   
        )

    let internal moveFolders source destination err1 err2 = 
    
        IO (fun () 
                ->
                pyramidOfInferno
                    {
                        let! _ = 
                            Directory.Exists source |> Result.fromBool () err1,
                                fun _ -> Ok ()   
                        
                        let! _ =
                            Directory.Exists destination |> Result.fromBool () err2,
                                fun _ 
                                    ->
                                    try
                                        pyramidOfInferno 
                                            {
                                                let! _ =    
                                                    let dirInfo = Directory.CreateDirectory destination
                                                    Thread.Sleep 1000 //wait for the directory to be created  
    
                                                    dirInfo.Exists |> Result.fromBool () err2,
                                                        fun _
                                                            ->
                                                            runIO (postToLog <| err2 <| "#444-1")
                                                            Error err2
                                                let! _ =
                                                    runFreeMonad
                                                    <|
                                                    copyOrMoveFiles { source = source; destination = destination } Move,
                                                        fun _ 
                                                            ->
                                                            runIO (postToLog <| err2 <| "#444-2")
                                                            Error err2
                                
                                                return Ok ()
                                            }                                 
                                    with 
                                    | ex 
                                        ->
                                        runIO (postToLog <| ex.Message <| "#444-3")
                                        Error err2                       
               
                        let! _ = 
                            runFreeMonad 
                            <| 
                            copyOrMoveFiles { source = source; destination = destination } Move,   
                                fun err
                                    ->
                                    runIO (postToLog <| err <| "#444-48")
                                    Error err2
                             
                        return Ok ()
                    } 
        )