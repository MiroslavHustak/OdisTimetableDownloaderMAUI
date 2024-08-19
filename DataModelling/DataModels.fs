namespace DataModelling

open System

//*************************

open Types

//Type-driven design

module DataModel = 
    
    type DtDataGet = 
        {           
            NewPrefix : NewPrefix  
            StartDate : StartDateDt
            EndDate : EndDateDt 
            CompleteLink : CompleteLink 
            FileToBeSaved : FileToBeSaved  
            PartialLink : PartialLink
        } 
  
    type DtDataSend = 
        {
            OldPrefix : OldPrefix 
            NewPrefix : NewPrefix 
            StartDate : StartDateDtOpt 
            endDate : EndDateDtOpt 
            TotalDateInterval : TotalDateInterval 
            Suffix : Suffix 
            JsGeneratedString : JsGeneratedString 
            CompleteLink : CompleteLink 
            FileToBeSaved : FileToBeSaved 
            PartialLink : PartialLink
        }