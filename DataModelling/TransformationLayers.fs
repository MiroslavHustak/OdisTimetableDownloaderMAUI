namespace TransformationLayers

open System

open Thoth.Json.Net

//**************************

open Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Helpers.Builders
open DataModelling.Dtm

module ApiTransformLayer =
    
    let internal transformLinksApiResponse postToLog2 response = 

        IO (fun () 
                ->     
                pyramidOfInferno
                    {
                        let! response = 
                            response, 
                                fun err 
                                    -> 
                                    runIO <| postToLog2 (string err) "#0001-Api"
                                    Error ApiResponseError  

                        let decoder : Decoder<string list> = Decode.field "list" (Decode.list Decode.string)

                        let! links = 
                            response.GetLinks |> Decode.fromString decoder, 
                                fun item
                                    -> 
                                    runIO <| postToLog2 (string item) "#0002-Api"                                                               
                                    Error ApiDecodingError  

                        return! Ok (links |> List.distinct)
                    }
           )

    let internal transformLogEntriesApiResponse = 
      
        function
            | Ok value -> value.GetLogEntries
            | Error _  -> String.Empty