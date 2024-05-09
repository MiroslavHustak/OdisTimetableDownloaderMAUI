namespace DataModelling

open System

open Types

//Type-driven design

module DataModel = 
   
    type DtDataGet = 
        {           
            newPrefix : NewPrefix  
            startDate : StartDateDt
            endDate : EndDateDt 
            completeLink : CompleteLink 
            fileToBeSaved : FileToBeSaved  
        } 
   
    type DtDataSend = 
        {
            oldPrefix : OldPrefix 
            newPrefix : NewPrefix 
            startDate : StartDateDtOpt 
            endDate : EndDateDtOpt 
            totalDateInterval : TotalDateInterval 
            suffix : Suffix 
            jsGeneratedString : JsGeneratedString 
            completeLink : CompleteLink 
            fileToBeSaved : FileToBeSaved 
        }