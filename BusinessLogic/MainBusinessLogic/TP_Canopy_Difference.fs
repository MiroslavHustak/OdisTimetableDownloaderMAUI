namespace BusinessLogic

open System
open System.IO
open Thoth.Json.Net

//************************************************************

open Types.Lazy_IO_Monad

open Helpers.Builders
open Helpers.Serialization

open Settings.SettingsGeneral

module TP_Canopy_Difference =    

    let private printResults () uniqueFileNamesTP uniqueFileNamesCanopy = 
        
        seq
            {
                sprintf "Je v TP, ale chybi v Canopy %A" uniqueFileNamesTP                  
                sprintf "Je v Canopy, ale chybi v TP %A" uniqueFileNamesCanopy
                String.replicate 48 "*"
            }

    let private getUniqueFileNames () folderPathTP folderPathCanopy =

        let fileNames path =

            Directory.EnumerateFiles path
            |> Seq.map Path.GetFileName
            |> Set.ofSeq

        let fileNamesTP = fileNames folderPathTP                    
        let fileNamesCanopy = fileNames folderPathCanopy        
        
        Set.difference fileNamesTP fileNamesCanopy |> Set.toList, Set.difference fileNamesCanopy fileNamesTP |> Set.toList
                
    let private result () folderPathTP folderPathCanopy =

        let getDirNames pathToDir = Directory.EnumerateDirectories pathToDir 
       
        match folderPathTP = pathTP_FutureValidity && folderPathCanopy = pathCanopy_FutureValidity with
        | true  -> (seq {folderPathTP}, seq {folderPathCanopy})
        | false -> (getDirNames folderPathTP, getDirNames folderPathCanopy)

        ||> Seq.map2
            (fun pathTP pathCanopy
                ->
                let uniqueFileNamesTP, uniqueFileNamesCanopy = getUniqueFileNames () pathTP pathCanopy 
                printResults () uniqueFileNamesTP uniqueFileNamesCanopy
            )
        |> Seq.collect id       

    let internal calculate_TP_Canopy_Difference () : Result<unit, string> =
        try
            let json =  
                seq
                    {
                        "CurrentValidity"
                        String.Empty
                        String.replicate 48 "*"
                        yield! result () pathTP_CurrentValidity pathCanopy_CurrentValidity
                        String.Empty
                        "FutureValidity"
                        String.replicate 48 "*"
                        yield! result () pathTP_FutureValidity pathCanopy_FutureValidity
                        String.Empty
                        "WithoutReplacementService"
                        String.replicate 48 "*"
                        yield! result () pathTP_WithoutReplacementService pathCanopy_WithoutReplacementService
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
            Error (string ex.Message)
