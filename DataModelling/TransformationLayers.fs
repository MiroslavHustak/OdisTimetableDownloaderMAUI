namespace TransformationLayers

open Thoth.Json.Net

//**************************

open Types
open Types.ErrorTypes

open Helpers.Builders

open DataModelling.Dto

module ApiTransformLayer =
    
    let internal transformApiResponse response = 

        pyramidOfInferno
            {
                let! response = response, fun err -> Error <| ApiResponseError err  

                let decoder : Decoder<string list> = Decode.field "list" (Decode.list Decode.string)

                let! links = response.GetLinks |> Decode.fromString decoder, fun _ -> Error ApiDecodingError  //TODO logfile  

                return! Ok links
            }