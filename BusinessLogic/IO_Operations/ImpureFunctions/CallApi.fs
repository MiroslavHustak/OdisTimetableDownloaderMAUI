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

open DataModelling.Dto

open TransformationLayers.ApiTransformLayer

module CallApi = 

    type ResponsePost = 
        {
            Message1 : string
            Message2 : string
        }

    let private decoderPost : Decoder<ResponsePost> =

        Decode.object
            (fun get ->
                     {
                         Message1 = get.Required.Field "Message1" Decode.string
                         Message2 = get.Required.Field "Message2" Decode.string
                     }
            )

    let internal postToRestApi errorMessage = 
        
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
                with
                | ex -> return { Message1 = String.Empty; Message2 = sprintf "Request failed with error message %s" (string ex.Message) }     
            } 

//*************************************************************************************************************

    let private decoderGet : Decoder<ResponseGet> =

        Decode.object
            (fun get
                ->
                {
                    GetLinks = get.Required.Field "GetLinks" Decode.string
                    Message = get.Required.Field "Message" Decode.string
                }
            )

    let internal getFromRestApi url = 
    
        async
            {       
                try
                    use! response = 
                        http
                            {
                                GET url
                                header "X-API-KEY" apiKeyTest 
                            }
                        |> Request.sendAsync
                
                    match response.statusCode with
                    | HttpStatusCode.OK 
                        ->
                        let! jsonString = Response.toTextAsync response
                        let response = Decode.fromString decoderGet jsonString

                        return transformApiResponse response
                       
                    | _ -> 
                        postToRestApi (sprintf "Request failed with status code %d" (int response.statusCode)) |> ignore //logfile entry
                        return Error <| ApiResponseError (sprintf "Request failed with status code %d" (int response.statusCode))
                with
                | ex 
                    ->
                    postToRestApi (sprintf "%s Error%i" <| string ex.Message <| 44) |> ignore //logfile entry
                    return Error <| ApiResponseError (string ex.Message)
            }