namespace Types 

open System

//SCDUs for type-driven development (TDD) //TDD not strictly necessary in such a small app

type [<Struct>] CompleteLinkOpt = CompleteLinkOpt of string option
type [<Struct>] FileToBeSavedOpt = FileToBeSavedOpt of string option
type [<Struct>] OldPrefix = OldPrefix of string
type [<Struct>] NewPrefix = NewPrefix of string
type [<Struct>] StartDate = StartDate of string
type [<Struct>] EndDate = EndDate of string
type [<Struct>] TotalDateInterval = TotalDateInterval of string
type [<Struct>] Suffix = Suffix of string
type [<Struct>] JsGeneratedString = JsGeneratedString of string
type [<Struct>] CompleteLink = CompleteLink of string
type [<Struct>] FileToBeSaved = FileToBeSaved of string
type [<Struct>] StartDateDt = StartDateDt of DateTime
type [<Struct>] EndDateDt = EndDateDt of DateTime
type [<Struct>] StartDateDtOpt = StartDateDtOpt of DateTime option
type [<Struct>] EndDateDtOpt = EndDateDtOpt of DateTime option