namespace Helpers

open System.IO
open System.Data
open FsToolkit.ErrorHandling

//************************************************************
open DirFileHelper

open Types.Lazy_IO_Monad

open Helpers
open Helpers.Builders

module Serialization =     

    let internal serializeWithThoth (json : string) (path : string) : IO<Result<unit, string>> =

        IO (fun () 
                ->        
                let prepareJsonAsyncWrite () = // it only prepares an asynchronous operation that writes the json string
      
                    try  
                        pyramidOfDoom //nelze option CE (TODO: look at the definition code to find out why)
                            {                   
                                //pouze pro moji potrebu, nepotrebuju znat chyby chyb ....
                                let! path = Path.GetFullPath path |> Option.ofNullEmpty, None  
                                               
                                let pathOption =
                                    File.Exists path
                                    |> Option.fromBool path
                                    |> Option.orElseWith 
                                        (fun ()
                                            ->
                                            File.WriteAllText(path, jsonEmpty)
                                            Some path
                                        ) 

                                let! path = pathOption, None 
                                                             
                                let writer = new StreamWriter(path, false)                
                                let!_ = writer |> Option.ofNull, None
                                                                                 
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
        )