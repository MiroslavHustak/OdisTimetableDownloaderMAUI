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
    type internal MyBuilder = MyBuilder with //This CE is a monad-like control-flow helper, not a monad
        member _.Recover(m : bool * (unit -> 'a), nextFunc : unit -> 'a) : 'a =
            match m with
            | (false, handleFalse)
                -> handleFalse()
            | (true, _)
                -> nextFunc()    
        member this.Bind(m, f) = this.Recover(m, f) //an alias to prevent confusion      
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x 
        member _.Using(x : 'a, _body: 'a -> 'b) : 'b = _body x    
        member _.Delay(f : unit -> 'a) = f()
        member _.Zero() = ()    
      
    let internal pyramidOfHell = MyBuilder

    //**************************************************************************************
   
    type Builder2 = Builder2 with    // This CE is a monad-like control-flow helper, not a lawful monad
        member _.Recover((m, recovery), nextFunc) =
            match m with
            | Some v -> nextFunc v
            | None   -> recovery    
        member this.Bind(m, f) = this.Recover(m, f) //an alias to prevent confusion        
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x
        member _.Using(resource, binder) =
            use r = resource
            binder r
        
    let internal pyramidOfDoom = Builder2
    
    //**************************************************************************************
       
    type internal MyBuilder3 = MyBuilder3 with  // This CE is a monad-like control-flow helper, not a lawful monad
        member _.Recover(m, nextFunc) = 
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

    type internal MyBuilder5 = MyBuilder5 with   // This CE is a monad-like control-flow helper, not a lawful monad
         member _.Recover(m : bool * 'a, nextFunc : unit -> 'a) : 'a =
             match m with
             | (false, value)
                 -> value
             | (true, _)
                 -> nextFunc() 
         member this.Bind(m, f) = this.Recover(m, f) //an alias to prevent confusion              
         member _.Return x : 'a = x   
         member _.ReturnFrom x : 'a = x 
         member _.Using(x : 'a, _body: 'a -> 'b) : 'b = _body x    
         member _.Delay(f : unit -> 'a) = f()
         member _.Zero() = ()    

    let internal pyramidOfDamnation = MyBuilder5

    //**************************************************************************************
    type internal OptionAdaptedBuilder = OptionAdaptedBuilder with
        member _.Bind(m, nextFunc) =
            match m with
            | Some v -> nextFunc v
            | None   -> None    
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x
        member _.Using(resource, binder) =
            use r = resource
            binder r
    
    let internal option2 = OptionAdaptedBuilder
      
    //**************************************************************************************

    type internal Reader<'e, 'a> = 'e -> 'a
    
    type internal ReaderBuilder = ReaderBuilder with
        member __.Bind(m, f) = fun env -> f (m env) env      
        member __.Return x = fun _ -> x
        member __.ReturnFrom x = x
        //member __.Zero x = x

    let internal reader = ReaderBuilder 

    //**************************************************************************************

    type RealWorld = RealWorldToken
    
    // The IO monad: a function that takes a RealWorld token and returns a new one plus a value
    type IO_Monad<'a> = IO_Monad of (RealWorld -> RealWorld * 'a)

    type IOMonad = IOMonad with

        member _.Bind(io: IO_Monad<'a>, binder: 'a -> IO_Monad<'b>) : IO_Monad<'b> =
            IO_Monad 
                (fun world 
                    ->
                    let runIO_helper (IO_Monad f) = f
                    let world', a = runIO_helper io world
                    runIO_helper (binder a) world'
                )

        member _.Delay(f: unit -> IO_Monad<'a>) : IO_Monad<'a> =
            IO_Monad 
                (fun world
                    ->
                    let (IO_Monad delayed) = f()
                    delayed world
                )
        
        // For do! notation with sequencing (ignoring result)
        member this.Combine(io1: IO_Monad<unit>, io2: IO_Monad<'a>) : IO_Monad<'a> =
            this.Bind(io1, fun () -> io2)
            
        member _.Zero() : IO_Monad<unit> = 
            IO_Monad (fun world -> world, ())

        member _.Return(x) : IO_Monad<'a> =
            IO_Monad (fun world -> world, x)
        
        member _.ReturnFrom(io: IO_Monad<'a>) = io
        
    let internal IOMonad = IOMonad

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