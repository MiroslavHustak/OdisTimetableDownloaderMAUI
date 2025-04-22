namespace TransformationLayers

open Thoth.Json.Net

//**************************

open Types
open Types.ErrorTypes

open Api.Logging
open Helpers.Builders
open DataModelling.Dto

module ApiTransformLayer =
    
    let internal transformApiResponse response = 

        pyramidOfInferno
            {
                let! response = response, fun err -> Error <| ApiResponseError err  

                let decoder : Decoder<string list> = Decode.field "list" (Decode.list Decode.string)

                let! links = 
                    response.GetLinks |> Decode.fromString decoder, 
                        fun _
                            -> 
                            postToLogFile (sprintf "%s Error%i" <| string ApiDecodingError <| 100)
                            |> Async.RunSynchronously
                            |> ignore
                            
                            Error ApiDecodingError  

                return! Ok (links |> List.distinct)
            }