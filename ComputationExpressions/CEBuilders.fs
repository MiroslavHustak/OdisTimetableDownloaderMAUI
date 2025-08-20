namespace Helpers

module Builders =

    //**************************************************************************************

    // A minimal IO<'a> computation expression in F# that mimics Haskell's IO monad

    open Types.Haskell_IO_Monad_Simulation
        
    type internal IOBuilder = IOBuilder with  //for testing purposes only, not used in the code from 09-08-2025 onwards
        member _.Bind(m : IO<'a>, f : 'a -> IO<'b>) : IO<'b> =
                   IO (fun ()
                           ->
                           let a = runIO m
                           runIO (f a)
                   )
        member _.Return(x : 'a) : IO<'a> = IO(fun () -> x)
        member _.ReturnFrom(x : 'a) = x
       
        member _.Zero(x : 'a) : IO<'a> = IO(fun () -> x)

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
                     
    [<Struct>]
    type internal MyBuilder3 = MyBuilder3 with       
        member _.Bind(resultExpr, nextFunc) = 
            match fst resultExpr with
            | Ok value  -> nextFunc value 
            | Error err -> (snd resultExpr) err
        member _.Zero () = ()       
        member _.Return x = x  
        member _.ReturnFrom x : 'a = x 
        member _.TryWith(body, catch) =
            try body()
            with ex -> catch ex   
     
    let internal pyramidOfInferno = MyBuilder3    
    
    //**************************************************************************************

    [<Struct>]
    type internal MyBuilder = MyBuilder with    
        member _.Bind(condition, nextFunc) =
            match fst condition with
            | false -> snd condition
            | true  -> nextFunc()  
        member _.Return x = x
        member _.ReturnFrom x : 'a = x 
        member _.Using x = x

    let internal pyramidOfHell = MyBuilder

    //**************************************************************************************

    [<Struct>]
    type internal Builder2 = Builder2 with    
        member _.Bind((optionExpr, err), nextFunc) =
            match optionExpr with
            | Some value -> nextFunc value 
            | _          -> err  
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x 
        member _.Using(resource, binder) =
            use r = resource
            binder r
        (*
        member _.TryFinally(body, compensation) =
            try body()
            finally compensation()
        member _.Zero () = ()        
        member _.TryWith(body, catch) =
            try body()
            with ex -> catch ex   
        *)
      
    let internal pyramidOfDoom = Builder2
    
    //**************************************************************************************

    type internal Reader<'e, 'a> = 'e -> 'a
    
    [<Struct>] 
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