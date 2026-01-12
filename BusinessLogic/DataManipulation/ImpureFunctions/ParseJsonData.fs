namespace JsonData

open System
open System.Threading

//************************************************************
open FSharp.Data
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
open Helpers.Validation
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

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
                                                        (*
                                                        use _ =
                                                            token.Register
                                                                (fun () 
                                                                    ->
                                                                    inbox.Post (Unchecked.defaultof<MsgIncrement>)
                                                                )
                                                        *)
                                                        let rec loop n = 
                                                            async
                                                                {
                                                                    try
                                                                        let! Inc i = inbox.Receive()
                                                                        reportProgress (float n, float l)
                                                                        return! loop (n + i)
                                                                    with
                                                                    | ex -> runIO (postToLog <| ex.Message <| "#911-MP")
                                                                }
                                                        loop 0

                                    let tempJson1, tempJson2 = jsonEmpty, readAllText >> runIO <| pathkodisMHDTotal 

                                    let kodisJsonSamples = //The biggest performance drag is the JsonProvider parsing => parallel computing done separatelly
                                        (token, pathToJsonList3 |> List.filter (not << isNull)) //just in case
                                        ||> List.Parallel.map_CPU_AW_Token_Async 
                                            (fun pathToJson 
                                                ->   
                                                async
                                                    {
                                                        try
                                                            counterAndProgressBar.Post <| Inc 1    
                                                            token.ThrowIfCancellationRequested () //pouzit v parallel loops jen u async verze

                                                            let! result = readAllTextAsync >> runIO <| pathToJson 

                                                            return result |> JsonProvider2.Parse
                                                             // The biggest performance drag    
                                                        with
                                                        | _ -> return JsonProvider2.Parse tempJson2
                                                    }
                                            )
                                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
                                        |> List.filter (not << isNull)  //just in case
                                            
                                    return 
                                        (token, pathToJsonList3, kodisJsonSamples) 
                                        |||> List.Parallel.map2_CPU2_AW_Token_Async 
                                            (fun pathToJson kodisJsonSample
                                                ->   
                                                async
                                                    {
                                                        try
                                                            counterAndProgressBar.Post <| Inc 1

                                                            token.ThrowIfCancellationRequested () //pouzit v parallel loops jen u async verze
                                                
                                                            //JsonProvider's results are of Array type => Array is used
                                                
                                                            let timetables =
                                                                option 
                                                                    {
                                                                        let! (sample : JsonProvider2.Root) = kodisJsonSample |> Option.ofNull 
                                                                        let! data = sample.Data |> Option.ofNull                                                             
                                                                        return 
                                                                            data
                                                                            |> Array.filter (not << isNull)
                                                                            |> Array.Parallel.map 
                                                                                (fun (item : JsonProvider2.Datum) -> item.Timetable)
                                                                    }
                                                                |> Option.defaultValue Array.empty   
                                                
                                                            let vyluky =
                                                                option 
                                                                    {
                                                                        let! (sample : JsonProvider2.Root) = kodisJsonSample |> Option.ofNull 
                                                                        let! data = sample.Data |> Option.ofNull                                                             
                                                                        return 
                                                                            data
                                                                            |> Array.filter (not << isNull)
                                                                            |> Array.Parallel.map 
                                                                                (fun (item : JsonProvider2.Datum) -> item.Vyluky)
                                                                            |> Array.concat
                                                                    }
                                                                |> Option.defaultValue Array.empty
                                                
                                                            let attachments =
                                                                option 
                                                                    {
                                                                        let! arr = vyluky |> Option.ofNull                                                             
                                                                        return
                                                                            arr
                                                                            |> Array.filter (not << isNull)
                                                                            |> Array.collect (fun (item : JsonProvider2.Vyluky) -> item.Attachments)
                                                                            |> Array.choose (fun item -> item.Url |> Option.ofNullEmptySpace)
                                                                    }
                                                                |> Option.defaultValue Array.empty
                                             
                                                            return Array.append timetables attachments  
                                                        with
                                                        |_ -> return [||] //silently swallowing an error
                                                    }
                                            ) 
                                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)  
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
                                |> Seq.choose 
                                    (fun item
                                        -> 
                                        isValidHttps item
                                        |> Option.fromBool item
                                        |> Option.bind Option.ofNullEmptySpace //ofNullEmptySpace je vyse, ale pro jistotu jeste jednou quli addOn
                                    ) 
                                |> Seq.distinct                                
                                |> List.ofSeq
                                |> Ok  
                        with
                        | ex
                            ->  
                            match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                            | err 
                                when err = StopDownloading
                                ->
                                //runIO (postToLog <| string ex.Message <| "#123456X")
                                Error <| JsonParsingError2 StopJsonParsing  
                            | _ ->
                                runIO (postToLog <| string ex.Message <| "#107")
                                Error <| JsonParsingError2 JsonParsingError    
                 // )
        )