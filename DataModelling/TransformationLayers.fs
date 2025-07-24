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
                                    runIO <| postToLogFile () (sprintf "%s Error%s" <| string ApiResponseError <| "#1001")
                                    |> Async.RunSynchronously
                                    |> ignore<'a>
                        
                                    Error <| ApiResponseError err  

                        let decoder : Decoder<string list> = Decode.field "list" (Decode.list Decode.string)

                        let! links = 
                            response.GetLinks |> Decode.fromString decoder, 
                                fun _
                                    -> 
                                    runIO <| postToLogFile () (sprintf "%s Error%s" <| string ApiDecodingError <| "#100")
                                    |> Async.RunSynchronously
                                    |> ignore<'a>
                            
                                    Error ApiDecodingError  

                        return! Ok (links |> List.distinct)
                    }
           )

    let internal transformLogEntriesApiResponse = 
      
        function
        | Ok value -> value.GetLogEntries
        | Error _  -> String.Empty

      