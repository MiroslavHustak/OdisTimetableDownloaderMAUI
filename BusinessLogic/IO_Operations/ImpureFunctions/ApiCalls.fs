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

open Api.Logging
open DataModelling.Dto
open TransformationLayers.ApiTransformLayer

module ApiCalls = 

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
                        postToLogFile (sprintf "Request failed with status code %d" (int response.statusCode)) |> ignore  
                        return Error <| ApiResponseError (sprintf "Request failed with status code %d" (int response.statusCode))
                with
                | ex 
                    ->
                    postToLogFile (sprintf "%s Error%i" <| string ex.Message <| 44) |> ignore  
                    return Error <| ApiResponseError (string ex.Message)
            }