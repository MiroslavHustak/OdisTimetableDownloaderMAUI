namespace DataModelling

open System
open System.Data

module Dto = 

    type DbDtoGet = 
        {
            completeLink : string option             
            fileToBeSaved : string option  
        }

    type DtDtoGet = 
        {           
            newPrefix : string option  
            startDate : DateTime option 
            endDate : DateTime option  
            completeLink : string option  
            fileToBeSaved : string option  
        } 

    type DbDtoSend = 
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
        }