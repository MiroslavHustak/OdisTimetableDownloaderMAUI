namespace Types 

open System

//SCDUs for type-driven development (TDD)  

type [<Struct>] internal CompleteLinkOpt = CompleteLinkOpt of string option
type [<Struct>] internal FileToBeSavedOpt = FileToBeSavedOpt of string option
type [<Struct>] internal OldPrefix = OldPrefix of string
type [<Struct>] internal NewPrefix = NewPrefix of string
type [<Struct>] internal TotalDateInterval = TotalDateInterval of string
type [<Struct>] internal Suffix = Suffix of string
type [<Struct>] internal JsGeneratedString = JsGeneratedString of string
type [<Struct>] internal CompleteLink = CompleteLink of string
type [<Struct>] internal PartialLink = PartialLink of string
type [<Struct>] internal FileToBeSaved = FileToBeSaved of string
type [<Struct>] internal StartDateRc = StartDateRc of DateTime
type [<Struct>] internal EndDateRc = EndDateRc of DateTime
type [<Struct>] internal StartDateRcOpt = StartDateRcOpt of DateTime option
type [<Struct>] internal EndDateRcOpt = EndDateRcOpt of DateTime option