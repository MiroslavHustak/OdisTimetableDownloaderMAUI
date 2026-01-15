namespace BusinessLogic

open System
open System.IO
open Thoth.Json.Net
open FsToolkit.ErrorHandling

//*******************

open Api.Logging
open Helpers.Serialization
open Settings.SettingsGeneral

open Types.Haskell_IO_Monad_Simulation

open Applicatives.ResultApplicative

module TP_Canopy_Difference = 

    let internal calculate_TP_Canopy_Difference () =

        let showResults uniqueFileNamesTP uniqueFileNamesCanopy = 
            
            seq
                {
                    sprintf "Je v TP, ale chybi v Canopy %A" uniqueFileNamesTP                  
                    sprintf "Je v Canopy, ale chybi v TP %A" uniqueFileNamesCanopy
                    String.replicate 48 "*"
                }

        let fileNames pathToFile =

            IO (fun () 
                    ->  
                    try
                        Directory.EnumerateFiles pathToFile
                        |> Seq.map Path.GetFileName
                        |> Set.ofSeq
                    with
                    | ex 
                        ->
                        //runIO (postToLog <| string ex.Message <| "#Canopy01")
                        Set.empty<string>
            )

        let fileNamesApplicative pathToFile =
        
            IO (fun () 
                    ->  
                    try
                        Directory.EnumerateFiles pathToFile
                        |> Seq.map Path.GetFileName
                        |> Set.ofSeq
                        |> Ok
                    with
                    | ex 
                        ->
                        //runIO (postToLog <| string ex.Message <| "#Canopy01")
                        // Set.empty<string>
                        Error "Applicative's educational test"
            )

        let getDirNames pathToDir =

            IO (fun () 
                    ->  
                    try
                        Directory.EnumerateDirectories pathToDir
                    with
                    | ex 
                        ->
                        //runIO (postToLog <| string ex.Message <| "#Canopy02")
                        Seq.empty<string>
            )
          
        // Viz applicative 
        let getUniqueFileNames folderPathTP folderPathCanopy =
        
            IO (fun () 
                    ->
                    let fileNamesTP = fileNames >> runIO <| folderPathTP                    
                    let fileNamesCanopy = fileNames >> runIO <| folderPathCanopy        
        
                    Set.difference fileNamesTP fileNamesCanopy |> Set.toList, Set.difference fileNamesCanopy fileNamesTP |> Set.toList
            )
        
        //Ostatni applicatives s popisem je v BusinessLogic_R.TP_Canopy_Difference
        let getUniqueFileNamesApplicative folderPathTP folderPathCanopy =
           
           IO (fun ()
                ->
                let tpResult = runIO (fileNamesApplicative folderPathTP)
                let canopyResult = runIO (fileNamesApplicative folderPathCanopy)
               
                (fun tp canopy -> tp, canopy)
                <!> tpResult                  
                <*> canopyResult             
        
                |> Result.map 
                    (fun (tp, canopy) 
                        ->
                        Set.difference tp canopy |> Set.toList,
                        Set.difference canopy tp |> Set.toList
                    )
                |> Result.defaultValue ([],[]) 
           )
        
        let result folderPathTP folderPathCanopy =

            IO (fun () 
                    ->       
                    match folderPathTP = pathTP_FutureValidity && folderPathCanopy = pathCanopy_FutureValidity with
                    | true  -> seq { folderPathTP }, seq { folderPathCanopy }
                    | false -> getDirNames >> runIO <| folderPathTP, getDirNames >> runIO <| folderPathCanopy

                    ||> Seq.map2
                        (fun pathTP pathCanopy
                            ->
                            let uniqueFileNamesTP, uniqueFileNamesCanopy = runIO <| getUniqueFileNamesApplicative pathTP pathCanopy 
                            showResults uniqueFileNamesTP uniqueFileNamesCanopy
                        )
                    |> Seq.collect id   
                    |> Seq.filter (not << isNull) //just in case
            )     

        IO (fun () 
                ->
                try 
                    let json =  
                        seq
                            {
                                currentValidity
                                String.Empty
                                String.replicate 48 "*"
                                yield! runIO <| result pathTP_CurrentValidity pathCanopy_CurrentValidity
                                String.Empty
                                futureValidity
                                String.replicate 48 "*"
                                yield! runIO <| result pathTP_FutureValidity pathCanopy_FutureValidity
                                String.Empty
                                longTermValidity
                                String.replicate 48 "*"
                                yield! runIO <| result pathTP_LongTermValidity pathCanopy_LongTermValidity
                            }                
                        |> List.ofSeq
                        |> List.map Encode.string
                        |> Encode.list
                        |> Encode.toString 2
            
                    #if ANDROID
                    runIO <| serializeWithThothAsync json logFileNameAndroid    
                    #else
                    runIO <| serializeWithThothAsync json logFileNameWindows 
                    #endif
           
                with
                | ex 
                    ->
                    runIO (postToLog <| string ex.Message <| "#Canopy03")
                    async { return Error (string ex.Message) }
        )