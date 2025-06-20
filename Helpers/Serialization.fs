﻿namespace Helpers

open System.IO
open System.Data
open FsToolkit.ErrorHandling

//************************************************************
open FileInfoHelper

open Helpers
open Helpers.Builders

module Serialization =     
    
    let internal serializeWithThoth (json : string) (path : string) =   
        
        let prepareJsonAsyncWrite () = // it only prepares an asynchronous operation that writes the json string
      
            try  
                option
                    {                   
                        //pouze pro moji potrebu, nepotrebuju znat chyby chyb ....
                        let! path = Path.GetFullPath path  |> Option.ofNullEmpty  

                        let path =  
                            match File.Exists path with
                            | true  -> 
                                    path
                            | false ->
                                    File.WriteAllText(path, jsonEmpty)
                                    path
                                                             
                        let writer = new StreamWriter(path, false)                
                        do! writer |> Option.ofNull
                                                                                 
                        return writer
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