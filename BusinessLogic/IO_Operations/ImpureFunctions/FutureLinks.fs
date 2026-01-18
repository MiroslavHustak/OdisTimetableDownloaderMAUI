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
                                runIO <| postToLog2 () (sprintf "0001-FLK4 %d" (int response.statusCode)) 
                                return Error ApiResponseError
                        with
                        | ex 
                            ->
                            runIO (postToLog2 <| string ex.Message <| "#0002-FLK4")
                            return Error ApiResponseError
                    }
        )

        // TODO implementovat
        (*
        | FutureValidity          
        ->
        let list = Records.SortRecordData.sortLinksOut dataToBeFiltered FutureValidity |> createPathsForDownloadedFiles 

        let (links, _) = list |> List.unzip
                                
        //let jsonPayload = "[" + (links |> List.map (sprintf "\"%s\"") |> String.concat ",") + "]" //tohle ne

        //prima transformace na json string (bez pouziti records / serializace / Thoth encoders )   
        let s1 = "{ \"list\": ["
        let s2 = links |> List.map (sprintf "\"%s\"") |> String.concat ","
        let s3 = "] }"
        
        let jsonPayload = sprintf "%s%s%s" s1 s2 s3                  

        let result = 
            async
                {
                    let url = "http://kodis.somee.com/api/jsonLinks" 
                    let apiKeyTest = "test747646s5d4fvasfd645654asgasga654a6g13a2fg465a4fg4a3"
                                                                               
                    let! response = 
                        http
                            {
                                PUT url
                                header "X-API-KEY" apiKeyTest 
                                body 
                                json jsonPayload
                            }
                        |> Request.sendAsync       
                                            
                    match response.statusCode with
                    | HttpStatusCode.OK 
                        -> 
                        let! jsonMsg = Response.toTextAsync response
    
                        return                          
                            Decode.fromString decoderPutTest jsonMsg   
                            |> function
                                | Ok value  -> value   
                                | Error err -> { Message1 = String.Empty; Message2 = err }      
                    | _ -> 
                        return { Message1 = String.Empty; Message2 = sprintf "Request failed with status code %d" (int response.statusCode) }                                           
                } 
            |> Async.Catch 
            |> Async.RunSynchronously   //nahradit pri realnem vyuziti async
            |> Result.ofChoice    
            |> function
                | Ok value -> value 
                | Error ex -> { Message1 = String.Empty; Message2 = string ex.Message }    

        match result.Message1.Equals(String.Empty) with true -> () | _ -> printfn "%s" result.Message1  
        match result.Message2.Equals(String.Empty) with true -> () | _ -> printfn "%s" result.Message2 
        
        list
        
        *)