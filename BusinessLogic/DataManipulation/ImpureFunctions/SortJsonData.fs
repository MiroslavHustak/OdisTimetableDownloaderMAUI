﻿namespace JsonData

open System.Threading

//************************************************************

open FsToolkit.ErrorHandling

//************************************************************

open EmbeddedTP.EmbeddedTP

//************************************************************

open Types.ErrorTypes

open Settings.SettingsKODIS

open Helpers
open Helpers.Builders
open Helpers.FileInfoHelper

module SortJsonData =  

    //*************************Helpers************************************************************
      
    let private tempJson1, tempJson2 = 

        let jsonEmpty = """[ {} ]"""

        [
            readAllTextAsync pathkodisMHDTotal 
            readAllTextAsync pathkodisMHDTotal2_0 
        ]         
        |> Async.Parallel 
        |> Async.Catch
        |> Async.RunSynchronously
        |> Result.ofChoice                      
        |> function
            | Ok [|a; b|] -> a, b
            | Ok _        -> jsonEmpty, jsonEmpty 
            | Error _     -> jsonEmpty, jsonEmpty

    let internal digThroughJsonStructure (token : CancellationToken) = //prohrabeme se strukturou json souboru 
        
        let kodisTimetables (token : CancellationToken) : Reader<string list, string seq> =
        
            reader
                {
                    let! pathToJsonList = fun env -> env
        
                    return 
                        pathToJsonList
                        |> Seq.ofList
                        |> Seq.collect
                            (fun pathToJson 
                                ->    
                                async
                                    {
                                        try
                                            match token.IsCancellationRequested with
                                            | false ->
                                                    let! json = readAllTextAsync pathToJson
                                                    return JsonProvider1.Parse json
                                            | true  -> 
                                                    return JsonProvider1.Parse tempJson1
                                        with 
                                        | _ -> return JsonProvider1.Parse tempJson1
                                    }
                                |> Async.RunSynchronously
                                |> Option.ofNull
                                |> function
                                    | Some value -> value |> Seq.map (_.Timetable)
                                    | None       -> Seq.empty
                            )
                }
            
        let kodisTimetables3 : Reader<string list, string seq> = 

            reader //Reader monad for educational purposes only, no real benefit here  
                {
                    let! pathToJsonList3 = fun env -> env 

                    return 
                        pathToJsonList3 
                        |> Seq.ofList 
                        |> Seq.collect 
                            (fun pathToJson 
                                ->    
                                let kodisJsonSamples =    
                                    async
                                        {
                                            try
                                                match token.IsCancellationRequested with
                                                | false ->
                                                        let! json = readAllTextAsync pathToJson  //tady nelze Result.sequence 
                                                        return JsonProvider2.Parse json

                                                | true  -> 
                                                        return JsonProvider2.Parse tempJson2
                                            with 
                                            | _ -> return JsonProvider2.Parse tempJson2
                                        }
                                    |> Async.RunSynchronously                                       
                                 
                                let timetables = 
                                    kodisJsonSamples
                                    |> Option.ofNull
                                    |> function 
                                        | Some value -> value.Data |> Seq.map _.Timetable  //nejde Some, nejde Ok
                                        | None       -> Seq.empty  //TODO logfile
                                 
                                let vyluky = 
                                    kodisJsonSamples
                                    |> Option.ofNull
                                    |> function 
                                    | Some value -> value.Data |> Seq.collect _.Vyluky  //nejde Some, nejde Ok
                                    | None       -> Seq.empty  //TODO logfile
                                 
                                let attachments = 
                                    vyluky
                                    |> Option.ofNull 
                                    |> function
                                        | Some value
                                            ->
                                            value
                                            |> Seq.collect (fun item -> item.Attachments)
                                            |> List.ofSeq
                                            |> List.Parallel.map (fun item -> item.Url |> Option.ofNullEmptySpace)                                
                                            |> List.choose id //co neprojde, to beze slova ignoruju
                                            |> List.toSeq

                                        | None   
                                            ->
                                            Seq.empty  //TODO logfile

                                Seq.append timetables attachments   
                            )  
                }       
         
        let kodisAttachments : Reader<string list, string seq> = //Reader monad for educational purposes only, no real benefit here
            
            reader 
                {
                    let! pathToJsonList = fun env -> env 
                        
                    return                          
                        pathToJsonList
                        |> Seq.ofList 
                        |> Seq.collect  //vzhledem ke komplikovanosti nepouzivam Result.sequence pro Array.collect (po zmene na seq ocekavam to same), nejde Some, nejde Ok jako vyse
                            (fun pathToJson 
                                -> 
                                let fn1 (value : JsonProvider1.Attachment seq) = 
                                    value
                                    |> List.ofSeq
                                    |> List.Parallel.map (fun item -> item.Url |> Option.ofNullEmptySpace) //jj, funguje to :-)                                    
                                    |> List.choose id //co neprojde, to beze slova ignoruju
                                    |> List.toSeq

                                let fn2 (item : JsonProvider1.Vyluky) =    
                                    item.Attachments 
                                    |> Option.ofNull        
                                    |> function 
                                        | Some value -> value |> fn1
                                        | None       -> Seq.empty  //TODO logfile              

                                let fn3 (item : JsonProvider1.Root) =  
                                    item.Vyluky
                                    |> Option.ofNull  
                                    |> function 
                                        | Some value -> value |> Seq.collect fn2 
                                        | None       -> Seq.empty  //TODO logfile     

                                let kodisJsonSamples = 
                                    async
                                        {
                                            try
                                                match token.IsCancellationRequested with
                                                | false ->
                                                        let! json = readAllTextAsync pathToJson  //tady nelze Result.sequence 
                                                        return JsonProvider1.Parse json
                                                | true  -> 
                                                        return JsonProvider1.Parse tempJson1
                                            with 
                                            | _ -> return JsonProvider1.Parse tempJson1
                                        }
                                    |> Async.RunSynchronously      
                                                          
                                kodisJsonSamples 
                                |> Option.ofNull
                                |> function 
                                    | Some value -> value |> Seq.collect fn3 
                                    | None       -> Seq.empty   //TODO logfile                                 
                            ) 
                }
           
        let addOn () =  
            [
                //pro pripad, kdyby KODIS strcil odkazy do uplne jinak strukturovaneho jsonu, tudiz by neslo pouzit dany type provider, anebo kdyz je vubec do jsonu neda (nize uvedene odkazy)
                //@"https://kodis-files.s3.eu-central-1.amazonaws.com/76_2023_10_09_2023_10_20_v_f2b77c8fad.pdf"
                @"https://kodis-files.s3.eu-central-1.amazonaws.com/46_A_2024_07_01_2024_09_01_faa5f15c1b.pdf"
                @"https://kodis-files.s3.eu-central-1.amazonaws.com/46_B_2024_07_01_2024_09_01_b5f542c755.pdf"
            ]
            |> List.toSeq   
      
        try 
            let task = kodisTimetables3 pathToJsonList3 
            (Seq.append <| task <| addOn())
            |> Seq.distinct
            |> List.ofSeq
            |> Ok            
        with
        | ex
            ->  
            string ex.Message |> ignore  //TODO logfile
            Error JsonFilteringError        