namespace Types

open System

//*******************

open System.Threading

module Haskell_IO_Monad_Simulation =    
    
    type [<Struct>] internal IO<'a> = IO of (unit -> 'a) // wrapping custom type simulating Haskell's IO Monad (without the monad, of course)

    let internal runIO (IO action) = action ()

    let internal runIOAsync (IO action) : Async<'a> = async { return action () }

    //not used yet
    let internal returnIO x = IO (fun () -> x)
    let internal bindIO (IO f) g = IO (fun () -> (runIO (g (f ()))))
    let internal mapIO f io = bindIO io (f >> returnIO)

module FreeMonad =
   
    type [<Struct>] internal FreeMonad<'a> = FreeMonad of (unit -> 'a) 

    let internal runFreeMonad (FreeMonad action) = action ()

module Types =

    type internal Index = | I1 | I2 | I3   //3 x 3 = 9   
    
    type internal GridFunction<'a> = { board : Index -> Index -> 'a }         

    let internal defaultGridFunction (defaultValue : 'a) : GridFunction<'a> =
        {
            board = fun _ _ -> defaultValue
        }     
    
    type [<Struct>] internal ODIS = { board : GridFunction<string> }    
   
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
        | WithoutReplacementService  

    type [<Struct>] internal ConfigMHD = 
        {
            source : string
            destination : string
        }

    type [<Struct>] internal ConfigKodis = 
        {
            source1 : string
            source2 : string
            source3 : string
            destination : string
        }