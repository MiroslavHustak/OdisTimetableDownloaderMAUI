namespace Helpers
       
module MyString = 
        
    open System    
      
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings: int, stringToAdd: string): string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty
                  
module CheckNetConnection =  

    open System.Net.NetworkInformation

    //*****************************************
    
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
              string ex.Message |> ignore    //TODO logfile
              None   