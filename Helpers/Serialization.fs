namespace Helpers

open System.IO
open System.Data
open FsToolkit.ErrorHandling

//************************************************************

open Helpers
open Helpers.Builders

module Serialization =     
    
    //filepath -> musim tam mit alespon prazdny json soubor, netvori se automaticky, pokud neexistuje
    let internal serializeWithThoth (json : string) (path : string) =   
        
        let prepareJsonAsyncWrite () = // it only prepares an asynchronous operation that writes the json string
      
            try  
                pyramidOfDoom
                    {
                        let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, None 

                        let fInfodat : FileInfo = FileInfo filepath
                        do! fInfodat.Exists |> Option.ofBool, None
                                                             
                        let writer = new StreamWriter(filepath, false)                
                        let!_  = writer |> Option.ofNull, None
                                                                                 
                        return Some writer
                    }         
                      
                |> Option.map 
                    (fun (writer : StreamWriter) 
                        ->
                        async
                            {
                                use writer = writer
                                do! writer.WriteAsync json |> Async.AwaitTask

                                return! writer.FlushAsync() |> Async.AwaitTask
                            }
                    )
            with
            | _ -> None

        async
            {
                try    
                    match prepareJsonAsyncWrite () with
                    | Some asyncWriter     
                        ->
                        do! asyncWriter    
                        return Ok ()

                    | None
                        ->                              
                        return Error "StreamWriter Error" 
                with
                | ex -> return Error (string ex.Message) 
            }   
        |> Async.RunSynchronously 
           


