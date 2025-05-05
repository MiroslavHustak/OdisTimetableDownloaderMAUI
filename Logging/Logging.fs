namespace Api

open System
open System.IO
open System.Net

//************************************************************

open FsHttp
open Thoth.Json.Net

open FsToolkit
open FsToolkit.ErrorHandling

//************************************************************

open Types
open Types.ErrorTypes
open Settings.SettingsGeneral

open LogEntries

open Helpers
open Helpers.Builders


module Logging = 

    [<Struct>]
    type internal ResponsePost = 
        {
            Message1 : string
            Message2 : string
        }

    let [<Literal>] private maxFileSizeKb = 512L // Maximum file size in kilobytes 

    let private decoderPost : Decoder<ResponsePost> =  //zatim zpetny message neni treba, ale ponechavam pro potencialni pouziti

        Decode.object
            (fun get
                ->
                {
                    Message1 = get.Required.Field "Message1" Decode.string
                    Message2 = get.Required.Field "Message2" Decode.string
                }
            )

    let internal postToLogFile errorMessage = 

        //prima transformace na json string (bez pouziti records / serializace / Thoth encoders )
        let s1 = "{ \"list\": ["
        let s2 = [ errorMessage; string DateTimeOffset.UtcNow ] |> List.map (sprintf "\"%s\"") |> String.concat ","
        let s3 = "] }"

        let jsonPayload = sprintf "%s%s%s" s1 s2 s3   
       
        async
            {       
                try
                    let! response = 
                        http
                            {
                                POST urlLogging
                                header "X-API-KEY" apiKeyTest 
                                body 
                                json jsonPayload
                            }
                        |> Request.sendAsync       
                                            
                    match response.statusCode with
                    | HttpStatusCode.OK 
                        -> 
                        let! jsonMsg = Response.toTextAsync response
    
                        return                          
                            Decode.fromString decoderPost jsonMsg   
                            |> function
                                | Ok value  -> value   
                                | Error err -> { Message1 = String.Empty; Message2 = err }      
                    | _ -> 
                        return { Message1 = String.Empty; Message2 = sprintf "Request failed with status code %d" (int response.statusCode) }    
                
                //Result type nema smysl u log files pro errors
                with
                | ex -> return { Message1 = String.Empty; Message2 = sprintf "Request failed with error message %s" (string ex.Message) }     
            } 

    //*************************************************************************** 
    #if WINDOWS   

    let internal saveJsonToFile () : Result<unit, string> =

        let prepareJsonAsyncAppend () =

            try                 
                pyramidOfDoom
                    {  
                        let url = "http://kodis.somee.com/api/logging"

                        //pouze pro moji potrebu, nepotrebuju znat chyby chyb ....
                        let! filepath = (Path.GetFullPath logFileName) |> Option.ofNullEmpty, Error String.Empty

                        let logEntries = 
                            async { return! getLogEntriesFromRestApi url } 
                            |> Async.RunSynchronously
                            |> function Ok logEntries -> logEntries | Error _ -> "Chyba při čtení logEntries z API"   
                   
                        let writer = new StreamWriter(filepath, true) // Append mode
                        let! _ = writer |> Option.ofNull, Error String.Empty
    
                        return Ok (writer, logEntries)  
                    }
                |> Result.map
                    (fun (writer, logEntries) 
                        ->
                        async
                            {
                                do! writer.WriteLineAsync logEntries |> Async.AwaitTask
                                do! writer.FlushAsync() |> Async.AwaitTask
    
                                return! writer.DisposeAsync().AsTask() |> Async.AwaitTask
                            }
                    )
            with
            | ex -> Error (string ex.Message)
    
        let checkFileSize () =
            
            try
                (Path.GetFullPath logFileName)
                |> Option.ofNullEmpty 
                |> Option.map
                    (fun path
                        ->                   
                        let fileInfo = FileInfo path
            
                        let sizeKb = 
                            match fileInfo.Exists with
                            | true  -> fileInfo.Length / 1024L  //abychom dostali hodnotu v KB
                            | false -> 0L
                        
                        match (<) sizeKb <| int64 maxFileSizeKb with
                        | true  -> ()
                        | false -> fileInfo.Delete()
            
                        sizeKb
                    )
            
            with
            | _ -> None
    
        async
            {
                try
                    match checkFileSize () with
                    | Some _
                        ->
                        match prepareJsonAsyncAppend () with
                        | Ok asyncWriter
                            ->
                            do! asyncWriter
                            return Ok ()

                        | Error _
                            ->
                            return Error String.Empty

                    | None
                        ->
                        return Error String.Empty
                with
                | _ -> return Error String.Empty
            }
        |> Async.RunSynchronously

    #endif