namespace Types 

open System

//SCDUs for type-driven development (TDD)  

// [<Struct>] does not help or makes even things worse
type internal CompleteLinkOpt = CompleteLinkOpt of string option
type internal FileToBeSavedOpt = FileToBeSavedOpt of string option
type internal OldPrefix = OldPrefix of string
type internal NewPrefix = NewPrefix of string
type internal TotalDateInterval = TotalDateInterval of string
type internal Suffix = Suffix of string
type internal JsGeneratedString = JsGeneratedString of string
type internal CompleteLink = CompleteLink of string
type internal PartialLink = PartialLink of string
type internal FileToBeSaved = FileToBeSaved of string
type internal StartDateRc = StartDateRc of DateTime
type internal EndDateRc = EndDateRc of DateTime
type internal StartDateRcOpt = StartDateRcOpt of DateTime option
type internal EndDateRcOpt = EndDateRcOpt of DateTime option

(*
Only use [<Struct>] SCDUs when:You're wrapping pure value types (int, float, bool, small structs).
Or using option<'T> where 'T is a struct.
*)