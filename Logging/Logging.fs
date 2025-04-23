namespace Api

open System
open System.Net

//************************************************************

open FsHttp
open Thoth.Json.Net

//************************************************************

open Types
open Types.ErrorTypes
open Settings.SettingsGeneral

module Logging = 

    [<Struct>]
    type internal ResponsePost = 
        {
            Message1 : string
            Message2 : string
        }

    let private decoderPost : Decoder<ResponsePost> =  //zatim zpetny message neni treba, ale ponechavam pro potencialni pouziti

        Decode.object
            (fun get ->
                     {
                         Message1 = get.Required.Field "Message1" Decode.string
                         Message2 = get.Required.Field "Message2" Decode.string
                     }
            )

    let internal postToLogFile errorMessage = 
        
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