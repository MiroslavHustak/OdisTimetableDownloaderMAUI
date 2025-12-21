namespace Filtering

open System
open System.Text.RegularExpressions

open FsToolkit.ErrorHandling

//************************************************************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Helpers
open Helpers.MyString
open Helpers.Builders
open Helpers.Validation

open Settings.SettingsKODIS
open Settings.SettingsGeneral

open Api.Logging
open DataModelling.DataModel

module FilterTimetableLinks =  

    // Array vubec rychlost nezvysilo
    
    let internal filterTimetableLinks param (pathToDir : string) (diggingResult : Result<string list, JsonParsingAndPdfDownloadErrors>) = 

        IO (fun () //mozna overkill - je to quli Regexu, u ktereho je impurity nejednoznacna, zbytek je dle mne pragmatically pure
                ->      

                //*************************************Helpers for SQL columns********************************************

                let datePatternRegex = Regex(@"202[3-9]_[0-1][0-9]_[0-3][0-9]_202[4-9]_[0-1][0-9]_[0-3][0-9]", RegexOptions.Compiled)

                let extractSubstring (input : string) =
            
                    try
                        match (datePatternRegex.Match input).Success with
                        | true  -> Ok input 
                        | false -> Ok String.Empty 
                    with 
                    | ex -> Error <| string ex.Message        
                  
                    |> Result.defaultWith
                        (fun err 
                            -> 
                            runIO (postToLog <| err <| "#108")
                            String.Empty
                        )
        
                let extractSubstring1 (input : string) =

                    try
                        match (datePatternRegex.Match input).Success with
                        | true  -> Ok (datePatternRegex.Match input).Value
                        | false -> Ok String.Empty
                       
                    with 
                    | ex -> Error <| string ex.Message                    

                    |> Result.defaultWith
                        (fun err 
                            -> 
                            runIO (postToLog <| err <| "#109")
                            String.Empty
                        )

                let extractSubstring2 (input : string) : (string option * int) =

                    let prefix = "NAD_"
            
                    match input.StartsWith prefix with
                    | false
                        ->
                        (None, 0)

                    | true
                        ->
                        let startIdx = prefix.Length
                            in
                            let restOfString = input.Substring startIdx
                                in
                                match restOfString.IndexOf '_' with
                                | -1 -> (None, 0)

                                | idx 
                                    when
                                        idx > 0 
                                            ->
                                            let result = restOfString.Substring(0, idx)
                                            (Some result, result.Length)

                                | _ -> (None, 0)

                //zamerne nepouzivam jednotny kod pro NAD (extractSubstring2) a X - pro pripad, ze KODIS zase neco zmeni
                let extractSubstring3 (input: string) : (string option * int) =

                    match input with            
                    | _ 
                        when 
                            input.[0] = 'X'
                                ->
                                match input.IndexOf '_' with
                                | index 
                                    when 
                                        index > 1
                                            -> 
                                            let result = input.Substring(1, index - 1)
                                                in
                                                (Some result, result.Length)

                                | _ -> (None, 0)

                    | _ -> (None, 0)       

                let extractStartDate (input : string) =

                     let result = 
                         match input.Equals String.Empty with
                         | true  -> String.Empty
                         | _     -> input.[0..min 9 (input.Length - 1)] 
                     result.Replace("_", "-")
         
                let extractEndDate (input : string) =

                    let result = 
                        match input.Equals String.Empty with
                        | true  -> String.Empty
                        | _     -> input.[max 0 (input.Length - 10)..]
                    result.Replace("_", "-")

                let splitString (input : string) =   

                    match input.StartsWith pathKodisAmazonLink with
                    | true  -> [ pathKodisAmazonLink; input.Substring pathKodisAmazonLink.Length ]
                    | false -> [ pathKodisAmazonLink; input ]

                //*************************************Splitting Kodis links into DataTable columns********************************************
                let splitKodisLink input =

                    let oldPrefix = 
                        try
                            Regex.Split(input, extractSubstring1 input) 
                            |> Array.toList
                            |> List.item 0
                            |> splitString
                            |> List.item 1
                            |> Ok
                        with 
                        | ex -> Error <| string ex.Message
                     
                        |> Result.defaultWith
                            (fun err 
                                -> 
                                runIO (postToLog <| err <| "#110")
                                String.Empty
                            )

                    let totalDateInterval = extractSubstring1 input

                    let partAfter =
                        try
                            Regex.Split(input, totalDateInterval)
                            |> Array.toList
                            |> List.item 1 
                            |> Ok
                        with 
                        | ex -> Error <| string ex.Message     
                         
                        |> Result.defaultWith
                            (fun err 
                                -> 
                                runIO (postToLog <| err <| "#111")
                                String.Empty
                            )
        
                    let vIndex = partAfter.IndexOf "_v"
                    let tIndex = partAfter.IndexOf "_t"

                    let suffix = 
                        match [vIndex; tIndex].Length = -2 with
                        | false when vIndex <> -1 -> partAfter.Substring(0, vIndex + 2)
                        | false when tIndex <> -1 -> partAfter.Substring(0, tIndex + 2)
                        | _                       -> String.Empty
           
                    let jsGeneratedString =
                        match [vIndex; tIndex].Length = -2 with
                        | false when vIndex <> -1 -> partAfter.Substring(vIndex + 2)
                        | false when tIndex <> -1 -> partAfter.Substring(tIndex + 2)
                        | _                       -> partAfter
        
                    let newPrefix (oldPrefix : string) =

                        let conditions =
                            [
                                fun () -> oldPrefix.Contains("AE") && oldPrefix.Length = 3
                                fun () -> oldPrefix.Contains("S") && oldPrefix.Length = 3
                                fun () -> oldPrefix.Contains("S") && oldPrefix.Length = 4
                                fun () -> oldPrefix.Contains("R") && oldPrefix.Length = 3
                                fun () -> oldPrefix.Contains("R") && oldPrefix.Length = 4
                                fun () -> oldPrefix.Contains("NAD")
                                fun () -> oldPrefix.Contains("X")
                                fun () -> oldPrefix.Contains("P")
                            ]

                        match List.filter (fun condition -> condition()) conditions with
                        | [ _ ] 
                            -> 
                            let index = conditions |> List.findIndex (fun item -> item () = true) //neni treba tryFind, bo v [ _ ] je vzdy neco
                     
                            match index with
                            | 0 | 1 | 3  
                                -> 
                                sprintf "_%s" oldPrefix                    

                            | 5 
                                -> 
                                let newPrefix =                                 
                                    match oldPrefix |> extractSubstring2 with
                                    | (Some value, length)
                                        when length <= lineNumberLength -> sprintf "NAD%s%s_" <| createStringSeqFold(lineNumberLength - length, "0") <| value
                                    | _                                 -> oldPrefix                                 
                                oldPrefix.Replace(oldPrefix, newPrefix) 
                        
                            | 6  
                                -> 
                                let newPrefix = //ponechat podobny kod jako vyse, nerobit refactoring, KODIS moze vse nekdy zmenit                                
                                    match oldPrefix |> extractSubstring3 with
                                    | (Some value, length)
                                        when length <= lineNumberLength -> sprintf "X%s%s_" <| createStringSeqFold(lineNumberLength - length, "0") <| value
                                    | _                                 -> oldPrefix                                 
                                oldPrefix.Replace(oldPrefix, newPrefix)

                            | 2 | 4 | _
                                ->
                                sprintf "%s" oldPrefix

                        | _     
                            ->
                            match oldPrefix.Length with                    
                            | 2  -> sprintf "%s%s" <| createStringSeqFold(2, "0") <| oldPrefix   //sprintf "00%s" oldPrefix
                            | 3  -> sprintf "%s%s" <| createStringSeqFold(1, "0") <| oldPrefix   //sprintf "0%s" oldPrefix                  
                            | _  -> oldPrefix
                          
                    let input = 
                        match input.Contains "_t" with 
                        | true  -> input.Replace(pathKodisAmazonLink, sprintf"%s%s" pathKodisAmazonLink @"timetables/").Replace("_t.pdf", ".pdf") 
                        | false -> input   
                
                    let fileToBeSaved = sprintf "%s%s%s.pdf" (newPrefix oldPrefix) totalDateInterval suffix

                    {
                        OldPrefixRc = OldPrefix oldPrefix
                        NewPrefixRc = NewPrefix (newPrefix oldPrefix)
                        StartDateRc = StartDateRcOpt (TryParserDate.parseDate <| extractStartDate totalDateInterval)
                        EndDateRc = EndDateRcOpt (TryParserDate.parseDate <| extractEndDate totalDateInterval)
                        TotalDateIntervalRc = TotalDateInterval totalDateInterval
                        SuffixRc = Suffix suffix
                        JsGeneratedStringRc = JsGeneratedString jsGeneratedString
                        CompleteLinkRc = CompleteLink input
                        FileToBeSavedRc = FileToBeSaved fileToBeSaved
                        PartialLinkRc = 
                            let pattern = Regex.Escape jsGeneratedString
                            PartialLink <| Regex.Replace(input, pattern, String.Empty)
                    }
     
                //**********************Filtering********************************************************
                let dataToBeFiltered : Result<RcData list, JsonParsingAndPdfDownloadErrors> = 

                    diggingResult   
                    |> Result.map
                        (fun value 
                            -> 
                            value
                            |> List.Parallel.map_CPU 
                                (fun item
                                    -> 
                                    let item = extractSubstring item      //"https://kodis-files.s3.eu-central-1.amazonaws.com/timetables/2_2023_03_13_2023_12_09.pdf                 
                           
                                    pyramidOfDamnation
                                        {
                                            let!_ = not (item.Split("https://", StringSplitOptions.None).Length > 2), String.Empty //abych nezahlcoval log file chybovyma hlaskama, jinak je to chycene #74764-...
                                            let!_ = extractSubstring >> isValidHttps <| item, String.Empty
                                            let!_ = item.Contains @"timetables/", item

                                            return item.Replace("timetables/", String.Empty).Replace(".pdf", "_t.pdf")
                                        }                                                  
                                )  
                            //|> List.sort //jen quli testovani
                            |> List.filter
                                (fun item
                                    -> 
                                    let cond1 = (item |> Option.ofNullEmptySpace).IsSome
                                    let cond2 = item |> Option.ofNullEmpty |> Option.toBool //for learning purposes - compare with (not String.IsNullOrEmpty(item))
                                    cond1 && cond2 
                                )         
                            |> List.Parallel.map_CPU (fun item -> splitKodisLink item) 
                        )          
                           
                //**********************Cesty pro soubory pro aktualni a dlouhodobe platne a pro ostatni********************************************************
                let createPathsForDownloadedFiles filteredList : (string * string) list = 
          
                    filteredList
                    |> List.Parallel.map_CPU 
                        //(fun item -> fst item |> function CompleteLink value -> value, snd item |> function FileToBeSaved value -> value)
                        (fun (CompleteLink linkVal, FileToBeSaved fileVal) -> linkVal, fileVal)
                    |> List.Parallel.map_CPU
                        (fun (link, file) 
                            -> 
                            let path =                                         
                                let (|IntType|StringType|OtherType|) (param : 'a) = //zatim nevyuzito, mozna -> TODO podumat nad refactoringem nize uvedeneho 
                                    match param.GetType() with
                                    | typ when typ = typeof<int>    -> IntType   
                                    | typ when typ = typeof<string> -> StringType  
                                    | _                             -> OtherType                                                      
                                                
                                //let pathToDir = sprintf "%s\\%s" pathToDir file //pro ostatni
                                let pathToDir = sprintf "%s/%s" pathToDir file //pro ostatni

                                match pathToDir.Contains currentValidity || pathToDir.Contains withoutReplacementService with 
                                | true  ->   
                                        true //pro aktualni a dlouhodobe platne
                                        |> function
                                            | true when file.Substring(0, 1) = "0"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 0 sortedLines)
                                            | true when file.Substring(0, 1) = "1"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 0 sortedLines)
                                            | true when file.Substring(0, 1) = "2"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 1 sortedLines)
                                            | true when file.Substring(0, 1) = "3"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 2 sortedLines)
                                            | true when file.Substring(0, 1) = "4"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 3 sortedLines)
                                            | true when file.Substring(0, 1) = "5"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 4 sortedLines)
                                            | true when file.Substring(0, 1) = "6"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 5 sortedLines)
                                            | true when file.Substring(0, 1) = "7"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 6 sortedLines)
                                            | true when file.Substring(0, 1) = "8"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 7 sortedLines)
                                            | true when file.Substring(0, 1) = "9"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 8 sortedLines)
                                            | true when file.Substring(0, 1) = "S"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 9 sortedLines)
                                            | true when file.Substring(0, 1) = "R"  -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 10 sortedLines)
                                            | true when file.Substring(0, 2) = "_S" -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 9 sortedLines)
                                            | true when file.Substring(0, 2) = "_R" -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 10 sortedLines)
                                            | _                                     -> pathToDir.Replace("_vyluk", sprintf "%s/%s/" <| "_vyluk" <| List.item 11 sortedLines)  
                                
                                | false -> 
                                        pathToDir 
                            
                            link, path 
                        ) 
        
                dataToBeFiltered
                |> Result.map
                    (fun data
                        ->
                        match param with
                        | CurrentValidity           -> Records.SortRecordData.sortLinksOut data CurrentValidity
                        | FutureValidity            -> Records.SortRecordData.sortLinksOut data FutureValidity
                        | WithoutReplacementService -> Records.SortRecordData.sortLinksOut data WithoutReplacementService
                
                        |> createPathsForDownloadedFiles
                    )  
        )