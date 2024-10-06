namespace Types 

[<Struct>]
type internal Validity =
    | CurrentValidity 
    | FutureValidity 
    | WithoutReplacementService 

[<Struct>]
type internal MsgIncrement =
    | Inc of int  

[<Struct>]
type MailboxMessage =
    | First of int  