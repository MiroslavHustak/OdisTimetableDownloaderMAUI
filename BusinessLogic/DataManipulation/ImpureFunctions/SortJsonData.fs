namespace JsonData

open System.Threading

//************************************************************

open FsToolkit.ErrorHandling

//************************************************************

open EmbeddedTP.EmbeddedTP

//************************************************************

open Types
open Types.ErrorTypes

open Api.Logging

open Settings.SettingsKODIS

open Helpers
open Helpers.Builders
open Helpers.FileInfoHelper

module SortJsonData =  

    //*************************Helpers************************************************************
      
    let private tempJson1, tempJson2 = 

        [
            readAllTextAsync pathkodisMHDTotal 
            readAllTextAsync pathkodisMHDTotal2_0 
        ]         
        |> Async.Parallel 
        |> Async.Catch
        |> Async.RunSynchronously
        |> Result.ofChoice                      
        |> function
            | Ok [|a; b|]    
                -> 
                a, b
            | Ok _ | Error _ 
                -> 
                jsonEmpty, jsonEmpty 

    //cancellation token jsem tady zrusil, aktivace nastava uz drive

    let internal digThroughJsonStructure () = //prohrabeme se strukturou json souboru 
        
        let kodisTimetables () : Reader<string list, string seq> =
        
            reader  //Reader monad for educational purposes only, no real benefit here  
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
                                            let! json = readAllTextAsync pathToJson
                                            return JsonProvider1.Parse json                                                
                                        with 
                                        | _ -> return JsonProvider1.Parse tempJson1
                                    }
                                |> Async.RunSynchronously //zatim cely async block pouze jako priprava pro potencialni pouziti Async.StartImmediate a progress indicator
                                
                                |> Option.ofNull
                                |> Option.map (Seq.map _.Timetable)
                                |> Option.defaultValue  Seq.empty
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
                                    try
                                        async
                                            {
                                                try
                                                    let! json = readAllTextAsync pathToJson  //tady nelze Result.sequence 
                                                    return JsonProvider2.Parse json
                                                with 
                                                | _ -> return JsonProvider2.Parse tempJson2
                                            }
                                        |> Async.RunSynchronously  //zatim cely async block pouze jako priprava pro potencialni pouziti Async.StartImmediate a progress indicator
                                    with
                                    | _ -> JsonProvider2.Parse tempJson2   
                                 
                                let timetables = 
                                    kodisJsonSamples
                                    |> Option.ofNull
                                    |> Option.map (fun value -> value.Data |> Seq.map _.Timetable)
                                    |> Option.defaultValue  Seq.empty
                                 
                                let vyluky = 
                                    kodisJsonSamples
                                    |> Option.ofNull
                                    |> Option.map (fun value -> value.Data |> Seq.collect _.Vyluky)
                                    |> Option.defaultValue Seq.empty
                                 
                                let attachments = 
                                    vyluky
                                    |> Option.ofNull
                                    |> Option.map
                                        (fun value
                                            -> 
                                            value
                                            |> Seq.collect _.Attachments
                                            |> List.ofSeq
                                            |> List.Parallel.map (fun item -> item.Url |> Option.ofNullEmptySpace)
                                            |> List.choose id  // Remove `None` values
                                            |> List.toSeq
                                        )
                                    |> Option.defaultValue Seq.empty
                                     
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
                                    |> Option.map fn1
                                    |> Option.defaultValue Seq.empty
                                       
                                let fn3 (item : JsonProvider1.Root) =  
                                    item.Vyluky
                                    |> Option.ofNull  
                                    |> Option.map (Seq.collect fn2)
                                    |> Option.defaultValue Seq.empty
                                       
                                let kodisJsonSamples = 
                                    try
                                        async
                                            {
                                                try
                                                    let! json = readAllTextAsync pathToJson  //tady nelze Result.sequence 
                                                    return JsonProvider1.Parse json
                                                with 
                                                | _ -> return JsonProvider1.Parse tempJson1
                                            }
                                        |> Async.RunSynchronously //zatim cely async block pouze jako priprava pro potencialni pouziti Async.StartImmediate a progress indicator     
                                    with
                                    | _ -> JsonProvider1.Parse tempJson1
                                    
                                kodisJsonSamples
                                |> Option.ofNull
                                |> Option.map (Seq.collect fn3)
                                |> Option.defaultValue Seq.empty
                            ) 
                }
           
        let addOn () =  
            [
                //pro pripad, kdyby KODIS strcil odkazy do uplne jinak strukturovaneho jsonu, tudiz by neslo pouzit dany type provider, anebo kdyz je vubec do jsonu neda (nize uvedene odkazy)
                //@"https://kodis-files.s3.eu-central-1.amazonaws.com/76_2023_10_09_2023_10_20_v_f2b77c8fad.pdf"
            ]
            |> List.toSeq   
      
        try 
            //zkusebne jsem prestal pouzivat kodisTimetables a kodisAttachments pro stary typ json souboru, zatim to vypada, ze se uz opravdu prestaly pouzivat
            let task = kodisTimetables3 pathToJsonList3 
                in
                addOn()
                |> Seq.append task
                |> Seq.distinct
                |> List.ofSeq
                |> Ok            
        with
        | ex
            ->  
            postToLogFile (sprintf "%s Error%i" <| (string ex.Message) <| 107)
            |> Async.RunSynchronously
            |> ignore<ResponsePost>
           
            Error JsonFilteringError        