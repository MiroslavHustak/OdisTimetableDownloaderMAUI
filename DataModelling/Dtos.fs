namespace DataModelling

open System
open System.Data

module Dto = 
    
    type DtDtoGet = 
        {           
            newPrefix : string option  
            startDate : DateTime option 
            endDate : DateTime option  
            completeLink : string option  
            fileToBeSaved : string option  
            partialLink : string option 
        } 
  
    type DtDtoSend = 
        {
            oldPrefix : string 
            newPrefix : string 
            startDate : DateTime  
            endDate : DateTime   
            totalDateInterval : string 
            suffix : string 
            jsGeneratedString : string 
            completeLink : string 
            fileToBeSaved : string  
            partialLink : string  
        }