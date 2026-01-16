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
                                runIO <| postToLogFile () (sprintf "0001-FLK4 %d" (int response.statusCode)) 
                                |> Async.Ignore<ResponsePost>
                                |> Async.StartImmediate   
                       
                                return Error ApiResponseError
                        with
                        | ex 
                            ->
                            runIO (postToLog <| string ex.Message <| "#0002-FLK4")
                            return Error ApiResponseError
                    }
        )