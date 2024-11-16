namespace DataModelling

open System

module Dto =
    
    [<Struct>]
    type internal ResponseGet = 
        {
            GetLinks : string
            Message : string
        } 