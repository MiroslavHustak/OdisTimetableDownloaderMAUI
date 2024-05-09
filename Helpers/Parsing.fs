namespace Helpers

module TryParserInt = //Adapted third-party code

     let private tryParseWith (tryParseFunc: string -> bool * _) =
         tryParseFunc >> function
         | true, value -> Some value
         | false, _    -> None

     let internal parseInt () = tryParseWith <| System.Int32.TryParse  
     //let (|Int|_|) = parseInt  
     
module TryParserDate = //tohle je pro parsing textoveho retezce do DateTime, ne pro overovani new DateTime() //Adapted third-party code

       let private tryParseWith (tryParseFunc: string -> bool * _) =
           tryParseFunc >> function
           | true, value -> Some value
           | false, _    -> None

       let internal parseDate () = tryParseWith <| System.DateTime.TryParse 
       //let (|Date|_|) = parseDate                 
                                    
//**************************************************************************************************                                  
//Toto neni pouzivany kod, ale jen pattern pro tvorbu TryParserInt, TryParserDate atd. //Adapted third-party code.
module private TryParser =

     let private tryParseWith (tryParseFunc: string -> bool * _) = 
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