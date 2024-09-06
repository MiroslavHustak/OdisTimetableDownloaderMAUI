namespace DataModelling

open System

//*************************

open Types

//Type-driven design

module DataModel = 
        
    type internal RcData = 
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