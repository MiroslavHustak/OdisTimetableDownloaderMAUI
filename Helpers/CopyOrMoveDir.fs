namespace Helpers


open System
open System.IO

open Builders
open Types.Haskell_IO_Monad_Simulation

// In this solution, source and destination values are null-checked and validated in the free monad.
// Ensure proper checking and validation when used elsewhere.

module MoveDir =

    let private prepareMoveEntireFolder source =

        try
            Path.GetFullPath source
            |> Option.ofNullEmpty
            |> Result.fromOption
        with
        | ex -> Error ex.Message

    let private prepareMoveContentOnly source =

        try
            pyramidOfInferno
                {
                    let! fullPath = 
                        Path.GetFullPath source
                        |> Option.ofNullEmpty
                        |> Result.fromOption, fun _ -> Error "Invalid path"
                    
                    let cond = fullPath.EndsWith(string Path.DirectorySeparatorChar) |> Result.fromBool fullPath String.Empty 
                    let! fullPath = cond, fun _ -> Ok (fullPath + string Path.DirectorySeparatorChar)

                    return Ok fullPath
                }
        with 
        | ex -> Error ex.Message

    let private moveEntry source target =

        try
            match Directory.Exists source, File.Exists source with
            | true, _ 
                ->
                Directory.CreateDirectory target |> ignore<DirectoryInfo>
                Ok ()

            | _, true 
                ->
                pyramidOfInferno
                    {
                        let cond = 
                            Path.GetDirectoryName target
                            |> Option.ofNullEmpty
                            |> Result.fromOption
                        
                        let! dir = cond, fun _ -> Error "Invalid target path" 
                                
                        do Directory.CreateDirectory dir |> ignore<DirectoryInfo>

                        let cond = File.Exists target |> Result.fromBool () String.Empty
                        let! _ = cond, fun _ -> Ok <| File.Move(source, target)
                                                 
                        do File.Delete target
                        do File.Move(source, target)

                        return Ok ()
                    }
         
            | _ -> 
                Error (sprintf "Invalid path: %s" source)

        with 
        | ex -> Error ex.Message

    let internal moveDirectory source targetParent mode : IO<Result<unit, string>> =

        IO (fun ()
                ->
                pyramidOfInferno
                    {
                        let resolvePath =
                            match mode with
                            | 0 -> prepareMoveEntireFolder
                            | 1 -> prepareMoveContentOnly
                            | _ -> fun _ -> Error "Invalid mode"

                        let! preparedSource = resolvePath source, fun _ -> Error "Invalid path"

                        let! pathCombine1 = 
                            Path.Combine(targetParent, Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar)))
                            |> Option.ofNullEmpty
                            |> Result.fromOption, fun _ -> Error "Invalid path"

                        let target =
                            match mode with
                            | 0 -> pathCombine1
                            | 1 -> targetParent
                            | _ -> targetParent  

                        let relativePath entry = 
                            Path.GetRelativePath(preparedSource, entry)
                            |> Option.ofNullEmpty
                            |> Result.fromOption 

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
                                            let! relative = relativePath entry, fun _ -> Error "Invalid path"
                                            let! dest = pathCombine2 relative, fun _ -> Error "Invalid path"

                                            return moveEntry entry dest
                                        }
                                )
                            |> Seq.toList
                            |> List.choose (function Error e -> Some e | _ -> None)
                            |> function
                                | [] 
                                    ->
                                    match mode with
                                    | 0 ->
                                        try
                                            Ok <| Directory.Delete(preparedSource, true)
                                        with
                                        | ex -> Error ex.Message
                                    | _ ->
                                        Ok ()
                                | errors 
                                    -> Error(String.concat "; " errors)    
                    }
        )

module CopyDir =

    let private prepareMoveEntireFolder source =

        try
            Path.GetFullPath source
            |> Option.ofNullEmpty
            |> Result.fromOption
        with
        | ex -> Error ex.Message
           
    let private prepareMoveContentOnly source =

        try
            pyramidOfInferno
                {
                    let! fullPath = 
                        Path.GetFullPath source
                        |> Option.ofNullEmpty
                        |> Result.fromOption, fun _ -> Error "Invalid path"
                    
                    let cond = fullPath.EndsWith(string Path.DirectorySeparatorChar) |> Result.fromBool fullPath String.Empty 
                    let! fullPath = cond, fun _ -> Ok (fullPath + string Path.DirectorySeparatorChar)

                    return Ok fullPath
                }
        with 
        | ex -> Error ex.Message
    
    let private copyFile source (destination : string) overwrite =

        try
            Path.GetDirectoryName destination
            |> Option.ofNullEmpty
            |> Result.fromOption
            |> Result.bind 
                (fun dir 
                    -> 
                    do Directory.CreateDirectory dir |> ignore<DirectoryInfo>
                    
                    match File.Exists destination, overwrite with
                    | true, true
                        -> Ok <| File.Copy(source, destination, true)
                    | false, _   
                        -> Ok <| File.Copy(source, destination, false)
                    | _ 
                        -> Ok ()
                )           
        with 
        | ex -> Error ex.Message

    let internal copyDirectory source targetParent mode overwriteOption =
    
        IO (fun ()
                ->
                pyramidOfInferno
                    {
                        let resolvePath =
                            match mode with
                            | 0 -> prepareMoveEntireFolder
                            | 1 -> prepareMoveContentOnly
                            | _ -> fun _ -> Error "Invalid mode"

                        let! preparedSource = resolvePath source, fun _ -> Error "Invalid path"

                        let overwrite =
                            match overwriteOption with
                            | 0 | 1 -> true
                            | _     -> false

                        let! pathCombine1 = 
                            Path.Combine(targetParent, Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar)))
                            |> Option.ofNullEmpty
                            |> Result.fromOption, fun _ -> Error "Invalid path"

                        let target =
                            match mode with
                            | 0 -> pathCombine1
                            | 1 -> targetParent
                            | _ -> targetParent  

                        let relativePath file = 
                            Path.GetRelativePath(preparedSource, file)
                            |> Option.ofNullEmpty
                            |> Result.fromOption 

                        let pathCombine2 relative = 
                            Path.Combine(target, relative)
                            |> Option.ofNullEmpty
                            |> Result.fromOption    

                        do Directory.CreateDirectory target |> ignore<DirectoryInfo>

                        return 
                            Directory.EnumerateFiles(preparedSource, "*", SearchOption.AllDirectories)
                            |> Seq.map
                                (fun file
                                    ->
                                    pyramidOfInferno
                                        {
                                            let! relative = relativePath file, fun _ -> Error "Invalid path"
                                            let! dest = pathCombine2 relative, fun _ -> Error "Invalid path"

                                            return copyFile file dest overwrite
                                        }
                                )
                            |> Seq.toList
                            |> List.choose (function Error e -> Some e | _ -> None)
                            |> function
                                | [] 
                                    -> Ok ()
                                | errors 
                                    -> Error(String.concat "; " errors)
                    }
            )