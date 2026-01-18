namespace BusinessLogic_R

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
open Applicatives.CummulativeResultApplicative

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
                        runIO (postToLog2 <| string ex.Message <| "#0001-CanopyDifference") 
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
                        runIO (postToLog2 <| string ex.Message <| "#0002-CanopyDifference")
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
                        runIO (postToLog2 <| string ex.Message <| "#0003-CanopyDifference")
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
        
        // Classic fail-fast applicative
        // Both independent computations are evaluated.
        // Error handling follows Result semantics: the first Error encountered is returned.
        // This shows a basic applicative that can be extended to error accumulation.
        let getUniqueFileNamesApplicative folderPathTP folderPathCanopy =
           
            IO (fun ()
                    ->
                    // Two independent IO actions (each may succeed or fail)
                    // Running them immediately here → Result types
                    let tpResult = runIO (fileNamesApplicative folderPathTP)
                    let canopyResult = runIO (fileNamesApplicative folderPathCanopy)
        
                    // Core of applicative style – combining two independent results
                    (fun tp canopy -> tp, canopy) // ← ordinary function (Set → Set → tuple)
                    <!> tpResult                  // ← first application: function <*> first value
                    <*> canopyResult              // ← second application: previous <*> second value
        
                    |> Result.map 
                        (fun (tp, canopy) 
                            ->
                            Set.difference tp canopy |> Set.toList,
                            Set.difference canopy tp |> Set.toList
                        )
                    |> Result.defaultValue ([],[])  // ← only for educational comparison, normally remove the line and let the caller handle the Error case properly
            )

        // Applicative with error accumulation
        // All three independent Result-producing operations are evaluated.
        // All errors are accumulated using a list.
        let getUniqueFileNamesApplicative3 folderPathTP folderPathCanopy1 folderPathCanopy2 =

            IO (fun ()
                    ->    
                    let tpResult =
                        runIO (fileNamesApplicative folderPathTP)
                        |> liftErrorToList
        
                    let canopy1Result =
                        runIO (fileNamesApplicative folderPathCanopy1)
                        |> liftErrorToList
        
                    let canopy2Result =
                        runIO (fileNamesApplicative folderPathCanopy2)
                        |> liftErrorToList

                    (fun tp c1 c2 -> tp, c1, c2)
                    <!!!> tpResult
                    <***> canopy1Result
                    <***> canopy2Result
        
                    |> Result.map (fun (tp, c1, c2) ->
                        // All three are guaranteed to be Ok here
                        let onlyInTP      = Set.difference tp c1 |> Set.difference <| c2 |> Set.toList
                        let onlyInCanopy1 = Set.difference c1 tp |> Set.difference <| c2 |> Set.toList
                        let onlyInCanopy2 = Set.difference c2 tp |> Set.difference <| c1 |> Set.toList
        
                        onlyInTP, onlyInCanopy1, onlyInCanopy2
                    )
                    // |> Result.defaultValue ([], [], [])  // optional for testing
            )

        // Applicative with error accumulation
        // All three independent Result-producing operations are run in parallel.
        // All errors are accumulated.
        let getUniqueFileNamesApplicative3Parallel x y =  //long running Result operations

            async
                {          
                    //simulate long-running operations
                    let op1 x y = 
                        match x with
                        | 0 ->
                            Error "op1 failed"
                        | _ ->
                            Ok (fun x y -> x + y)
                    
                    let op2 x y =
                        match x, y with
                        | _, _ ->
                            Ok (fun x y -> x * y)
                    
                    let op3 x y =
                        match y with
                        | 0 ->
                            Error "division by zero"
                        | _ ->
                            Ok (fun x y -> x / y)        

                    let! results = 
                        [| 
                            async { return op1 x y |> liftErrorToList }
                            async { return op2 x y |> liftErrorToList }
                            async { return op3 x y |> liftErrorToList } 
                        |]
                        |> Async.Parallel

                    let result1 = results |> Array.item 0 
                    let result2 = results |> Array.item 1 
                    let result3 = results |> Array.item 2 
        
                    return
                        (fun r1 r2 r3 -> r1, r2, r3)
                        <!!!> result1
                        <***> result2
                        <***> result3

                        |> Result.map
                            (fun (r1, r2, r3) 
                                ->
                                let onlyInOp1 = r1 x y + r2 x y + r3 x y  //doing something with the results
                                let onlyInOp2 = r1 x y * r2 x y * r3 x y  //doing something with the results
                                let onlyInOp3 = r1 x y / r2 x y / r3 x y  //doing something with the results
                                onlyInOp1, onlyInOp2, onlyInOp3
                            )
                }  

        // Applicative with error accumulation
        // All three independent exception-throwing operations are run in parallel.
        // Exceptions are caught and converted to Result values, and all errors are accumulated. 
        let getUniqueFileNamesApplicative3Parallel_Catch path = //long running .NET processess with exceptions
            
            IO (fun ()
                    ->  
                    async 
                        {
                            //simulate long running exceptions-throwing operations from .NET  
                            let op1 path = Path.GetFullPath path              
                            let op2 path = Path.GetFullPath path                
                            let op3 path = Path.GetFullPath path
                        
                            let asyncToAccumulatingResult (a : Async<'a>) : Async<Result<'a, string list>> =
                                a
                                |> Async.Catch
                                |> Async.map
                                    (Result.ofChoice >> Result.mapError (fun ex -> [ ex.Message ]))
        
                            let! results =
                                [|
                                    async { return op1 path }
                                    async { return op2 path }
                                    async { return op3 path }
                                |]
                                |> Array.map asyncToAccumulatingResult
                                |> Async.Parallel
        
                            let result1 = results |> Array.item 0 
                            let result2 = results |> Array.item 1 
                            let result3 = results |> Array.item 2 
        
                            return
                                (fun r1 r2 r3 -> r1, r2, r3)
                                <!!!> result1
                                <***> result2
                                <***> result3

                                |> Result.map
                                    (fun (r1, r2, r3) 
                                        ->
                                        let onlyInOp1 = sprintf "%s%s%s" r1 r2 r3              //doing something with the results
                                        let onlyInOp2 = String.length r2                       //doing something with the results
                                        let onlyInOp3 = r3 |> String.map System.Char.ToUpper   //doing something with the results
                                        onlyInOp1, onlyInOp2, onlyInOp3
                                    )
                        }
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
                    runIO (postToLog2 <| string ex.Message <| "#0004-CanopyDifference")
                    async { return Error (string ex.Message) }
        )