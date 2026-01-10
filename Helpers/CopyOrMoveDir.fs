namespace Helpers

open System
open System.IO

open FsToolkit.ErrorHandling

open IO_Monad
open Builders
open Types.Haskell_IO_Monad_Simulation

#if ANDROID
open Android.OS
#endif

// In this solution, source and destination values are null-checked and validated in the free monad.
// !!!!!!!!!!!!!!!! Ensure proper checking and validation when used elsewhere !!!!!!!!!!!!!!!!!!!

// jen castecne kontrolovano, pri publishing vse overit, copy i move

module private PathHelpers =

    type PathKind =
        | Dir
        | File
        | Missing

    #if ANDROID 
    let isAtLeastAndroid11 = int Build.VERSION.SdkInt >= 30
    #else
    let isAtLeastAndroid11 = false
    #endif

    let preparePath (context : string) (path : string) : Result<string, string> =

        match Option.ofNullEmptySpace path with
        | None 
            -> 
            Error <| sprintf "%s path is null or empty." context
        | Some p 
            ->
            try
                Ok <| Path.GetFullPath p
            with 
            | ex -> Error <| sprintf "%s path is invalid: %s" context (string ex.Message)
                 
    let detectPathKind (fullPath : string) : Result<PathKind, string> =
      
        match isAtLeastAndroid11 with
        | true 
            -> 
            match preparePath "Provided" fullPath with
            | Error e
                ->
                Error e
            | Ok normalizedPath
                ->
                try
                    let attrs = File.GetAttributes fullPath
                    match attrs &&& FileAttributes.Directory = FileAttributes.Directory with
                    | true  -> Ok Dir
                    | false -> Ok File
                with
                | :? FileNotFoundException
                | :? DirectoryNotFoundException
                    ->
                    Ok Missing
                | :? UnauthorizedAccessException 
                    ->
                    Error (sprintf "Access denied to path '%s'" normalizedPath)
                | ex
                    ->
                    Error (sprintf "Failed inspecting path '%s': %s" normalizedPath (string ex.Message))
        | false 
            ->
            try
                let dirExists = Directory.Exists fullPath // bez tohoto dir checking mobilni app pro A7.1 nefunguje, TOCTOU race zatim nebyl problem
                let fileExists = File.Exists fullPath     // bez tohoto file checking mobilni app pro A7.1 nefunguje, TOCTOU race zatim nebyl problem

                match dirExists, fileExists with
                | true, false 
                    -> 
                    Ok Dir
                | false, true 
                    ->
                    Ok File
                | true, true
                    ->
                    // extremely rare
                    try
                        let attrs = File.GetAttributes fullPath
                        match (attrs &&& FileAttributes.Directory) = FileAttributes.Directory with true -> Ok Dir | false -> Ok File
                    with 
                    | _ -> Ok Dir
                | false, false 
                    ->
                    Ok Missing
            with
            | ex -> Error (sprintf "Failed inspecting path '%s': %s" <| fullPath <| string ex.Message)

module MoveDir =
   
    let private safeDeleteDirectory dir =

        try
            Ok <| Directory.Delete(dir, true)
        with
        | ex -> Error <| string ex.Message

    let private prepareMoveFolder source = PathHelpers.preparePath "Source" source

    let private preparePath (context : string) (path : string) : Result<string, string> =

        match Option.ofNullEmptySpace path with
        | None 
            -> 
            Error <| sprintf "%s path is null or empty." context
        | Some p 
            ->
            try
                Ok <| Path.GetFullPath p
            with 
            | ex -> Error <| sprintf "%s path is invalid: %s" context (string ex.Message)

    let private moveFile (source : string) (target : string) : Result<unit, string> =

        match PathHelpers.isAtLeastAndroid11 with
        | true  
            ->
            result
                {
                    let! sourceFull = preparePath "source" source
                    let! targetFull = preparePath "target" target
    
                    match sourceFull = targetFull with
                    | true  ->
                            return ()  
                    | false ->
                            let moveAction : IO_Monad<unit> =  //tohle je lazy, takze se to neprovede hned TODO overit
                                IOMonad
                                    {
                                        return! primIO (fun () -> File.Move(sourceFull, targetFull, overwrite = true)) //overwrite = true -> fileDelete () + fileMove () 
                                    }
    
                            try
                                runIOMonad moveAction
                                return ()
                            with 
                            | ex
                                ->
                                return!
                                    Error (sprintf "Failed to move file from '%s' to '%s': %s" sourceFull targetFull (string ex.Message))
                }

            | false 
                -> 
                try
                    match (File.Exists target) with // bez tohoto file checking mobilni app pro A7.1 nefunguje, TOCTOU race zatim nebyl problem
                    | true 
                        ->
                        File.Delete target
                        File.Move(source, target)
                        Ok ()            
                    | false 
                        ->
                        File.Move(source, target)
                        Ok ()
                with 
                | ex -> Error ex.Message 

    let private moveEntry source target  =

        try
            match PathHelpers.preparePath "Source" source with            
            | Ok fullSource 
                ->
                match PathHelpers.detectPathKind fullSource with
                | Ok PathHelpers.Dir 
                    ->
                    // Just ensure target dir exists
                    Directory.CreateDirectory target |> ignore<DirectoryInfo>
                    Ok ()

                | Ok PathHelpers.File
                    ->
                    result
                        {
                            let! dir =
                                Path.GetDirectoryName target
                                |> Option.ofNullEmpty
                                |> Result.fromOption
                            
                            Directory.CreateDirectory dir |> ignore<DirectoryInfo>
                            
                            let!_ = moveFile fullSource target
                            
                            return ()
                        }

                | Ok PathHelpers.Missing 
                    ->
                    Error (sprintf "Invalid path (does not exist): %s" fullSource)

                | Error e 
                    ->
                    Error e

            | Error e 
                -> 
                Error e

        with
        | ex -> Error <| string ex.Message

    let internal moveDirectory source targetParent : IO<Result<unit, string>> =

        let relativePath basePath entry =

            try
                Path.GetRelativePath(basePath, entry)
                |> Option.ofNullEmpty
                |> Option.toResult "Failed getting relative path"
            with 
            | ex -> Error (sprintf "Failed getting relative path: %s" <| string ex.Message)

        let moveAllEntries preparedSource target =

            try
                // Eagerly enumerate entries as a list to close handles
                let entries =                
                    Directory.EnumerateFileSystemEntries(preparedSource, "*", SearchOption.AllDirectories) 
                    |> Seq.toList

                entries
                |> List.map 
                    (fun entry 
                        ->
                        result
                            {
                                let! relative = relativePath preparedSource entry
                                let! dest =
                                    Path.Combine(target, relative)
                                    |> Option.ofNullEmpty
                                    |> Option.toResult "Failed getting combined path"
                            
                                return! moveEntry entry dest 
                            }

                        (*
                        resultOption
                            {
                                let! relative = relativePath preparedSource entry
                                let! dest = Path.Combine(target, relative) |> Option.ofNullEmpty  //dostaneme Error None                          
                                return! moveEntry entry dest 
                            }
                         *)
                    )
            with
            | ex -> [ Error (sprintf "Failed enumerating entries: %s" <| string ex.Message) ] 

        IO (fun () 
                ->
                let tryMove () =

                    result
                        {
                            let! preparedSource = prepareMoveFolder source

                            let! targetDirName =
                                Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar))
                                |> Option.ofNullEmpty
                                |> Option.toResult "Failed getting file name"

                            let target = Path.Combine(targetParent, targetDirName)

                            Directory.CreateDirectory target |> ignore<DirectoryInfo>

                            let moveResults = moveAllEntries preparedSource target

                            let errors =
                                moveResults
                                |> List.choose (function Error e -> Some e | _ -> None)

                            match errors with
                            | []   -> return! safeDeleteDirectory preparedSource // Delete source directory after successful move
                            | errs -> return! Error (String.concat "; " errs)
                        }

                try
                    tryMove ()
                with 
                | ex -> Error <| string ex.Message
        )

module CopyDir =

    let private prepareMoveEntireFolder source = PathHelpers.preparePath "Source" source

    let private prepareMoveContentOnly source =

        pyramidOfInferno
            {
                let! fullPath = PathHelpers.preparePath "Source" source, fun err -> Error err
                let! fullPath =
                    fullPath.EndsWith(string Path.DirectorySeparatorChar)
                    |> Result.fromBool fullPath String.Empty
                        , fun _ -> Ok (sprintf "%s%s" fullPath (string Path.DirectorySeparatorChar))

                return Ok fullPath        
            }

    let private copyEntry source (destination : string) overwrite =
   
        match PathHelpers.isAtLeastAndroid11 with
        | true 
            ->
            result
                {
                    let! fullSource = PathHelpers.preparePath "Source" source
                    try
                        match Path.GetDirectoryName destination |> Option.ofNullEmptySpace with
                        | None 
                            -> 
                            return! Error "Destination path has no directory part."
                        | Some destDir
                            ->
                            Directory.CreateDirectory destDir |> ignore
        
                    
                            File.Copy(fullSource, destination, overwrite)
                            return ()
                    with 
                    | ex 
                        ->
                        return!
                            Error
                            <| 
                            match ex with
                            | :? FileNotFoundException 
                            | :? DirectoryNotFoundException 
                                ->
                                sprintf "Source not found or inaccessible: %s" fullSource
                            | :? UnauthorizedAccessException 
                                ->
                                sprintf "Access denied when copying '%s' to '%s'" fullSource destination
                            | _ when (string ex.Message).Contains("used by another process") || (string ex.Message).Contains("being used") 
                                ->
                                sprintf "File is in use: %s" destination
                            | _ ->
                                sprintf "Failed to copy '%s' to '%s': %s" fullSource destination (string ex.Message)                       
                }
        | false 
            ->
            let tryCopyEntry () = 
                
                match PathHelpers.preparePath "Source" source with
                           
                | Ok fullSource
                    ->
                    match PathHelpers.detectPathKind fullSource with
                    | Ok PathHelpers.Dir
                        ->
                        Directory.CreateDirectory destination |> ignore<DirectoryInfo>
                        Ok ()
                
                    | Ok PathHelpers.File
                        ->
                        Path.GetDirectoryName destination
                        |> Option.ofNullEmpty
                        |> Result.fromOption
                        |> Result.bind 
                            (fun dir 
                                ->
                                Directory.CreateDirectory dir |> ignore<DirectoryInfo>
                
                                match (File.Exists destination, overwrite) with // bez tohoto file checking mobilni app pro A7.1 nefunguje, TOCTOU race zatim nebyl problem
                                | (true, true) 
                                    ->
                                    File.Copy(fullSource, destination, true)
                                    Ok ()
                                | (false, _) 
                                    ->
                                    File.Copy(fullSource, destination, false)
                                    Ok ()
                                | (true, false) 
                                    -> 
                                    Ok ()
                            )
                
                    | Ok PathHelpers.Missing 
                        ->
                        Error (sprintf "Invalid path (does not exist): %s" fullSource)
                
                    | Error e -> Error e
                
                | Error e -> Error e
                
            try
                tryCopyEntry ()
            with 
            | ex -> Error <| string ex.Message

    let internal copyDirectory source targetParent mode overwriteOption : IO<Result<unit,string>> =

        IO (fun ()
                ->
                try
                    result
                        {
                            let resolvePath =
                                match mode with
                                | 0 -> prepareMoveEntireFolder
                                | 1 -> prepareMoveContentOnly
                                | _ -> fun _ -> Error "Invalid mode"
    
                            let! preparedSource = resolvePath source
    
                            let! target =
                                match mode with
                                | 0 ->
                                    result 
                                        {
                                            let! folderName =
                                                Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar))
                                                |> Option.ofNullEmpty
                                                |> Option.toResult "Failed getting folder name"
                    
                                            let! combined =
                                                Path.Combine(targetParent, folderName)
                                                |> Option.ofNullEmpty
                                                |> Option.toResult "Failed combining path"
                    
                                            return combined
                                        }
                                | 1 -> 
                                    Ok targetParent
                                | _ ->
                                    Error "Invalid mode"
    
                            Directory.CreateDirectory target |> ignore
    
                            // Eagerly enumerate entries as a list to close handles
                            let entries = 
                                Directory.EnumerateFileSystemEntries(preparedSource, "*", SearchOption.AllDirectories) 
                                |> Seq.toList
    
                            let errors =
                                entries
                                |> List.map
                                    (fun entry 
                                        ->
                                        result 
                                            {
                                                let! relative = 
                                                    try
                                                        Path.GetRelativePath(preparedSource, entry)
                                                        |> Option.ofNullEmpty
                                                        |> Option.toResult "Failed getting relative path"
                                                    with 
                                                    | ex -> Error (sprintf "Failed getting relative path: %s" ex.Message)
    
                                                let! dest = 
                                                    Path.Combine(target, relative)
                                                    |> Option.ofNullEmpty
                                                    |> Option.toResult "Failed getting destination path"
    
                                                return! copyEntry entry dest overwriteOption
                                            }
                                )
                                |> List.choose (function Error e -> Some e | Ok _ -> None)
    
                            return!
                                match errors with
                                | []   -> Ok ()
                                | errs -> Error <| String.concat "; " errs
                        }
                with
                | ex -> Error <| sprintf "Unexpected exception in copyDirectory: %s" (string ex.Message)
        )

module MoveDir2 = //testing Rust code implementation (for Windows Machine only)    
            
    let internal moveDirectory2 source targetParent : IO<Result<unit, string>> =

        IO (fun () 
                ->    
                try
                    match NativeHelpers.Native.rust_move_c(source, targetParent) with
                    | 0 -> Ok ()
                    | n -> Error (sprintf "Native move operation failed with code %d" n)
                with
                | ex -> Error <| string ex.Message
        )

module CopyDir2 = //testing Rust code implementation (for Windows Machine only)     
        
    let internal copyDirectory2 source targetParent =
        
        IO (fun ()
                ->
                try
                    match NativeHelpers.Native.rust_copy_c(source, targetParent) with
                    | 0 -> Ok ()
                    | n -> Error (sprintf "Native move operation failed with code %d" n)
                with
                | ex -> Error <| string ex.Message
        )