namespace Api

open System.Net

//************************************************************

open FsHttp
open Thoth.Json.Net

//************************************************************

open Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Settings.SettingsGeneral

open Api.Logging
open DataModelling.Dtm
open TransformationLayers.ApiTransformLayer

module FutureLinks = 

    let private decoderGet : Decoder<ResponseGetLinks> =

        Decode.object
            (fun get
                ->
                {
                    GetLinks = get.Required.Field "GetLinks" Decode.string
                    Message = get.Required.Field "Message" Decode.string
                }
            )

    let internal getFutureLinksFromRestApi url = 

        IO (fun () 
                ->         
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

                                return runIO <| transformLinksApiResponse postToLogFile response
                       
                            | _ -> 
                                runIO <| postToLogFile () (sprintf "Request #347 failed with status code %d" (int response.statusCode))
                                |> Async.Ignore<ResponsePost>
                                |> Async.StartImmediate   
                       
                                return Error <| ApiResponseError (sprintf "Request #447 failed with status code %d" (int response.statusCode))
                        with
                        | ex 
                            ->
                            runIO (postToLog <| ex.Message <| "#044")

                            return Error <| ApiResponseError (string ex.Message)
                    }
        )