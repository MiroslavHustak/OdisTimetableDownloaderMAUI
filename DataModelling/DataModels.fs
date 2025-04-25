namespace DataModelling

open Types

module DataModel = 

    [<Struct>]
    type internal RcData = //Type-driven development
        {
            OldPrefixRc : OldPrefix 
            NewPrefixRc : NewPrefix 
            StartDateRc : StartDateRcOpt 
            EndDateRc : EndDateRcOpt 
            TotalDateIntervalRc : TotalDateInterval 
            SuffixRc : Suffix 
            JsGeneratedStringRc : JsGeneratedString 
            CompleteLinkRc : CompleteLink 
            FileToBeSavedRc : FileToBeSaved 
            PartialLinkRc : PartialLink
        }