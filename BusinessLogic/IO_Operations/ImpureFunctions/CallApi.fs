﻿namespace Api

open System
open System.Net

//************************************************************

open FsHttp
open Thoth.Json.Net

//************************************************************

open Types.ErrorTypes
open Settings.SettingsGeneral

open DataModelling.Dto

open TransformationLayers.ApiTransformLayer

module CallApi = 

    let private decoderGet : Decoder<ResponseGet> =

        Decode.object
            (fun get
                ->
                {
                    GetLinks = get.Required.Field "GetLinks" Decode.string
                    Message = get.Required.Field "Message" Decode.string
                }
            )

    let internal getFromRestApi () = 
    
        async
            {
                let url = "http://kodis.somee.com/api/" // nezapomen na trailing slash 
                
                let! response = 
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
                    return Error <| ApiResponseError (sprintf "Request failed with status code %d" (int response.statusCode))
            }
        |> Async.RunSynchronously