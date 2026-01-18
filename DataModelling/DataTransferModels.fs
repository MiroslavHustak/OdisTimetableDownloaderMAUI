namespace DataModelling

module Dtm =
    
    type internal ResponseGetLinks = 
        {
            GetLinks : string
            Message : string
        } 

    type internal ResponseGetLogEntries = 
        {
            GetLogEntries : string
            Message : string
        } 

    type internal ResponsePut = 
        {
            Message1 : string
            Message2 : string
        }