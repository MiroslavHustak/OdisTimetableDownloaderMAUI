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
open Types.Haskell_IO_Monad_Simulation

open LogEntries
open Settings.SettingsGeneral

open Helpers
open Helpers.Builders
open Helpers.DirFileHelper

module Logging = 
    
    type internal ResponsePost = 
        {
            Message1 : string
            Message2 : string
        }

    let private decoderPost : Decoder<ResponsePost> =  //zatim zpetny message neni treba, ale ponechavam pro potencialni pouziti

        Decode.object
            (fun get
                ->
                {
                    Message1 = get.Required.Field "Message1" Decode.string
                    Message2 = get.Required.Field "Message2" Decode.string
                }
            )

    let internal postToLogFile () errorMessage = 

        IO (fun () 
                ->               
                //direct transformation to a json string (without records / serialization / Thoth encoders )
                let s1 = "{ \"list\": ["
                let s2 = [ errorMessage; string DateTimeOffset.UtcNow ] |> List.map (sprintf "\"%s\"") |> String.concat ","
                let s3 = "] }"

                let jsonPayload = sprintf "%s%s%s" s1 s2 s3   
       
                async
                    {       
                        try
                            use! response = 
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
                                return { Message1 = String.Empty; Message2 = sprintf "Request #247 failed with status code %d" (int response.statusCode) }    
                
                        //Result type nema smysl u log files pro errors
                        with
                        | ex -> return { Message1 = String.Empty; Message2 = sprintf "Request failed with error message %s" (string ex.Message) }     
                    } 
        )        

    let internal postToLog (msg : 'a) errCode =   

        IO (fun () 
                ->    
                try
                    runIO <| postToLogFile () (sprintf "%s Error%s" <| string msg <| errCode) 
                    |> Async.Ignore<ResponsePost>
                    |> Async.Start   
                with
                |_ -> () //kdyz nefunguje KODIS API, zhavaruje aji logfile, ktery z endpoints bere / uklada na nej message
        )

    //*************************************************************************** 
    #if WINDOWS  
    let internal saveJsonToFileAsync () =

        IO (fun () 
                ->
                asyncResult 
                    {
                        try
                            let! path = SafeFullPath.safeFullPathResult logFileName 

                            let! logEntries =
                                getLogEntriesFromRestApi >> runIO <| urlLogging
                                |> AsyncResult.mapError
                                    (fun _ -> "Chyba při čtení logEntries z KODIS API (kodis.somee)")

                            let fs =
                                new FileStream
                                    (
                                        path,
                                        FileMode.OpenOrCreate,
                                        FileAccess.Write,
                                        FileShare.None
                                    )
                            try 
                                let maxBytes = int64 maxFileSizeKb * 1024L

                                match fs.Length > maxBytes with
                                | true  -> fs.SetLength 0L  //truncating oversized file
                                | false -> ()

                                fs.Seek(0L, SeekOrigin.End) |> ignore<int64>

                                use writer = new StreamWriter(fs)
                                do! writer.WriteLineAsync logEntries |> Async.AwaitTask
                            finally                        
                                fs.Dispose()
                        with
                        | ex-> return! Error <| string ex.Message 
                    }
        )
    #endif

    let internal postToLog2 (msg : 'a) (err : string) =  //for stress testing purposes only

        IO (fun () 
                ->
                asyncResult 
                    {
                        try
                            #if WINDOWS 
                            let logFilePath = logFileNameWindows2
                            #else
                            let logFilePath = logFileNameAndroid2
                            #endif
                            let! path = SafeFullPath.safeFullPathResult logFilePath 
                                                        
                            let fs =
                                new FileStream
                                    (
                                        path,
                                        FileMode.OpenOrCreate,
                                        FileAccess.Write,
                                        FileShare.None
                                    )
                            try 
                                let maxBytes = int64 maxFileSizeKb * 1024L

                                match fs.Length > maxBytes with
                                | true  -> fs.SetLength 0L  //truncating oversized file
                                | false -> ()

                                fs.Seek(0L, SeekOrigin.End) |> ignore<int64>

                                use writer = new StreamWriter(fs)
                                let s = sprintf "%s %s Error%s" <| string DateTimeOffset.Now <| string msg <| err 
                                do! writer.WriteLineAsync s |> Async.AwaitTask
                            finally                        
                                fs.Dispose()
                        with
                        | ex-> return! Error <| string ex.Message 
                    }
                |> Async.Ignore<Result<unit, string>>
                |> Async.Start  
        )