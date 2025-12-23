namespace IO_Operations

open System
open System.IO

open Types
open Types.Types
open Helpers.Builders
open Helpers.StringCombine

module CreatingPathsAndNames =    
         
    let internal createNewDirectoryPaths pathToDir : Reader<string list, string list> =
        
        reader
            { 
                let! getDefaultRecordValues = //Reader monad for educational purposes only, no real benefit here
                    fun env -> env in return getDefaultRecordValues |> List.map (fun item -> sprintf"%s/%s"pathToDir item) 
            } 

    let internal createDirName variant : Reader<string list, string> = 

        reader
            {
                let! getDefaultRecordValues = fun env -> env //Reader monad for educational purposes only, no real benefit here

                return 
                    match variant with 
                    | CurrentValidity  -> getDefaultRecordValues |> List.item 0
                    | FutureValidity   -> getDefaultRecordValues |> List.item 1
                    | LongTermValidity -> getDefaultRecordValues |> List.item 2
            } 
               
    (*
    let internal createOneNewDirectory pathToDir dirName = 
          
          [ sprintf"%s\%s"pathToDir dirName ] //lomitko !!!
    *)

    let internal createOneNewDirectoryPath pathToDir dirName = //zatim nepouzivano

        #if WINDOWS
        let str = 
            sprintfNative pathToDir dirName //testovani Rust dll
            |> Option.defaultValue String.Empty
        #else
        let str = sprintf"%s/%s" pathToDir dirName         
        #endif
        [ str ]  //list -> aby bylo mozno pouzit funkci createFolders bez uprav //lomitko !!!