//namespace Helpers

[<RequireQualifiedAccess>]
module List.Parallel

open System
open System.Threading

#if ANDROID
open Android.OS
open Android.App
open Android.Net
open Android.Views
open Android.Content
open Android.Runtime

#endif

open FSharp.Control
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator.QuotationEvaluationExtensions

//*************************************************************

open Settings.SettingsGeneral

// *****************************Helpers***********************************

let private expr (param : 'a) = Expr.Value param  
  
let private splitListIntoEqualParts (numParts : int) (originalList : 'a list) =   //well, almost equal parts :-)           

    let rec splitAccumulator remainingList partsAccumulator acc =
    
        match remainingList with
        | [] -> 
             partsAccumulator |> List.rev 
        | _  ->                     
             let currentPartLength =
    
                 let partLength list n = 

                     let totalLength = list |> List.length  
                     let partLength = totalLength / n    
                              
                     totalLength % n > 0
                     |> function
                         | true  -> (+) partLength 1
                         | false -> partLength 
    
                 match acc.Equals numParts with
                 | true  -> partLength originalList numParts    
                 | false -> partLength remainingList acc                                 
        
             let (part, rest) = remainingList |> List.splitAt currentPartLength 

             splitAccumulator rest (part :: partsAccumulator) (acc - 1)
                      
    splitAccumulator originalList [] numParts
        
let private numberOfThreads l =  
        
    let numberOfThreads = Environment.ProcessorCount //pragmatically pure
        
    match (>) numberOfThreads 0 with 
    | true  ->                            
            match (>=) l numberOfThreads with
            | true  -> numberOfThreads

            | false
                when l > 0 
                    -> l

            | _     -> 1
        
    | false -> 
            1

let private maxDegreeOfParallelismWM = 

    let (|Small|Medium|Large|) length = //active pattern
        match length with
        | length 
            when length < myIdeaOfASmallList 
            -> Small

        | length
            when length >= myIdeaOfASmallList && length <= myIdeaOfALargelList 
            -> Medium

        | _ 
            -> Large
    
    function
        | Small  -> maxDegreeOfParallelismThrottled
        | Medium -> maxDegreeOfParallelismMedium
        | Large  -> maxDegreeOfParallelism

let private maxDegreeOfParallelismAdaptedAndroid = 
      
    #if ANDROID
    let (|Small|Medium|Large|) length = //active pattern

        match length with
        | length 
            when length < myIdeaOfASmallList || Build.VERSION.SdkInt < BuildVersionCodes.R 
            -> Small

        | length
            when (length >= myIdeaOfASmallList && length <= myIdeaOfALargelList) && Build.VERSION.SdkInt >= BuildVersionCodes.R 
            -> Medium

        | length
            when length >= myIdeaOfALargelList && Build.VERSION.SdkInt >= BuildVersionCodes.R 
            -> Large

        | _ 
            -> Medium
    
    function
        | Small  -> maxDegreeOfParallelismThrottled
        | Medium -> maxDegreeOfParallelismMedium
        | Large  -> maxDegreeOfParallelism

    #else
    maxDegreeOfParallelismWM
    #endif

//**************************Functions*******************************************

// ALL FUNCTIONS ARE NOT WRAPPED IN TRY..WITH AND THE RECORD TYPE TO AVOID HASSLE

// Although functions using numberOfThreads, async and tasks are technically impure, they are pure in the sense that they do not change any state outside their scope

// Using Array.Parallel.iter  //TODO otestovat rychlost ve srovnani s Async.Parallel
let iter_CPU (action : 'a -> unit) (list : 'a list) : unit =

    match list with
    | []
        -> 
        ()
    | _ ->
        let l = List.length list
            in
            let numberOfThreads = numberOfThreads l
                in
                splitListIntoEqualParts numberOfThreads list        
                |> List.toArray
                |> (List.iter >> Array.Parallel.iter <| action)   

let iter_IO (action : 'a -> unit) (list : 'a list) =
 
    match list with
    | [] 
        -> 
        ()
    | _ 
        ->
        let maxDegreeOfParallelismAdapted = List.length >> maxDegreeOfParallelismAdaptedAndroid <| list
            in 
            list
            |> List.map (fun item -> async { return action item })
            |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
            |> Async.Ignore<unit array> 
            |> Async.RunSynchronously //Async.Parallel doesn't block any threads while waiting for IO operations to complete.

// Using Array.Parallel.map //Array.Parallel.map is designed for CPU-bound work.  //TODO otestovat rychlost ve srovnani s Async.Parallel
let map_CPU (action : 'a -> 'b) (list : 'a list) : 'b list =

    match list with
    | []
        -> 
        []
    | _ ->
        let l = List.length list
            in
            let numberOfThreads = numberOfThreads l
                in
                splitListIntoEqualParts numberOfThreads list        
                |> List.toArray
                |> (List.map >> Array.Parallel.map <| action)  //Pools of threads by mel dat optimalni number of threads, zpravidla umerny CPU cores (ale i mene - 5,6 versus mych natvrdo 8 u code quotations) 
                |> List.ofArray
                |> List.concat

let map_IO (action : 'a -> 'b) (list : 'a list) =

    match list with
    | [] -> 
         []
    | _  ->
         let maxDegreeOfParallelismAdapted = List.length >> maxDegreeOfParallelismAdaptedAndroid <| list  
             in 
             list
             |> List.toArray
             |> Array.map (fun item -> async { return action item })  //just testing with array here
             |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
             |> Async.RunSynchronously  
             |> List.ofArray

// Using Array.Parallel.iter //TODO otestovat rychlost ve srovnani s Async.Parallel
let iter2_CPU<'a, 'b> (mapping: 'a -> 'b -> unit) (xs1: 'a list) (xs2: 'b list) : unit =

    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match xs1, xs2 with
    | [], _ | _, [] 
        -> 
        ()
    | _ when xs1Length <> xs2Length
        -> 
        ()
    | _ ->
        let numberOfThreads = numberOfThreads xs1Length
            in
            (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)
            ||> List.zip 
            |> List.toArray        
            |> Array.Parallel.iter (fun (chunk1, chunk2) -> (chunk1, chunk2) ||> List.iter2 mapping) 

let iter2_IO<'a, 'b, 'c> (mapping : 'a -> 'b -> unit) (xs1 : 'a list) (xs2 : 'b list) : unit =      

    let xs1Length = List.length xs1
    let xs2Length = List.length xs2
    
    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | false
        ->
        let listToParallel (xs1, xs2) = List.map2 mapping xs1 xs2

        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length
            in
            (splitListIntoEqualParts maxDegreeOfParallelismAdapted xs1, splitListIntoEqualParts maxDegreeOfParallelismAdapted xs2)
            ||> List.zip
            |> List.map (fun pair -> async { return listToParallel pair })
            |> Async.Parallel
            |> Async.Ignore<unit list array> 
            |> Async.RunSynchronously

    | true 
        ->
        ()

// Using Array.Parallel.map  //TODO otestovat rychlost ve srovnani s Async.Parallel
let map2_CPU<'a, 'b, 'c> (mapping: 'a -> 'b -> 'c) (xs1: 'a list) (xs2: 'b list) : 'c list =

    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match xs1, xs2 with
    | [], _ | _, [] 
        ->
        []
    | _ when xs1Length <> xs2Length
        ->
        []
    | _ ->
        let numberOfThreads = numberOfThreads xs1Length
            in
            (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)
            ||> List.zip 
            |> List.toArray 
            |> Array.Parallel.map (fun (chunk1, chunk2) -> (chunk1, chunk2) ||> List.map2 mapping) 
            |> Array.toList 
            |> List.concat

let map2_IO<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (xs1 : 'a list) (xs2 : 'b list) : 'c list =
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2
    
    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | false
        ->
        let listToParallel (xs1, xs2) = List.map2 mapping xs1 xs2

        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length
            in
            (splitListIntoEqualParts maxDegreeOfParallelismAdapted xs1, splitListIntoEqualParts maxDegreeOfParallelismAdapted xs2)
            ||> List.zip
            |> List.map (fun pair -> async { return listToParallel pair })
            |> Async.Parallel
            |> Async.RunSynchronously
            |> List.ofArray
            |> List.concat

    | true 
        ->
        [] 

let map2_IO_Token<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (token : CancellationToken) (xs1 : 'a list) (xs2 : 'b list) : 'c list =
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | false
        ->
        let listToParallel (xs1, xs2) = List.map2 mapping xs1 xs2

        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length
            in
            (splitListIntoEqualParts maxDegreeOfParallelismAdapted xs1, splitListIntoEqualParts maxDegreeOfParallelismAdapted xs2)
            ||> List.zip
            |> List.toArray
            |> Array.map (fun pair -> async { return listToParallel pair }) //just testing with array here
            |> Async.Parallel
            |> fun workflow -> Async.RunSynchronously(workflow, cancellationToken = token)  
            |> List.ofArray
            |> List.concat

    | true 
        ->
        [] 

//*********************************************************************
// Code quotations, for educational purposes only
//*********************************************************************
let iter' (action : 'a -> unit) (list : 'a list) =

    match list with
    | [] ->
         ()
    | _  ->
         let listToParallel list = list |> List.iter action        
         
         let l = list |> List.length
             in
             let numberOfThreads = numberOfThreads l   
                 in                           
                 let myList = splitListIntoEqualParts numberOfThreads list                             
                     in 
                     fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
                     |> (List.length >> List.init <| myList)
                     |> List.map _.Compile()      
                     |> Async.Parallel  
                     |> Async.Ignore<unit array> 
                     |> Async.RunSynchronously 

//code quotations
let iter2'<'a, 'b> (mapping : 'a -> 'b -> unit) (xs1 : 'a list) (xs2 : 'b list) = 
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2   

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | false -> 
            let listToParallel (xs1, xs2) = (xs1, xs2) ||> List.iter2 mapping    
                           
            let numberOfThreads = numberOfThreads xs1Length    
                in
                let myList =       
                    (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)  
                    ||> List.zip   
                    in                                               
                    fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
                    |> (List.length >> List.init <| myList)
                    |> List.map _.Compile()       
                    |> Async.Parallel  
                    |> Async.Ignore<unit array> 
                    |> Async.RunSynchronously

    | true  ->
            ()

//code quotations
let map' (action : 'a -> 'b) (list : 'a list) =

    match list with
    | [] -> 
         []
    | _  ->
         let listToParallel (list : 'a list) = list |> List.map action 
            
         let l = list |> List.length
             in
             let numberOfThreads = numberOfThreads l   
                 in                          
                 let myList : 'a list list = splitListIntoEqualParts numberOfThreads list 
                     in
                     fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
                     |> (List.length >> List.init <| myList)
                     |> List.map _.Compile()       
                     |> Async.Parallel      
                     |> Async.RunSynchronously
                     |> List.ofArray
                     |> List.concat

//code quotations
let map2'<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (xs1 : 'a list) (xs2 : 'b list) =   
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2   

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | false -> 
            let listToParallel (xs1, xs2) = (xs1, xs2) ||> List.map2 mapping    
                                
            let numberOfThreads = numberOfThreads xs1Length  
                in
                let myList =       
                    (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)  
                    ||> List.zip                 
                    in                               
                    fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
                    |> (List.length >> List.init <| myList)
                    |> List.map _.Compile()       
                    |> Async.Parallel  
                    |> Async.RunSynchronously
                    |> List.ofArray
                    |> List.concat

    | true  ->
            []