namespace Types

open System.Threading

module Haskell_IO_Monad_Simulation =    
    
    type [<Struct>] internal IO<'a> = IO of (unit -> 'a) // wrapping custom type simulating Haskell's IO Monad (without the monad, of course)

    let internal runIO (IO action) = action () 
    let internal runIOAsync (IO action) : Async<'a> = async { return action () }

    let private bind (IO action) (f : 'a -> IO<'b>) : IO<'b> = //educational code
        IO (fun () 
                ->
                let a = action ()
                let (IO action2) = f a
                action2 ()
        )

    let internal impureFn = 
           IO ( (fun () -> printfn "Impure function executed")) //educational code

    let private result_test = runIO impureFn //educational code

    //*****************************************
    
    // Simulating Haskell's RealWorld token for better purity representation 
    // Educational code
    type RealWorld = RealWorld 

    type IO2<'a> = IO2 of (RealWorld -> RealWorld * 'a)
     
    let runIO2 (IO2 f) =
        let (RealWorld, value) = f RealWorld
        value
            
    let private bind2 (IO2 fa) f =
        IO2 (fun w0 
                ->
                let (w1, a) = fa w0
                let (IO2 fb) = f a
                fb w1
        )  

    //*****************************************

    //not used yet
    let internal returnIO x = IO (fun () -> x)
    let internal bindIO (IO f) g = IO (fun () -> (runIO (g (f ()))))
    let internal mapIO f io = bindIO io (f >> returnIO)

module FreeMonad =
   
    type [<Struct>] internal FreeMonad<'a> = FreeMonad of (unit -> 'a) 

    let internal runFreeMonad (FreeMonad action) = action ()

module Types =     
   
    type internal Context<'a, 'b, 'c> = 
        {
            listMappingFunction : ('a -> 'b -> 'c) -> 'a list -> 'b list -> 'c list
            reportProgress : (float * float ) -> unit
            dir : string
            list : (string * string) list
        }
                    
    type [<Struct>] internal MsgIncrement =
        | Inc of int  
           
    type ConnectivityMessage =
        | UpdateState of bool
        | CheckState of AsyncReplyChannel<bool>    

    type CancellationMessage =
        | UpdateState2 of bool * CancellationTokenSource
        | CheckState2 of AsyncReplyChannel<CancellationToken option>    

    type [<Struct>] internal Validity =
        | CurrentValidity 
        | FutureValidity 
        | LongTermValidity  

    type internal ConfigMHD = 
        {
            source : string
            destination : string
        }

    type internal ConfigKodis = 
        {
            source1 : string
            source2 : string
            source3 : string
            destination : string
        }