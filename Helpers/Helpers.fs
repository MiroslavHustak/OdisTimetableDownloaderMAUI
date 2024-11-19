namespace Helpers

open Microsoft.Maui.ApplicationModel

module FileInfoHelper = 

    open System
    open System.IO

    //*************************
    
    open Types.ErrorTypes
    open Helpers.Builders
    open Settings.Messages
    
    let private jsonEmpty = """[ {} ]"""

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
       
module MyString = 
        
    open System    
      
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings : int, stringToAdd : string): string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty
                  
