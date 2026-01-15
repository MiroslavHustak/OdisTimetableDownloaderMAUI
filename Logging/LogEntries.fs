namespace Api

open System.Net

//************************************************************

open FsHttp
open Thoth.Json.Net

//************************************************************

open Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open DataModelling.Dtm
open Settings.SettingsGeneral
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

    let internal getLogEntriesFromRestApi url = 

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

                                return Ok <| transformLogEntriesApiResponse response
                       
                            | _ -> 
                                return Error ApiResponseError
                        with
                        | _ -> return Error ApiResponseError
                    }
           )