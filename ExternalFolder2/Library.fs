namespace FSharpHelpers

open System.IO

// In this solution, source and destination values are null-checked and validated in the free monad.
// Ensure proper checking and validation when used elsewhere.

type internal IO<'a> = IO of (unit -> 'a) 

module MoveDir =  

    let private prepareMoveEntireFolder (source : string) : Result<string, string> =

        try
            Path.GetFullPath source |> Ok
        with 
        | ex -> Error ex.Message

    let private prepareMoveContentOnly (source : string) : Result<string, string> =

        try
            let full = Path.GetFullPath source
            match full.EndsWith(string Path.DirectorySeparatorChar) with
            | true  -> Ok full
            | false -> Ok (sprintf "%s%s" full (string Path.DirectorySeparatorChar))
        with 
        | ex -> Error ex.Message

    let private moveEntry (source : string) (target : string) : Result<unit, string> =

        try
            let isDir = Directory.Exists source
            let isFile = File.Exists source

            match (isDir, isFile) with
            | (true, _) 
                ->
                Directory.CreateDirectory target |> ignore<DirectoryInfo>
                Ok ()

            | (_, true)
                ->
                Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore<DirectoryInfo>
                File.Move(source, target)
                Ok ()

            | _
                ->
                Error <| sprintf "Invalid path: %s" source

        with 
        | ex -> Error ex.Message
    
    let internal runIO (IO action) = action ()

    /// Move logic like C++ version
    let internal moveDirectory (source : string) (targetParent : string) (mode : int) =

        IO (fun () 
                -> 
                match mode with
                | 0 | 1 
                    ->
                    let pathResult =
                        match mode with
                        | 0 -> prepareMoveEntireFolder source
                        | 1 -> prepareMoveContentOnly source
                        | _ -> Error "Invalid mode"

                    pathResult
                    |> Result.bind 
                        (fun preparedSource 
                            ->
                            let target =
                                match mode with
                                | 0 -> Path.Combine(targetParent, Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar)))
                                | 1 -> targetParent
                                | _ -> targetParent

                            Directory.CreateDirectory target |> ignore<DirectoryInfo>

                            Directory.EnumerateFileSystemEntries(preparedSource, "*", SearchOption.AllDirectories)                    
                            |> Seq.map 
                                (fun entry
                                    ->
                                    let relativePath = Path.GetRelativePath(preparedSource, entry)
                                    let destPath = Path.Combine(target, relativePath)
                                    moveEntry entry destPath
                                )
                            |> Seq.toList
                            |> List.choose (function | Error e -> Some e | _ -> None)
                            |> function
                                | [] 
                                    ->
                                    match mode with
                                    | 0 ->
                                        try
                                            Directory.Delete(preparedSource, true)
                                            Ok ()
                                        with 
                                        | ex -> Error ex.Message
                                    |_  -> 
                                        Ok ()
                                | errors 
                                    ->
                                    Error <| String.concat "; " errors
                        )
                | _ 
                    ->
                    Error "Invalid mode"
        )

module CopyDir =

    let private prepareMoveEntireFolder (source : string) : Result<string, string> =
    
        try
            Path.GetFullPath source |> Ok
        with 
        | ex -> Error ex.Message
    
    let private prepareMoveContentOnly (source : string) : Result<string, string> =
    
        try
            let full = Path.GetFullPath source
            match full.EndsWith(string Path.DirectorySeparatorChar) with
            | true  -> Ok full
            | false -> Ok (sprintf "%s%s" full (string Path.DirectorySeparatorChar))
        with 
        | ex -> Error ex.Message
    
    let private copyFile (source : string) (destination : string) (overwrite : bool) : Result<unit, string> =

        try
            Directory.CreateDirectory (Path.GetDirectoryName destination) |> ignore<DirectoryInfo>
            File.Copy(source, destination, overwrite)
            Ok ()
        with 
        | ex -> Error ex.Message

    let internal runIO (IO action) = action ()

    /// Copy logic like C++ version
    let internal copyDirectory (source : string) (targetParent : string) (mode : int) (overwriteOption : int) =
        
        IO (fun () 
                -> 
                let getCopyPath =
                    match mode with
                    | 0 -> prepareMoveEntireFolder
                    | 1 -> prepareMoveContentOnly
                    | _ -> fun _ -> Error "Invalid mode"

                let overwrite =
                    match overwriteOption with
                    | 0 -> true // Overwrite all
                    | 1 -> true // Overwrite old only - simplified
                    | _ -> false

                getCopyPath source
                |> Result.bind 
                    (fun preparedSource
                        ->
                        let target = Path.Combine(targetParent, Path.GetFileName(preparedSource.TrimEnd(Path.DirectorySeparatorChar)))
                
                        Directory.CreateDirectory target |> ignore<DirectoryInfo>

                        let entries = Directory.EnumerateFiles(preparedSource, "*", SearchOption.AllDirectories)
                        entries
                        |> Seq.map
                            (fun file 
                                ->
                                let relative = Path.GetRelativePath(preparedSource, file)
                                let destFile = Path.Combine(target, relative)
                                copyFile file destFile overwrite
                            )
                        |> Seq.toList
                        |> List.choose (function | Error e -> Some e | _ -> None)
                        |> function
                            | []  
                                -> Ok ()
                            | errors 
                                -> Error(String.concat "; " errors)
                    )
        )