//namespace Helpers

[<RequireQualifiedAccess>]
module List.Parallel

open System

open FSharp.Control
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator.QuotationEvaluationExtensions

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
        
    let numberOfThreads = Environment.ProcessorCount //all List.Parallel.iter/iter2/map/map2 are impure because of that :-(
        
    match (>) numberOfThreads 0 with 
    | true  ->                            
            match (>=) l numberOfThreads with
            | true  -> numberOfThreads

            | false
                when l > 0 
                    -> l

            | _     -> 1
        
    | false -> 1

let iter' action list =  
        
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
                     |> List.init (List.length myList)
                     |> List.map _.Compile()      
                     |> Async.Parallel  
                     |> Async.RunSynchronously 
                     |> ignore<unit array>

let iter action list =

    match list with
    | [] ->
         ()
    | _  ->
         list
         |> List.map (fun item -> async { return action item })  // Create an async task for each item
         |> Async.Parallel  
         |> Async.RunSynchronously  
         |> ignore<unit array>

let iter2<'a, 'b> (mapping : 'a -> 'b -> unit) (xs1 : 'a list) (xs2 : 'b list) = 
    
    let l = xs1 |> List.length   

    match (l = 0 || xs2.IsEmpty) || l <> (xs2 |> List.length) with
    | false -> 
            let listToParallel (xs1, xs2) = (xs1, xs2) ||> List.iter2 mapping    
                           
            let numberOfThreads = numberOfThreads l    
                in
                let myList =       
                    (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)  
                    ||> List.zip   
                    in                                               
                    fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
                    |> List.init (List.length myList)
                    |> List.map _.Compile()       
                    |> Async.Parallel  
                    |> Async.RunSynchronously
                    |> ignore<unit array>

    | true  ->
            ()

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
                     |> List.init (List.length myList)
                     |> List.map _.Compile()       
                     |> Async.Parallel      
                     |> Async.RunSynchronously
                     |> List.ofArray
                     |> List.concat

let map (action : 'a -> 'b) (list : 'a list) =

    match list with
    | [] -> 
         []
    | _  ->
         list
         |> List.map (fun item -> async { return action item })  
         |> Async.Parallel  
         |> Async.RunSynchronously  
         |> List.ofArray
 
let map2<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (xs1 : 'a list) (xs2 : 'b list) =   
    
    let l = xs1 |> List.length 

    match (l = 0 || xs2.IsEmpty) || l <> (xs2 |> List.length) with
    | false -> 
            let listToParallel (xs1, xs2) = (xs1, xs2) ||> List.map2 mapping    
                                
            let numberOfThreads = numberOfThreads l  
                in
                let myList =       
                    (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)  
                    ||> List.zip                 
                    in                               
                    fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
                    |> List.init (List.length myList)
                    |> List.map _.Compile()       
                    |> Async.Parallel  
                    |> Async.RunSynchronously
                    |> List.ofArray
                    |> List.concat

    | true  ->
            []