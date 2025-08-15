namespace BusinessLogic

open System
open System.IO
open Thoth.Json.Net
open FsToolkit.ErrorHandling

//*******************

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

    let private fileNames pathToFile =

        IO (fun () 
                ->
                try
                    //nelze checkFileCondition
                    (*
                        •	If checkFileCondition returns None (e.g., the file does not exist or the condition fails), then fileNames always returns Set.empty<string>.
                        •	This means your results will be empty if the condition is not met, even if the directory exists and contains files.
                    *)
                    runIO <| checkDirectoryCondition pathToFile (fun dirInfo -> dirInfo.Exists)
                    |> Option.map 
                        (fun _
                            ->
                            Directory.EnumerateFiles pathToFile
                            |> Seq.map Path.GetFileName
                            |> Set.ofSeq
                        )
                    |> Option.defaultValue Set.empty<string>
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
                    runIO <| checkDirectoryCondition pathToDir (fun dirInfo -> dirInfo.Exists)
                    |> Option.map (fun _ -> Directory.EnumerateDirectories pathToDir)
                    |> Option.defaultValue Set.empty<string>
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
            
                    #if ANDROID
                    runIO <| serializeWithThoth json logFileNameAndroid    
                    #else
                    runIO <| serializeWithThoth json logFileNameWindows 
                    #endif
           
                with
                | ex 
                    ->
                    runIO (postToLog <| ex.Message <| "#Canopy03")
                    Error (string ex.Message)
        )