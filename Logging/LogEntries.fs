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

open DataModelling.Dtm
open TransformationLayers.ApiTransformLayer

module LogEntries = 

    let private decoderGet : Decoder<ResponseGetLogEntries> =

        Decode.object
            (fun get
                ->
                {
                    GetLogEntries = get.Required.Field "GetLogEntries" Decode.string
                    Message = get.Required.Field "Message" Decode.string
                }
            )

    let internal getLogEntriesFromRestApi () url = 
    
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

                        return Ok <| transformLogEntriesApiResponse response
                       
                    | _ -> 
                        return Error <| ApiResponseError (sprintf "Request failed with status code %d" (int response.statusCode))
                with
                | ex 
                    ->
                    return Error <| ApiResponseError (string ex.Message)
            }