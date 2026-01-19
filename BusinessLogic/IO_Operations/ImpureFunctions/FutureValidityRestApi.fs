namespace Api

open System.Net
open System.Net.Http
open System.Threading

//************************************************************

open FsHttp
open Thoth.Json.Net
open FsToolkit.ErrorHandling

//************************************************************

open Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open DataModelling.Dtm
open Settings.SettingsGeneral
open TransformationLayers.ApiTransformLayer

module FutureValidityRestApi = 

    let private decoderGet : Decoder<ResponseGetLinks> =

        Decode.object
            (fun get
                ->
                {
                    GetLinks = get.Required.Field "GetLinks" Decode.string
                    Message = get.Required.Field "Message" Decode.string
                }
            )
    
    let private decoderPut : Decoder<ResponsePut> =

        Decode.object
            (fun get
                ->
                {
                    Message1 = get.Required.Field "Message1" Decode.string
                    Message2 = get.Required.Field "Message2" Decode.string
                }
            )

    //******************************************************************

    let internal getFutureLinksFromRestApi (token : CancellationToken) url = 

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
                                        config_timeoutInSeconds 31
                                        config_cancellationToken token
                                        #if ANDROID
                                        config_transformHttpClient
                                            (fun _
                                                ->
                                                let unsafeHandler = new HttpClientHandler() //nelze use //docasne reseni
                                                unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)   
                                                let unsafeClient = new HttpClient(unsafeHandler) 
                                                unsafeClient
                                            )
                                        #endif
                                    }
                                |> Request.sendAsync
                
                            match response.statusCode with
                            | HttpStatusCode.OK 
                                ->
                                let! jsonString = Response.toTextAsync response
                                let response = Decode.fromString decoderGet jsonString

                                return runIO <| transformLinksApiResponse postToLog2 response
                       
                            | _ ->  
                                runIO <| postToLog2 () (sprintf "0001-FLK4 %d" (int response.statusCode)) 
                                return Error ApiResponseError
                        with
                        | ex 
                            ->
                            runIO (postToLog2 <| string ex.Message <| "#0002-FLK4")
                            runIO (postToLog2 <| (string <| ex.GetType().FullName) <| "#0021-FLK4")
                            runIO (postToLog2 <| string ex.StackTrace <| "#0022-FLK4")
                            return Error ApiResponseError
                    }
        )

    //******************* For Kodis4 only **************************

    let internal putFutureLinksToRestApi (token : CancellationToken) links = 

        IO (fun () 
                ->          
                // Direct transformation into JSON string (no records/serialization/Thoth encoders are necessary)   
                let s1 = "{ \"list\": ["
                let s2 = links |> List.map (sprintf "\"%s\"") |> String.concat ","
                let s3 = "] }"
        
                let jsonPayload = sprintf "%s%s%s" s1 s2 s3  
           
                async
                    {
                        try                                                                                                   
                            let! response = 
                                http
                                    {
                                        PUT urlJson
                                        header "X-API-KEY" apiKeyTest 
                                        config_timeoutInSeconds 31
                                        config_cancellationToken token
                                        #if ANDROID
                                        config_transformHttpClient
                                            (fun _
                                                ->
                                                let unsafeHandler = new HttpClientHandler() //nelze use //docasne reseni
                                                unsafeHandler.ServerCertificateCustomValidationCallback <- (fun _ _ _ _ -> true)   
                                                let unsafeClient = new HttpClient(unsafeHandler) 
                                                unsafeClient
                                            )
                                        #endif
                                        body 
                                        json jsonPayload
                                    }
                                |> Request.sendAsync       
                                            
                            match response.statusCode with
                            | HttpStatusCode.OK 
                                -> 
                                let! jsonMsg = Response.toTextAsync response
                        
                                // I don't need to deal with potential errors, info about them is sufficient.
                                return 
                                    Decode.fromString decoderPut jsonMsg
                                    |> Result.mapError
                                        (fun err 
                                            ->
                                            let errMsg = 
                                                { 
                                                    Message1 = string response.statusCode
                                                    Message2 = err 
                                                }
                                                           
                                            runIO <| postToLog2 () (sprintf "0003-FLK4 %A" errMsg)                                
                                                             
                                        ) |> ignore<Result<ResponsePut, unit>> //silently swallowing errors
                            | _ -> 
                                runIO <| postToLog2 () (sprintf "0004-FLK4 %d" (int response.statusCode)) 
                                return () //silently swallowing errors

                        with
                        | ex 
                            ->
                            runIO (postToLog2 <| string ex.Message <| "#0005-FLK4")
                            runIO (postToLog2 <| (string <| ex.GetType().FullName) <| "#0051-FLK4")
                            runIO (postToLog2 <| string ex.StackTrace <| "#0052-FLK4")
                            return () //silently swallowing errors
                    } 
        )