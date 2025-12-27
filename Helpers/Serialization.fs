namespace Helpers

open System.IO

//************************************************************

open FsToolkit.ErrorHandling

//************************************************************

open Helpers
open Types.Haskell_IO_Monad_Simulation

module Serialization =   

    // Async
    let internal serializeWithThothAsync (json : string) (path : string) : IO<Async<Result<unit, string>>> =
        
        IO (fun ()
                ->
                try   
                    asyncResult 
                        {
                            let! path = SafeFullPath.safeFullPathResult path                                
                            use writer = new StreamWriter(path, append = false)
                            return! writer.WriteAsync json |> Async.AwaitTask
                        }
                with
                | ex -> async { return Error <| string ex.Message }
        )

    // Sync
    let internal serializeWithThothSync (json : string) (path :  string) : IO<Result<unit, string>> =
    
        IO (fun ()
                ->
                try      
                    result 
                        {
                            let! path = SafeFullPath.safeFullPathResult path                               
                            use writer = new StreamWriter(path, append = false)
                            return writer.Write json   
                        }
                with
                | ex -> Error <| string ex.Message
        )

    // For educational purposes, not to be used in production code
    let internal serializeWithThoth (json : string) (path : string) : IO<Result<unit, string>> =

        IO (fun ()
                ->
                try      
                    asyncOption 
                        {
                            let! path = SafeFullPath.safeFullPathOption path 
                            use writer = new StreamWriter(path, append = false)
                            // Does not block the thread in the kernel during the actual disk wait 
                            // Insignificant performance help
                            return! writer.WriteAsync json |> Async.AwaitTask                               
                        }
                    |> Async.RunSynchronously 
                    |> Option.toResult "Invalid path"               
                with
                | ex -> Error <| string ex.Message
        )