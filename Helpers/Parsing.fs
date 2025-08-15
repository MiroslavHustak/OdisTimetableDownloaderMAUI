namespace Helpers

module TryParserInt = 

    // pragmatically pure if not concerned about culture-dependent outcomes
    let private tryParseWith (tryParseFunc : string -> bool * 'a) : string -> 'a option =
        tryParseFunc >> function
        | true, value -> Some value
        | false, _    -> None
     
    let internal parseInt : string -> int option = tryParseWith System.Int32.TryParse
    //let (|Int|_|) = parseInt  
     
module TryParserDate = //tohle je pro parsing textoveho retezce do DateTime, ne pro overovani new DateTime() 
       
    // pragmatically pure if not concerned about culture-dependent outcomes
    let private tryParseWith (tryParseFunc : string -> bool * 'a) : string -> 'a option =
        tryParseFunc >> function
        | true, value -> Some value
        | false, _    -> None
       
    let internal parseDate : string -> System.DateTime option =
        tryParseWith System.DateTime.TryParse       
    //let (|Date|_|) = parseDate                 
                                    
//**************************************************************************************************                                  
//Toto neni pouzivany kod, ale jen pattern pro tvorbu TryParserInt, TryParserDate atd. 
module private TryParser =

    let private tryParseWith (tryParseFunc : string -> bool * _) = 
        tryParseFunc >> function
        | true, value -> Some value
        | false, _    -> None

    let internal parseDate   = tryParseWith <| System.DateTime.TryParse
    let internal parseInt    = tryParseWith <| System.Int32.TryParse
    let internal parseSingle = tryParseWith <| System.Single.TryParse
    let internal parseDouble = tryParseWith <| System.Double.TryParse
    // etc.

    // active patterns for try-parsing strings
    let internal (|Date|_|)   = parseDate
    let internal (|Int|_|)    = parseInt
    let internal (|Single|_|) = parseSingle
    let internal (|Double|_|) = parseDouble

     (*
     open System
     open System.Globalization
     
     let private tryParseWithCulture (tryParseFunc : string -> IFormatProvider -> bool * _) (culture : CultureInfo) =
         fun s -> tryParseFunc s culture |> function
             | true, value -> Some value
             | false, _    -> None
     
     let internal parseDate (culture : CultureInfo) : string -> Option<DateTime> =
         tryParseWithCulture DateTime.TryParse culture
     *)