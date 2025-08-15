namespace Helpers

open System
open System.IO

open Builders
open Types.Haskell_IO_Monad_Simulation

// In this solution, source and destination values are null-checked and validated in the free monad.
// Ensure proper checking and validation when used elsewhere.

module private PathHelpers =

    type PathKind =
        | Dir
        | File
        | Missing

    let preparePath (what : string) (path : string) : Result<string, string> =

        match Option.ofNullEmptySpace path with
        | None 
            -> 
            Error <| sprintf "%s path is null or empty." what
        | Some p 
            ->
            try
                Ok (Path.GetFullPath p)
            with 
            | ex 
                ->
                Error <| sprintf "%s path is invalid: %s" what (string ex.Message)
 
    let detectPathKind (fullPath : string) : Result<PathKind, string> =
       
        try
            let dirExists = Directory.Exists fullPath
            let fileExists = File.Exists fullPath

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
            Ok (Directory.Delete(dir, true))
        with
        | ex -> Error ex.Message

    let private prepareMoveFolder source = PathHelpers.preparePath "Source" source

    let private moveFile (source : string) (target : string) : Result<unit, string> =

        try
            match (File.Exists target) with
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
                    pyramidOfInferno
                        {
                            let! dir =
                                Path.GetDirectoryName target
                                |> Option.ofNullEmpty
                                |> Result.fromOption, fun err -> Error err
                            
                            Directory.CreateDirectory dir |> ignore<DirectoryInfo>
                            
                            let! _ = moveFile fullSource target, fun err -> Error err
                            
                            return Ok ()
                        }

                | Ok PathHelpers.Missing 
                    ->
                    Error (sprintf "Invalid path (does not exist): %s" fullSource)

                | Error e 
                    -> Error e

            | Error e 
                -> Error e

        with
        | ex -> Error ex.Message

    let internal moveDirectory source targetParent : IO<Result<unit, string>> =

        let relativePath basePath entry =

            try
                Path.GetRelativePath(basePath, entry)
                |> Option.ofNullEmpty
                |> Result.fromOption
            with 
            | ex -> Error (sprintf "Failed getting relative path: %s" <| string ex.Message)

        let moveAllEntries preparedSource target =

            Directory.EnumerateFileSystemEntries(preparedSource, "*", SearchOption.AllDirectories)
            |> Seq.map 
                (fun entry 
                    ->
                    pyramidOfInferno
                        {
                            let! relative = relativePath preparedSource entry, fun err -> Error err
                            let! dest =
                                Path.Combine(target, relative)
                                |> Option.ofNullEmpty
                                |> Result.fromOption, fun err -> Error err
                            return moveEntry entry dest 
                        }
                )
            |> Seq.toList

        IO (fun () 
                ->
                let tryMove () =

                    pyramidOfInferno
                        {
                            let! preparedSource = prepareMoveFolder source, fun err -> Error err

                            let! targetDirName =
                                Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar))
                                |> Option.ofNullEmpty
                                |> Result.fromOption, fun err -> Error err

                            let target = Path.Combine(targetParent, targetDirName)

                            Directory.CreateDirectory target |> ignore<DirectoryInfo>

                            let moveResults = moveAllEntries preparedSource target

                            let errors =
                                moveResults
                                |> List.choose (function Error e -> Some e | _ -> None)

                            match errors with
                            | []   -> return safeDeleteDirectory preparedSource // Delete source directory after successful move
                            | errs -> return Error (String.concat "; " errs)
                        }

                try
                    tryMove ()
                with 
                | ex -> Error ex.Message
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

                            match (File.Exists destination, overwrite) with
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
        | ex -> Error ex.Message

    let internal copyDirectory source targetParent mode overwriteOption =

        let relativePath basePath entry =

            try
                Path.GetRelativePath(basePath, entry)
                |> Option.ofNullEmpty
                |> Result.fromOption
            with 
            | ex -> Error (sprintf "Failed getting relative path: %s" <| string ex.Message)

        IO (fun ()
                ->
                try
                    pyramidOfInferno
                        {
                            let resolvePath =
                                match mode with
                                | 0 -> prepareMoveEntireFolder
                                | 1 -> prepareMoveContentOnly
                                | _ -> fun _ -> Error "Invalid mode"

                            let! preparedSource = resolvePath source, fun err -> Error err

                            let! pathCombine1 =
                                Path.Combine(targetParent, Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar)))
                                |> Option.ofNullEmpty
                                |> Result.fromOption, fun err -> Error err

                            let target =
                                match mode with
                                | 0 -> pathCombine1
                                | 1 -> targetParent
                                | _ -> targetParent                       

                            let pathCombine2 relative =
                                Path.Combine(target, relative)
                                |> Option.ofNullEmpty
                                |> Result.fromOption

                            Directory.CreateDirectory target |> ignore<DirectoryInfo>

                            return
                                Directory.EnumerateFileSystemEntries(preparedSource, "*", SearchOption.AllDirectories)
                                |> Seq.map 
                                    (fun entry 
                                        ->
                                        pyramidOfInferno 
                                            {
                                                let! relative = relativePath preparedSource entry, fun err -> Error err
                                                let! dest = pathCombine2 relative, fun err -> Error err

                                                return copyEntry entry dest overwriteOption
                                            }
                                    )
                                |> Seq.toList
                                |> List.choose (function Error e -> Some e | _ -> None)
                                |> function
                                    | []     -> Ok ()
                                    | errors -> Error (String.concat "; " errors)
                        }
                with
                | ex -> Error ex.Message
        )
