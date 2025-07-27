namespace Types

open System.Threading

module Haskell_IO_Monad_Simulation =
    
    [<Struct>]
    type internal IO<'a> = IO of (unit -> 'a) // wrapping custom type simulating Haskell's IO Monad (without the monad, of course)

    let internal runIO (IO action) = action ()

    let internal runIOAsync (IO action) : Async<'a> = async { return action () }

    //not used yet
    let internal returnIO x = IO (fun () -> x)
    let internal bindIO (IO f) g = IO (fun () -> (runIO (g (f ()))))
    let internal mapIO f io = bindIO io (f >> returnIO)

module FreeMonad =
    
    [<Struct>]
    type internal FreeMonad<'a> = FreeMonad of (unit -> 'a) 

    let internal runFreeMonad (FreeMonad action) = action ()

module Types =

    [<Struct>]   //vhodne pro 16 bytes => 4096 characters
    type internal ODIS =  
        {        
            OdisDir1 : string
            OdisDir2 : string
            //OdisDir3 : string
            OdisDir4 : string
            OdisDir5 : string
            OdisDir6 : string
        }               
   
    type internal Context<'a, 'b, 'c> = 
        {
            listMappingFunction : ('a -> 'b -> 'c) -> 'a list -> 'b list -> 'c list
            reportProgress : (float * float ) -> unit
            dir : string
            list : (string * string) list
        }
    
    [<Struct>]
    type internal MsgIncrement =
        | Inc of int  
           
    type ConnectivityMessage =
        | UpdateState of bool
        | CheckState of AsyncReplyChannel<bool>    

    type CancellationMessage =
        | UpdateState2 of bool * CancellationTokenSource
        | CheckState2 of AsyncReplyChannel<CancellationToken option>    

    [<Struct>]
    type internal Validity =
        | CurrentValidity 
        | FutureValidity 
        | WithoutReplacementService  

    [<Struct>]
    type internal ConfigMHD =
        {
            source : string
            destination : string
        }

    [<Struct>]
    type internal ConfigKodis =
        {
            source1 : string
            source2 : string
            source3 : string
            destination : string
        }