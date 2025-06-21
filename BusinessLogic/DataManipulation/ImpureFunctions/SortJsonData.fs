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

// Zkusebne jsem prestal pouzivat kodisTimetables a kodisAttachments (viz full version) pro stary typ json souboru, zatim to vypada, ze se uz opravdu prestaly pouzivat
module SortJsonData =  

    //*************************Helpers************************************************************

    let private tempJson1, tempJson2 = jsonEmpty, readAllTextAsync pathkodisMHDTotal |> Async.RunSynchronously 
         
    let internal digThroughJsonStructure () = //prohrabeme se strukturou json souboru 
                    
        let kodisTimetables3 : Reader<string list, string seq> = 

            reader //Reader monad for educational purposes only, no real benefit here  
                {
                    let! pathToJsonList3 = fun env -> env 

                    let kodisJsonSamples =  //The biggest performance drag is the JsonProvider parsing => parallel done separatelly
                        pathToJsonList3 
                        |> List.Parallel.map_CPU
                            (fun pathToJson 
                                ->
                                try
                                    let json = readAllText pathToJson
                                    JsonProvider2.Parse json
                                with
                                | _ -> JsonProvider2.Parse tempJson2
                            )  

                    return 
                        (pathToJsonList3, kodisJsonSamples) 
                        ||> List.Parallel.map2_CPU 
                            (fun pathToJson kodisJsonSample
                                ->    
                                //JsonProvider's results are of Array type => Array is used
                                let timetables = 
                                    kodisJsonSample
                                    |> Option.ofNull
                                    |> Option.map (fun value -> value.Data |> Array.Parallel.map (_.Timetable))
                                    |> Option.defaultValue Array.empty
                             
                                let vyluky = 
                                    kodisJsonSample
                                    |> Option.ofNull
                                    |> Option.map (fun value -> value.Data |> Array.Parallel.map (_.Vyluky) |> Array.concat)
                                    |> Option.defaultValue Array.empty
                             
                                let attachments = 
                                    vyluky
                                    |> Option.ofNull
                                    |> Option.map
                                        (fun value -> 
                                            value
                                            |> Array.collect (_.Attachments)
                                            |> Array.Parallel.map (fun item -> item.Url |> Option.ofNullEmptySpace) 
                                            |> Array.choose id  // Remove `None` values
                                        )
                                    |> Option.defaultValue Array.empty
                                 
                                Array.append timetables attachments   
                            ) 
                        |> List.toArray 
                        |> Array.concat    
                        |> Seq.ofArray
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
            postToLog <| string ex.Message <| "#107"
            Error JsonFilteringError        