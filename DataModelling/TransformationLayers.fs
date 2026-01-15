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
    
    let internal transformLinksApiResponse postToLogFile response = 

        IO (fun () 
                ->     
                pyramidOfInferno
                    {
                        let! response = 
                            response, 
                                fun err 
                                    -> 
                                    runIO <| postToLogFile () "#1001"
                                    |> Async.Ignore<'a>
                                    |> Async.StartImmediate
                        
                                    Error ApiResponseError  

                        let decoder : Decoder<string list> = Decode.field "list" (Decode.list Decode.string)

                        let! links = 
                            response.GetLinks |> Decode.fromString decoder, 
                                fun _
                                    -> 
                                    runIO <| postToLogFile () "#100"
                                    |> Async.Ignore<'a>
                                    |> Async.StartImmediate
                            
                                    Error ApiDecodingError  

                        return! Ok (links |> List.distinct)
                    }
           )

    let internal transformLogEntriesApiResponse = 
      
        function
            | Ok value -> value.GetLogEntries
            | Error _  -> String.Empty