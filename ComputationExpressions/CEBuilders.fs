namespace Helpers

module Builders =

    //**************************************************************************************

    // A minimal IO<'a> computation expression in F# that mimics Haskell's IO monad

    open Types.Haskell_IO_Monad_Simulation
        
    type internal IOBuilder = IOBuilder with  //for testing purposes only, not used in the code from Aug 9, 2025 onwards
        member _.Bind(m : IO<'a>, f : 'a -> IO<'b>) : IO<'b> =
            IO (fun ()
                    ->
                    let a = runIO m
                    runIO (f a)
            )
        member _.Zero(x : 'a) : IO<'a> = IO (fun () -> x)
        member _.Return(x : 'a) : IO<'a> = IO (fun () -> x)
        member _.ReturnFrom(x : 'a) = x    

    let internal io = IOBuilder

    (*
    getLine :: IO String
    getLine = 
        getChar >>= \c ->
            if c == '\n'
                then return ""
                else getLine >>= \l -> return (c : l)
    *)

    //**************************************************************************************
        
    //[<Struct>] does not help
    type internal MyBuilder = MyBuilder with 
        member _.Bind(m : bool * (unit -> 'a), nextFunc : unit -> 'a) : 'a =
            match m with
            | (false, handleFalse)
                -> handleFalse()
            | (true, _)
                -> nextFunc()    
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x 
        member _.Using(x : 'a, _body: 'a -> 'b) : 'b = _body x    
        member _.Delay(f : unit -> 'a) = f()
        member _.Zero() = ()    
      
    let internal pyramidOfHell = MyBuilder

    //**************************************************************************************
   
    type Builder2 = Builder2 with    
        member _.Bind((m, recovery), nextFunc) =
            match m with
            | Some v -> nextFunc v
            | None   -> recovery    
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x
        member _.Using(resource, binder) =
            use r = resource
            binder r
        
    let internal pyramidOfDoom = Builder2
    
    //**************************************************************************************
       
    type internal MyBuilder3 = MyBuilder3 with   
        member _.Recover(m, nextFunc) = //neni monada, nesplnuje vsechny 3 monadicke zakony   
            match m with
            | (Ok v, _)           
                -> nextFunc v 
            | (Error err, handler) 
                -> handler err
        member this.Bind(m, f) = this.Recover(m, f) //an alias to prevent confusion        
        member _.Zero () = ()       
        member _.Return x = x
        member _.ReturnFrom x = x     
        
    let internal pyramidOfInferno = MyBuilder3  

    //**************************************************************************************
       
    type internal MyBuilder4 = MyBuilder4 with 
        member _.Bind(m, nextFunc) =
            match m with
            | Ok v 
                -> nextFunc v  
            | Error e
                -> Error e           
        member _.Return x = x   //oproti result CE nema OK
        member _.ReturnFrom x : 'a = x
        
    let internal pyramidOfAbbys = MyBuilder4  //nepouzivano, nahrazeno result CE z FsToolkit.ErrorHandling s return!

    //**************************************************************************************

    type internal MyBuilder5 = MyBuilder5 with    
         member _.Bind(m : bool * 'a, nextFunc : unit -> 'a) : 'a =
             match m with
             | (false, value)
                 -> value
             | (true, _)
                 -> nextFunc()    
         member _.Return x : 'a = x   
         member _.ReturnFrom x : 'a = x 
         member _.Using(x : 'a, _body: 'a -> 'b) : 'b = _body x    
         member _.Delay(f : unit -> 'a) = f()
         member _.Zero() = ()    

    let internal pyramidOfDamnation = MyBuilder5
      
    //**************************************************************************************

    type internal Reader<'e, 'a> = 'e -> 'a
    
    type internal ReaderBuilder = ReaderBuilder with
        member __.Bind(m, f) = fun env -> f (m env) env      
        member __.Return x = fun _ -> x
        member __.ReturnFrom x = x
        //member __.Zero x = x

    let internal reader = ReaderBuilder 

    //**************************************************************************************

    let [<Literal>] MinLengthCE = 2 //Builder pro CE vyzaduje velke pismeno.... strange
    let [<Literal>] MaxLengthCE = 3 
       
    type internal XorBuilder = XorBuilder with
    
        member _.Yield(value : bool) = [ value ]
        member _.Combine(previous : bool list, following : bool list) = previous @ following
        member _.Delay(func : unit -> bool list) = func() 
    
        member _.Run(values : bool list) =   

            match values.Length with
            | MinLengthCE
                ->
                let a = values |> List.item 0
                let b = values |> List.item 1
                Ok ((a && not b) || (not a && b)) // XOR logic for 2 values

            | MaxLengthCE 
                ->
                let a = values |> List.item 0
                let b = values |> List.item 1
                let c = values |> List.item 2
                Ok ((a && not b && not c) || (not a && b && not c) || (not a && not b && c)) // XOR logic for 3 values

            | _ ->
                Error "Invalid number of values for XOR computation"
    
        member _.Zero() = []

    let internal xor = XorBuilder
    
    (*         
    ┌─────────────────────────────┬───────────────────────────────┬─────────────────────┬───────────────────┬─────────┐
    │ Kind                        │ Examples in F# stdlib         │ Type of yield / let!│ Must preserve     │ Lawful? │
    │                             │                               │                     │ wrapper?          │         │
    ├─────────────────────────────┼───────────────────────────────┼─────────────────────┼───────────────────┼─────────┤
    │ Wrapped / Container style   │ option { }, async { },        │ option<'a>          │ Yes               │ YES     │
    │                             │ Choice<'a,'b> { }, Result { } │ Async<'a>, etc.     │                   │         │
    ├─────────────────────────────┼───────────────────────────────┼─────────────────────┼───────────────────┼─────────┤
    │ Kleisli / Continuation      │ seq { }, list { },            │ plain 'a            │ No (only          │ YES     │
    │ style (collapse-to-value)   │ array { }, task { },          │                     │ observable        │         │
    │                             │ your recover / conditional    │                     │ behaviour matters)│         │
    │                             │ builders                      │                     │                   │         │
    └─────────────────────────────┴───────────────────────────────┴─────────────────────┴───────────────────┴─────────┘
       
    Key insight:
        • In the Kleisli/continuation style (seq, list, your recover builder, etc.)
            the monad laws are judged ONLY by observable results, NOT by internal
            representation or hidden metadata that does not affect future behaviour.
       
        • Right identity holds if  m >>= return   produces the same values/effects
            as m — even if internal thunks, enumerators, or recovery values differ.
       
        • This is exactly how the F# standard library implements seq { }, list { },
            array { } and task { } — all are 100% lawful monads despite returning
            plain values from Return and Bind.  

     //Thunks are primarily used to delay a calculation until its result is needed, or to insert operations at the beginning or end of the other subroutine. 
    *)   