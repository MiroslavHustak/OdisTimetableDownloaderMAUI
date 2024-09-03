namespace Helpers

module LogicalAliases =      

    let internal xor a b = (a && not b) || (not a && b)   
        
    (*
    let rec internal nXor operands =
        match operands with
        | []      -> false  
        | x :: xs -> (x && not (nXor xs)) || ((not x) && (nXor xs))
    *)

    [<TailCall>]
    let internal nXor operands =
        let rec nXor_tail_recursive acc operands =
            match operands with
            | []      -> acc
            | x :: xs -> nXor_tail_recursive ((x && not acc) || ((not x) && acc)) xs
        nXor_tail_recursive false operands
       
module MyString = 
        
    open System
    
    [<CompiledName "CreateStringSeq">]      
    let internal createStringSeq (numberOfStrings: int, stringToAdd: string): string = 
        
        let initialString = String.Empty   //initial value of the string
        let listRange = [ 1 .. numberOfStrings ] 

        //[<TailCall>]
        let rec loop list acc =
            match list with 
            | []        ->
                         acc
            | _ :: tail -> 
                         let finalString = (+) acc stringToAdd  
                         loop tail finalString  //Tail-recursive function calls that have their parameters passed by the pipe operator are not optimized as loops #6984
    
        loop listRange initialString
        
    //List.reduce nelze, tam musi byt stejny typ acc a range      
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings: int, stringToAdd: string): string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty
                  
module CheckNetConnection =  

    open System.Net.NetworkInformation
    
    open Helpers
      
    let internal checkNetConn (timeout : int) =                 
       
        try
            use myPing = new Ping()      
                
            let host : string = "8.8.4.4" //IP google.com
            let buffer : byte[] = Array.zeroCreate <| 32
            
            let pingOptions: PingOptions = new PingOptions()                
     
            myPing.Send(host, timeout, buffer, pingOptions)
            |> (Option.ofNull >> Option.bind 
                    (fun pingReply -> 
                                    Option.fromBool
                                        (pingReply |> ignore) 
                                        ((=) pingReply.Status IPStatus.Success)                                           
                    )
               ) 
        with
        | ex ->
              ()//logInfoMsg <| sprintf "Err110 %s" (string ex.Message)
              None   