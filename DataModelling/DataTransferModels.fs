namespace DataModelling

module Dtm =
    
    [<Struct>]
    type internal ResponseGetLinks = 
        {
            GetLinks : string
            Message : string
        } 

    [<Struct>]
    type internal ResponseGetLogEntries = 
        {
            GetLogEntries : string
            Message : string
        } 