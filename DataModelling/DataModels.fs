namespace DataModelling

open System

open Types

//Type-driven design

module DataModel = 

    type DbDataGet = 
        {            
            completeLink : CompleteLinkOpt
            fileToBeSaved : FileToBeSavedOpt
        }

    type DtDataGet = 
        {           
            newPrefix : NewPrefix  
            startDate : StartDateDt
            endDate : EndDateDt 
            completeLink : CompleteLink 
            fileToBeSaved : FileToBeSaved  
        } 

    type DbDataSend = 
        {
            oldPrefix : OldPrefix 
            newPrefix : NewPrefix 
            startDate : StartDate 
            endDate : EndDate 
            totalDateInterval : TotalDateInterval 
            suffix : Suffix 
            jsGeneratedString : JsGeneratedString 
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