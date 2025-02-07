namespace Helpers

module FileInfoHelper = 

    open System
    open System.IO

    //*************************
    open Types
    open Types.ErrorTypes

    open Helpers.Builders
    
    open Settings.Messages    

    let [<Literal>] internal jsonEmpty = """[ {} ]"""

    let internal readAllText path = 

        pyramidOfDoom
            {
                //path je sice casto pod kontrolou a filepath nebude null, nicmene pro jistotu...  
                let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError
                                                
                let fInfoDat = FileInfo filepath
                let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                return Ok <| File.ReadAllText filepath                                           
            }  
            
        |> function
            | Ok value -> value                      
            | Error _  -> jsonEmpty //TODO logfile, nestoji to za to vytahovat Result nahoru                                 
                    
    let internal readAllTextAsync path = 

        pyramidOfDoom
            {   
                //path je sice casto pod kontrolou a filepath nebude null, nicmene pro jistotu...  
                let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError

                let fInfoDat = FileInfo filepath
                let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                return Ok (File.ReadAllTextAsync filepath |> Async.AwaitTask)                                          
            }  
            
        |> function
            | Ok value -> value                      
            | Error _  -> async { return jsonEmpty } //TODO logfile, nestoji to za to vytahovat Result nahoru

    let internal checkFileCondition pathToFile condition =
        
        pyramidOfDoom
            {
                let filepath = pathToFile |> Path.GetFullPath |> Option.ofNullEmpty 
                let! filepath = filepath, None
                    
                let fInfodat : FileInfo = FileInfo filepath
                let! _ = condition fInfodat |> Option.ofBool, None  
                                                 
                return Some ()
            }              
       
module MyString = 
        
    open System    
      
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings : int, stringToAdd : string): string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty

module Xor = 

    open Helpers.Builders

    //pro xor CE musi byt explicitne typ, type inference bere u yield typ unit, coz tady jaksi nejde, bo bool

    //jen priklad pouziti, v realnem pripade pouzij primo xor { a; b } nebo xor { a; b; c }    
    let internal xor2 (a : bool) (b : bool) = xor { a; b }
    let internal xor3 (a : bool) (b : bool) (c : bool) = xor { a; b; c }    