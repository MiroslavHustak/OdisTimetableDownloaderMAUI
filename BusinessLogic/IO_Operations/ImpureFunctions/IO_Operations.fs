namespace IO_Operations

open System.IO
open System.Threading
open System.Collections

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

#if ANDROID
open Android.OS
#endif

module IO_Operations =    
    
    let internal deleteAllODISDirectories pathToDir = 

        IO (fun () 
                ->  
                let deleteIt : Reader<string list, Result<unit, ParsingAndDownloadingErrors>> = 
        
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
                                        |> List.Parallel.iter_IO_AW (fun item -> item.Delete true)
                                        |> Ok
                                        //smazeme pouze adresare obsahujici stare JR, ostatni ponechame              
                                with 
                                | :? System.IO.DirectoryNotFoundException
                                    ->
                                    Ok ()   // nothing to delete 
                                | ex 
                                    ->
                                    runIO (postToLog <| string ex.Message <| "#0001-IO") 
                                    Error <| PdfDownloadError2 FileDeleteError
                        }
    
                deleteIt listOfODISVariants  
        )

    let internal deleteOneODISDirectory variant pathToDir =
    
        //smazeme pouze jeden adresar obsahujici stare JR, ostatni ponechame

        IO (fun () 
                ->  
                let deleteIt : Reader<string list, Result<unit, ParsingAndDownloadingErrors>> =  
    
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
                                        |> List.Parallel.iter_IO_AW (fun item -> item.Delete true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce  
                                        |> Ok               
                                    
                                with 
                                | :? System.IO.DirectoryNotFoundException
                                    ->
                                    Ok ()   // nothing to delete 
                                | ex
                                    ->
                                    runIO (postToLog <| string ex.Message <| "#0002-IO") 
                                    Error <| PdfDownloadError2 FileDeleteError                       
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
                        |> List.Parallel.iter_IO_AW (fun item -> item.Delete true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce  
                        |> Ok
                with
                | :? System.IO.DirectoryNotFoundException
                    ->
                    Ok ()   // nothing to delete 
                | ex
                    ->
                    runIO (postToLog <| string ex.Message <| "#0003-IO") 
                    Error FileDeleteErrorMHD //dpoMsg1  
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
                        |> List.Parallel.iter_IO_AW (fun item -> item.Delete())   
                with
                | :? System.IO.DirectoryNotFoundException
                    ->
                    ()   // nothing to delete 
                | ex
                    ->
                    () //runIO (postToLog <| string ex.Message <| "#0004-IO") 
                    //proste se nic nestane, tak se nesmazou, no...
        )  
        
    let internal deleteOld () = 

        IO (fun () 
                ->  
                try
                    let dirInfo = DirectoryInfo oldTimetablesPath

                    deleteAllODISDirectories >> runIO <| oldTimetablesPath |> ignore<Result<unit, ParsingAndDownloadingErrors>>
                    dirInfo.Delete()

                    (*
                    //Time-of-Check-To-Time-Of-Use TOCTOU   
                    match dirInfo.Exists with //TOCTOU race condition does not have any impact on the code logic here
                    | true
                        -> 
                        deleteAllODISDirectories >> runIO <| oldTimetablesPath |> ignore<Result<unit, JsonParsingAndPdfDownloadErrors>>
                        dirInfo.Delete()
                    | false 
                        ->
                        ()
                    *)
                with
                | :? System.IO.DirectoryNotFoundException
                    ->
                    ()   // nothing to delete 
                | ex
                    ->
                    runIO (postToLog <| string ex.Message <| "#0005-IO") 
        )
                          
    let internal deleteOld4 () = //Async.Catch is in App.fs

        IO (fun () 
                ->  
                try
                    let dirInfo = DirectoryInfo oldTimetablesPath4

                    deleteAllODISDirectories >> runIO <| oldTimetablesPath4 |> ignore<Result<unit, ParsingAndDownloadingErrors>>

                    runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I2) oldTimetablesPath4 |> ignore<Result<unit, MHDErrors>>
                    runIO <| deleteOneODISDirectoryMHD (ODIS_Variants.board.board I2 I3) oldTimetablesPath4 |> ignore<Result<unit, MHDErrors>>

                    dirInfo.Delete()

                    (*
                    //Time-of-Check-To-Time-Of-Use TOCTOU   
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
                    *)
                with
                | :? System.IO.DirectoryNotFoundException
                    ->
                    ()   // nothing to delete 
                | ex
                    ->
                    runIO (postToLog <| string ex.Message <| "#0006-IO")  
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
                    runIO (postToLog <| string ex.Message <| "#0007-IO") 
                    Error <| PdfDownloadError2 CreateFolderError4   
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
                        runIO (postToLog <| string ex.Message <| "#0008-IO") 
                        Error <| PdfDownloadError2 CreateFolderError4   
                | false 
                    -> 
                    Error <| PdfDownloadError2 NoPermissionError //jen quli dodrzeni typu, neni tra robit vubec nic
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
                    runIO (postToLog <| string ex.Message <| "#0009-IO") 
                    Error <| PdfDownloadError2 CreateFolderError2   
        )

    let private moveFoldersAndroid11Plus source destination err1 err2 =
        
        IO (fun () 
                ->
                let ensureSource () =
                    try
                        // safest existence check is to *touch* the directory
                        Directory.EnumerateFileSystemEntries source |> ignore<IEnumerable>
                        Ok ()
                    with 
                    | ex
                        ->
                        runIO (postToLog <| string ex.Message <| "#0010-IO") 
                        Error err1 //LetItBe...
    
                let ensureDestination () =
                    try
                        let dir = Directory.CreateDirectory destination
                        Thread.Sleep 1000 //stabilization time
                        Ok dir
                    with
                    | ex 
                        ->
                        runIO (postToLog <| string ex.Message <| "#00011-IO") 
                        Error err2
    
                result
                    {
                        match ensureSource () with
                        | Ok ()
                            -> ()
                        | Error err 
                            -> return! Error err
    
                        match ensureDestination () with
                        | Ok _ 
                            -> ()
                        | Error err
                            -> return! Error err
    
                        match runFreeMonad (copyOrMoveFiles { source = source; destination = destination } Move) with
                        | Ok _ 
                            ->
                            return ()
                        | Error moveErr
                            ->
                            runIO (postToLog <| string moveErr <| "#0012-IO") 
                            return! Error err2
                    }
                |> Result.mapError
                    (fun finalErr 
                        ->
                        runIO (postToLog <| string finalErr <| "#0013-IO") 
                        finalErr
                    )
        )    

    let private moveFoldersAndroid7_1 source destination err1 err2 = //7.1 bez Exists nefunguje, TOCTOU zatim neni problem
        
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
                                                            runIO (postToLog <| string err2 <| "#0014-IO") 
                                                            Error err2
                                                let! _ =
                                                    runFreeMonad
                                                    <|
                                                    copyOrMoveFiles { source = source; destination = destination } Move,
                                                        fun _ 
                                                            ->
                                                            runIO (postToLog <| string err2 <| "#0015-IO") 
                                                            Error err2
                                    
                                                return Ok ()
                                            }                                 
                                    with 
                                    | ex 
                                        ->
                                        runIO (postToLog <| string ex.Message <| "#0016-IO") 
                                        Error err2                       
                   
                        let! _ = 
                            runFreeMonad 
                            <| 
                            copyOrMoveFiles { source = source; destination = destination } Move,   
                                fun err
                                    ->
                                    runIO (postToLog <| string err <| "#0017-IO") 
                                    Error err2
                                 
                        return Ok ()
                    } 
        )

    let internal moveFolders source destination err1 err2 : IO<Result<unit, 'a>>= 

        try
            #if ANDROID 
            let isAtLeastAndroid11 = int Build.VERSION.SdkInt >= 30
            #else
            let isAtLeastAndroid11 = true
            #endif

            match isAtLeastAndroid11 with   
            | true  -> moveFoldersAndroid11Plus source destination err1 err2
            | false -> moveFoldersAndroid7_1 source destination err1 err2   
        with
        | ex 
            ->
            IO (fun () 
                    ->  
                    runIO (postToLog <| string ex.Message <| "#0018-IO") 
                    Error err2   
            )   

    let internal moveAll configKodis (token : CancellationToken)=  
    
        IO (fun () 
                ->
                let normaliseAsyncResult (token : CancellationToken) (a : Async<Result<'a, PdfDownloadErrors>>) =

                    async 
                        {
                            try
                                token.ThrowIfCancellationRequested()
                                let! r = a
                                return r |> Result.mapError List.singleton
                            with
                            | ex                                 
                                ->
                                runIO (postToLog <| string ex.Message <| "#0019-IO")
                                return Error [ FolderMovingError4 ]
                        }

                // Kdyz se move nepovede, tak se vubec nic nedeje, proste nebudou starsi soubory,
                // nicmene priprava na zpracovani err je provedena  
                let moveTask1 () = 
                    async
                        {
                            let!_ = runIOAsync <| moveFolders configKodis.source1 configKodis.destination LetItBeKodis4 FolderMovingError4
                            return Ok [] 
                        }
            
                let moveTask2 () = 
                    async 
                        {    
                            let!_ = runIOAsync <| moveFolders configKodis.source2 configKodis.destination LetItBeKodis4 FolderMovingError4
                            return Ok []  
                        }
    
                let moveTask3 () = 
                    async
                        {
                            let!_ = runIOAsync <| moveFolders configKodis.source3 configKodis.destination LetItBeKodis4 FolderMovingError4
                            return Ok []  
                        }   
                    
                //runIO (postToLog <| DateTime.Now.ToString("HH:mm:ss:fff") <| "Parallel start")
                
                
                async
                    {

                        let! results = 
                            [| 
                                
                                normaliseAsyncResult token (moveTask1())
                                normaliseAsyncResult token (moveTask2())
                                normaliseAsyncResult token (moveTask3())
                            |]
                            |> Async.Parallel

                        let result1 = Array.head results
                        let result2 = Array.item 1 results               
                        let result3 = Array.last results
    
                        return
                            validation
                                {
                                    let! links1 = result1
                                    and! links2 = result2
                                    and! links3 = result3

                                    return links1 @ links2 @ links3 |> List.distinct 
                                }
                    }

                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
                |> function
                    | Ok []  -> ()
                    | result -> runIO (postToLog <| sprintf "%A" result <| "#0020-IO")   

                Ok ()  //Applicative-style validation intended for logging only              

                // runIO (postToLog <| DateTime.Now.ToString("HH:mm:ss:fff") <| "Parallel end")  
        )