namespace DataModelling

open System
open System.Data

module Dto = 
    
    type DtDtoGet = 
        {           
            NewPrefix : string option  
            StartDate : DateTime option 
            EndDate : DateTime option  
            CompleteLink : string option  
            FileToBeSaved : string option  
            PartialLink : string option 
        } 
  
    type DtDtoSend = 
        {
            OldPrefix : string 
            NewPrefix : string 
            StartDate : DateTime  
            EndDate : DateTime   
            TotalDateInterval : string 
            Suffix : string 
            JsGeneratedString : string 
            CompleteLink : string 
            FileToBeSaved : string  
            PartialLink : string  
        }