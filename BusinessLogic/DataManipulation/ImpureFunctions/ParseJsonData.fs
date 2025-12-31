namespace JsonData

open System
open System.Threading

//************************************************************

open FsToolkit.ErrorHandling

//************************************************************

open EmbeddedTP.EmbeddedTP

//************************************************************

open Types
open Types.Types
open Types.ErrorTypes

open Api.Logging

open Settings.SettingsKODIS
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.Builders
open Helpers.DirFileHelper

// Zkusebne jsem prestal pouzivat kodisTimetables a kodisAttachments (viz full version) pro stary typ json souboru, zatim to vypada, ze se uz opravdu prestaly pouzivat
module ParseJsonData =      

    let internal parseJsonStructure reportProgress (token : CancellationToken) = //prohrabeme se strukturou json souboru 
        
        IO (fun () 
                ->  
                //FSharp.Control.Lazy.Create  // Use FSharp.Control.Lazy.Create to explicitly reference the F# Lazy type and avoid conflicts with System.Lazy<'T> from .NET.
                   // (fun () 
                       // ->
                        let kodisTimetables3 : Reader<string list, string seq> = 

                            reader //Reader monad for educational purposes only, no real benefit here  
                                {
                                    let! pathToJsonList3 = fun env -> env 

                                    let l = pathToJsonList3 |> List.length
                                        in
                                        let counterAndProgressBar =
                                            MailboxProcessor<MsgIncrement>
                                                .StartImmediate
                                                    <|
                                                    fun inbox 
                                                        ->
                                                        let rec loop n = 
                                                            async { match! inbox.Receive() with Inc i -> reportProgress (float n, float l); return! loop (n + i) }
                                                        loop 0

                                    //let tempJson1, tempJson2 = jsonEmpty, readAllText >> runIO <| pathkodisMHDTotal 

                                    let kodisJsonSamples = //The biggest performance drag is the JsonProvider parsing => parallel computing done separatelly
                                        pathToJsonList3
                                        |> List.Parallel.map_CPU 
                                            (fun pathToJson 
                                                ->                                               
                                                token.ThrowIfCancellationRequested()  // Artificial checkpoint 
                                                
                                                counterAndProgressBar.Post <| Inc 1
                                                
                                                readAllTextAsync >> runIO <| pathToJson   
                                                |> Async.RunSynchronously
                                                |> JsonProvider2.Parse // The biggest performance drag                                            
                                            )

                                    return 
                                        (pathToJsonList3, kodisJsonSamples) 
                                        ||> List.Parallel.map2_CPU 
                                            (fun pathToJson kodisJsonSample
                                                ->  
                                                token.ThrowIfCancellationRequested()  // Artificial checkpoint 
                                                
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
                                                        (fun value 
                                                            -> 
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
                            match Helpers.ExceptionHelpers.isCancellation ex with
                            | true  ->
                                    Error <| JsonError StopJsonParsing   
                            | false ->
                                    runIO (postToLog <| string ex.Message <| "#107")
                                    Error <| JsonError JsonParsingError
                  //  )
        )