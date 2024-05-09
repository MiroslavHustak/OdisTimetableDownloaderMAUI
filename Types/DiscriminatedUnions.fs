namespace Types 

[<Struct>]
type internal Validity =
    | CurrentValidity 
    | FutureValidity 
    //| ReplacementService 
    | WithoutReplacementService 

//for educational code only
type internal Msg =
    | Incr of int
    | Fetch of AsyncReplyChannel<int>

[<Struct>]
type internal MsgIncrement =
    | Inc of int  

[<Struct>]
type MailboxMessage =
    | First of int  