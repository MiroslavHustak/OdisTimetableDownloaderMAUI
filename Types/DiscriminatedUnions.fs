namespace Types 

open ErrorTypes

[<Struct>]
type internal Validity =
    | CurrentValidity 
    | FutureValidity 
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

[<Struct>]
type internal DownloadResult =
    | Downloaded of Downloaded : Result<unit, PdfDownloadErrors>
    | NoPdfForGivenVariant of NoPdfForGivenVariant : Result<unit, PdfDownloadErrors>
