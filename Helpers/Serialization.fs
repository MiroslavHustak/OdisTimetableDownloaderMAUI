namespace Helpers

open System.IO

open FsToolkit.ErrorHandling

//************************************************************

open Helpers
open Types.Haskell_IO_Monad_Simulation

module Serialization =   
   
    let internal serializeWithThothAsync (json: string) (path: string) : IO<Async<Result<unit, string>>> =

        IO (fun () 
                ->
                asyncResult 
                    {
                        let! fullPath = SafeFullPath.safeFullPathResult >> runIO <| path    
                        use writer = new StreamWriter(fullPath, append = false)    
                        return! 
                            writer.WriteAsync json
                            |> Async.AwaitTask
                            |> Async.map Ok //see my Excel -> DB templates for explanation  
                    }
                |> AsyncResult.catch (fun ex -> string ex.Message)
        )

    // Sync
    let internal serializeWithThothSync (json : string) (path :  string) : IO<Result<unit, string>> =
    
        IO (fun ()
                ->
                try      
                    result 
                        {
                            let! path = SafeFullPath.safeFullPathResult >> runIO <| path                               
                            use writer = new StreamWriter(path, append = false)
                            return writer.Write json   
                        }
                with
                | ex -> Error <| string ex.Message
        )