﻿namespace BusinessLogic

open System
open System.IO
open Thoth.Json.Net

//*******************

open Helpers.Builders
open Helpers.Serialization
open Helpers.DirFileHelper

open Api.Logging
open Settings.SettingsGeneral

open Types.Haskell_IO_Monad_Simulation

module TP_Canopy_Difference =    

    let private showResults uniqueFileNamesTP uniqueFileNamesCanopy = 
            
        seq
            {
                sprintf "Je v TP, ale chybi v Canopy %A" uniqueFileNamesTP                  
                sprintf "Je v Canopy, ale chybi v TP %A" uniqueFileNamesCanopy
                String.replicate 48 "*"
            }

    let private fileNames path =

        IO (fun () 
                ->
                try
                    Directory.EnumerateFiles path
                    |> Seq.map Path.GetFileName
                    |> Set.ofSeq   
                with
                | ex 
                    ->
                    runIO (postToLog <| ex.Message <| "#Canopy01")
                    Set.empty<string>
        )

    let private getDirNames pathToDir =

        IO (fun () 
                ->
                try
                    match runIO <| checkDirectoryCondition pathToDir (fun dirInfo -> dirInfo.Exists) with
                    | Some _ -> Directory.EnumerateDirectories pathToDir                            
                    | None   -> Seq.empty<string>    
                with
                | ex 
                    ->
                    runIO (postToLog <| ex.Message <| "#Canopy02")
                    Seq.empty<string>
        )
          
    let private getUniqueFileNames folderPathTP folderPathCanopy =
        
        IO (fun () 
                ->
                let fileNamesTP = runIO <| fileNames folderPathTP                    
                let fileNamesCanopy = runIO <| fileNames folderPathCanopy        
        
                Set.difference fileNamesTP fileNamesCanopy |> Set.toList, Set.difference fileNamesCanopy fileNamesTP |> Set.toList
        )
                
    let private result folderPathTP folderPathCanopy =

        IO (fun () 
                ->       
                match folderPathTP = pathTP_FutureValidity && folderPathCanopy = pathCanopy_FutureValidity with
                | true  -> (seq { folderPathTP }, seq { folderPathCanopy })
                | false -> (runIO <| getDirNames folderPathTP, runIO <| getDirNames folderPathCanopy)

                ||> Seq.map2
                    (fun pathTP pathCanopy
                        ->
                        let uniqueFileNamesTP, uniqueFileNamesCanopy = runIO <| getUniqueFileNames pathTP pathCanopy 
                        showResults uniqueFileNamesTP uniqueFileNamesCanopy
                    )
                |> Seq.collect id       
        )

    let internal calculate_TP_Canopy_Difference () = 

        IO (fun () 
                ->
                try 
                    let json =  
                        seq
                            {
                                "JR_ODIS_aktualni_vcetne_vyluk"
                                String.Empty
                                String.replicate 48 "*"
                                yield! runIO <| result pathTP_CurrentValidity pathCanopy_CurrentValidity
                                String.Empty
                                "JR_ODIS_pouze_budouci_platnost"
                                String.replicate 48 "*"
                                yield! runIO <| result pathTP_FutureValidity pathCanopy_FutureValidity
                                String.Empty
                                "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk"
                                String.replicate 48 "*"
                                yield! runIO <| result pathTP_WithoutReplacementService pathCanopy_WithoutReplacementService
                            }                
                        |> List.ofSeq
                        |> List.map Encode.string
                        |> Encode.list
                        |> Encode.toString 2
            
                    #if WINDOWS
                    io { return! runIO <| serializeWithThoth json logFileName2 }    
                    #else
                    runIO (serializeWithThoth json logFileName3) 
                    #endif
           
                with
                | ex 
                    ->
                    runIO (postToLog <| ex.Message <| "#Canopy03")
                    Error (string ex.Message)
        )