//namespace Helpers

[<RequireQualifiedAccess>]
module List.Parallel

open System
open System.Threading

#if ANDROID
open Android.OS
#endif

open FSharp.Control
open Microsoft.FSharp.Quotations
open FSharp.Quotations.Evaluator.QuotationEvaluationExtensions

//*************************************************************

open Types.ErrorTypes
open Helpers.ExceptionHelpers
open Settings.SettingsGeneral

//************************************************************************

// !!!! APPLY TRY-WITH BLOCKS WHEN USING FUNCTIONS FROM List.Parallel !!!!!

//************************************************************************

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

        let isAtLeastAndroid11 = int Build.VERSION.SdkInt >= 30

        match length with
        | length 
            when length < myIdeaOfASmallList || not isAtLeastAndroid11 //Build.VERSION.SdkInt < BuildVersionCodes.R 
            -> Small

        | length
            when (length >= myIdeaOfASmallList && length <= myIdeaOfALargelList) && isAtLeastAndroid11 //Build.VERSION.SdkInt >= BuildVersionCodes.R 
            -> Medium

        | length
            when length >= myIdeaOfALargelList && isAtLeastAndroid11 //Build.VERSION.SdkInt >= BuildVersionCodes.R 
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

// Although functions using numberOfThreads, async and tasks are technically impure, they are pure in the sense that they do not change any state outside their scope

// Arrays cannot raise exceptions as the lists the arrays are converted from cannot be nullable

// Using Array.Parallel.iter  //TODO otestovat rychlost ve srovnani s Async.Parallel
let iter_CPU_PT (action : 'a -> unit) (list : 'a list) : unit =

    match list with
    | []
        -> 
        ()
    | _ ->
        list
        |> List.toArray
        |> Array.Parallel.iter action  
        ()

/// Version using Async.Parallel for CPU-bound iteration (for performance testing)
let iter_CPU_AW (action : 'a -> unit) (list : 'a list) : unit =

    match list with
    | []
        -> 
        ()
    | _ ->
        let maxDegree = Environment.ProcessorCount   // or numberOfThreads (List.length list)

        list
        |> Array.ofList
        |> Array.map (fun x -> async { return action x })  
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
        |> Async.Ignore<unit array> 
        |> Async.RunSynchronously
        ()

let iter_IO_AW (action : 'a -> unit) (list : 'a list) =
 
    match list with
    | [] 
        -> 
        ()
    | _ 
        ->
        let maxDegreeOfParallelismAdapted = List.length >> maxDegreeOfParallelismAdaptedAndroid <| list
            
        list
        |> Array.ofList
        |> Array.map (fun item -> async { return action item })  
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
        |> Async.Ignore<unit array> 
        |> Async.RunSynchronously 
        ()

// Using Array.Parallel.map //Array.Parallel.map is designed for CPU-bound work.  //TODO otestovat rychlost ve srovnani s Async.Parallel
let map_CPU_PT (action : 'a -> 'b) (list : 'a list) : 'b list =

    match list with
    | []
        -> 
        []
    | _ ->
        list
        |> List.toArray
        |> Array.Parallel.map action  
        |> Array.toList

let map_CPU_AW_Token (action : 'a -> 'b) (token : CancellationToken) (list : 'a list) : 'b list =

    match list with
    | []
        -> 
        []
    | _ ->
        let maxDegree = Environment.ProcessorCount   // or reuse numberOfThreads (List.length list)

        list
        |> Array.ofList
        |> Array.map
            (fun x 
                ->
                async 
                    {
                        token.ThrowIfCancellationRequested ()
                        return action x
                    }
            )  
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        |> Array.toList

let map_CPU_AW_Token_Async (action : 'a -> Async<'b>) (token : CancellationToken) (list : 'a list) : Async<'b list> =

    match list with
    | [] ->  
        async { return [] }
    | _ ->
        let maxDegree = Environment.ProcessorCount   // or reuse numberOfThreads (List.length list)       

        let tasks = 
            list
            |> Array.ofList   
            |> Array.map
                (fun item 
                    ->
                    async 
                        {
                            token.ThrowIfCancellationRequested ()
                            return! action item
                        }
                )   

        async 
            {
                let! result =  Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
                return result |> List.ofArray 
            } 

let map_IO_AW (action : 'a -> 'b) (list : 'a list) =

    match list with
    | [] -> 
         []
    | _  ->
         let maxDegreeOfParallelismAdapted = List.length >> maxDegreeOfParallelismAdaptedAndroid <| list  
         
         list
         |> Array.ofList
         |> Array.map (fun item -> async { return action item })  
         |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
         |> Async.RunSynchronously  
         |> List.ofArray

let map_IO_AW_Async (action : 'a -> Async<'b>) (list : 'a list) =
         
    match list with
    | [] -> 
        async { return [] }
    | _  ->
        let maxDegreeOfParallelismAdapted = List.length >> maxDegreeOfParallelismAdaptedAndroid <| list  
                  
        let tasks = 
            list
            |> Array.ofList       
            |> Array.map (fun item -> async { return! action item })  
            
        async 
            {
                let! result =  Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
                return result |> List.ofArray 
            }  
            
let map_IO_AW_Token_Async (action : 'a -> Async<'b>) (token : CancellationToken) (list : 'a list) =
     
    match list with
    | [] -> 
        async { return [] }
    | _  ->
        let maxDegreeOfParallelismAdapted = List.length >> maxDegreeOfParallelismAdaptedAndroid <| list  
              
        let tasks = 
            list
            |> Array.ofList   
            |> Array.map
                (fun item 
                    ->
                    async 
                        {
                            token.ThrowIfCancellationRequested ()
                            return! action item
                        }
                )        
        
        async 
            {
                let! result =  Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
                return result |> List.ofArray 
            }   

// Using Array.Parallel.iter //TODO otestovat rychlost ve srovnani s Async.Parallel
let iter2_CPU_PT<'a, 'b> (mapping : 'a -> 'b -> unit) (xs1 : 'a list) (xs2 : 'b list) : unit =

    match xs1, xs2 with
    | [], _ | _, [] 
        -> 
        ()
    | _ when List.length xs1 <> List.length xs2
        -> 
        ()
    | _ ->
        List.zip xs1 xs2
        |> List.toArray
        |> Array.Parallel.iter (fun (x, y) -> mapping x y)

/// Version using Async.Parallel for CPU-bound work (for testing and comparison with Array.Parallel)
let iter2_CPU_AW<'a, 'b> (mapping : 'a -> 'b -> unit) (xs1 : 'a list) (xs2 : 'b list) : unit =

    match xs1, xs2 with
    | [], _ | _, [] 
        -> 
        ()
    | _ when List.length xs1 <> List.length xs2
        -> 
        ()
    | _ ->
        let maxDegree = Environment.ProcessorCount   // typical choice for CPU-bound work

        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map (fun (x, y) -> async { return mapping x y })
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
        |> Async.Ignore<unit array> 
        |> Async.RunSynchronously
        ()

let iter2_IO_AW<'a, 'b> (mapping : 'a -> 'b -> unit) (xs1 : 'a list) (xs2 : 'b list) : unit =      
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2
    
    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | true 
        ->
        ()
    | false
        ->
        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length
        
        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map (fun (x, y) -> async { return mapping x y })
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
        |> Async.Ignore<unit array> 
        |> Async.RunSynchronously
        ()

let iter2_IO_AW_Token<'a,'b,'c> (mapping :'a->'b-> Async<'c>) (token : CancellationToken) (xs1 :'a list) (xs2 :'b list) : unit =

    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | true 
        ->
        ()
    | false
        ->
        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length        
                    
        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map
            (fun (x, y) 
                ->
                async 
                    {
                        token.ThrowIfCancellationRequested ()
                        return! mapping x y
                    }
            )        
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted) |> Async.Ignore<_>
        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        ()

let iter2_IO_AW_Token_Async<'a,'b,'c> (mapping :'a->'b-> Async<'c>) (token : CancellationToken) (xs1 :'a list) (xs2 :'b list) : Async<unit> =

    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | true 
        ->
        async { return () }
    | false
        ->
        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length        
           
        let tasks =  
            List.zip xs1 xs2
            |> Array.ofList
            |> Array.map
                (fun (x, y) 
                    ->
                    async 
                        {
                            token.ThrowIfCancellationRequested ()
                            return! mapping x y
                        }
                )        
        async 
            {
                let! results = Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
                return results |> ignore<'c array> 
            }     

// Using Array.Parallel.map  //TODO otestovat rychlost ve srovnani s Async.Parallel
let map2_CPU_PT<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (xs1 : 'a list) (xs2 : 'b list) : 'c list = //PT pool of threads

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
        List.zip xs1 xs2
        |> List.toArray
        |> Array.Parallel.map (fun (x, y) -> mapping x y)
        |> Array.toList        

/// Version using Async.Parallel for CPU-bound mapping (for performance testing/comparison only)
let map2_CPU2_AW<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (xs1 : 'a list) (xs2 : 'b list) : 'c list =

    match xs1, xs2 with
    | [], _ | _, [] 
        ->
        []
    | _ when List.length xs1 <> List.length xs2
        ->
        []
    | _ ->
        let maxDegree = Environment.ProcessorCount   // standard choice for CPU-bound tasks

        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map (fun (x, y) -> async { return mapping x y })
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
        |> Async.RunSynchronously
        |> Array.toList

 //v mem pripade pro parsing velice mirne, tj. zanedbatelne pomalejsi, nez Array.Parallel (ale mohu aplikovat token)
let map2_CPU2_AW_Token<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (token : CancellationToken) (xs1 : 'a list) (xs2 : 'b list) : 'c list =

    match xs1, xs2 with
    | [], _ | _, [] 
        ->
        []
    | _ when List.length xs1 <> List.length xs2
        ->
        []
    | _ ->
        let maxDegree = Environment.ProcessorCount   // standard choice for CPU-bound tasks

        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map
            (fun (x, y) 
                ->
                async 
                    {
                        token.ThrowIfCancellationRequested ()
                        return mapping x y
                    }
            )        
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        |> Array.toList

let map2_CPU2_AW_Token_Async<'a, 'b, 'c> (mapping : 'a -> 'b -> Async<'c>) (token : CancellationToken) (xs1 : 'a list) (xs2 : 'b list) : Async<'c list> =

    match xs1, xs2 with
    | [], _ | _, [] 
        ->
        async { return [] }

    | _ when List.length xs1 <> List.length xs2
        ->
        async { return [] }

    | _ ->
        let maxDegree = Environment.ProcessorCount   // standard choice for CPU-bound tasks

        let tasks = 
            List.zip xs1 xs2
            |> Array.ofList
            |> Array.map
                (fun (x, y) 
                    ->
                    async 
                        {
                            token.ThrowIfCancellationRequested ()
                            return! mapping x y
                        }
                )  
        async 
            {
                let! results = Async.Parallel(tasks, maxDegreeOfParallelism = maxDegree)
                return results |> Array.toList
            }    

let map2_IO_AW<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (xs1 : 'a list) (xs2 : 'b list) : 'c list =
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2
    
    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | true 
        ->
        []
    | false
        ->
        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length
        
        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map (fun (x, y) -> async { return mapping x y })
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
        |> Async.RunSynchronously
        |> Array.toList

let internal map2_IO_AW_Token<'a, 'b, 'c> (mapping : 'a -> 'b -> 'c) (token : CancellationToken) (xs1 : 'a list) (xs2 : 'b list) : 'c list =
    
    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | true 
        ->
        []
    | false
        ->
        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length        
        
        List.zip xs1 xs2
        |> Array.ofList
        |> Array.map
            (fun (x, y) 
                ->
                async 
                    {
                        token.ThrowIfCancellationRequested ()
                        return mapping x y
                    }
            )        
        |> fun tasks -> Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)           
        |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        |> List.ofArray     
        
let map2_IO_AW_Token_Async<'a,'b,'c> (mapping :'a->'b-> Async<'c>) (token : CancellationToken) (xs1 :'a list) (xs2 :'b list) : Async<'c list> =

    let xs1Length = List.length xs1
    let xs2Length = List.length xs2

    match (xs1Length = 0 || xs2.IsEmpty) || xs1Length <> xs2Length with
    | true 
        ->
        async { return [] }
    | false
        ->
        let maxDegreeOfParallelismAdapted = maxDegreeOfParallelismAdaptedAndroid xs1Length        
           
        let tasks =  
            List.zip xs1 xs2
            |> Array.ofList
            |> Array.map
                (fun (x, y) 
                    ->
                    async 
                        {
                            token.ThrowIfCancellationRequested ()
                            return! mapping x y
                        }
                )        
        async 
            {
                let! results = Async.Parallel(tasks, maxDegreeOfParallelism = maxDegreeOfParallelismAdapted)
                return results |> Array.toList
            } 

// *********************************************************************
// EXPERIMENTAL: Code quotations variants — DO NOT USE IN PRODUCTION
// Kept only for educational comparison
// *********************************************************************

let iter' (action : 'a -> unit) (list : 'a list) =

    match list with
    | [] ->
         ()
    | _  ->
         let listToParallel list = list |> List.iter action        
         
         let l = list |> List.length
            
         let numberOfThreads = numberOfThreads l   
                                         
         let myList = splitListIntoEqualParts numberOfThreads list                             
                  
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
               
            let myList =       
                (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)  
                ||> List.zip   
                                                               
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
           
         let numberOfThreads = numberOfThreads l   
                                         
         let myList : 'a list list = splitListIntoEqualParts numberOfThreads list 
                     
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
               
            let myList =       
                (splitListIntoEqualParts numberOfThreads xs1, splitListIntoEqualParts numberOfThreads xs2)  
                 ||> List.zip                 
                                               
            fun i -> <@ async { return listToParallel (%%expr myList |> List.item %%(expr i)) } @>
            |> (List.length >> List.init <| myList)
            |> List.map _.Compile()       
            |> Async.Parallel  
            |> Async.RunSynchronously
            |> List.ofArray
            |> List.concat

    | true  ->
            []

    (*      
       //// iter_IO in C#

        public static async Task DownloadUrlsAsync(
        IReadOnlyList<string> urls,
        int maxConcurrency = 18)
        {
            if (urls.Count == 0)
                return;

            using var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks =
                urls.Select(async url =>
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        var content = await httpClient.GetStringAsync(url)
                                                        .ConfigureAwait(false);

                        Console.WriteLine($"Got {url}: {content.Length} chars");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

            await Task.WhenAll(tasks).ConfigureAwait(false); //eqv. Async.Parallel
        }      
            
    *)