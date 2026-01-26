namespace Helpers

open System.IO
//***************************
open FsToolkit.ErrorHandling
//***************************

open Types
open Types.Types

open FreeMonad
open Haskell_IO_Monad_Simulation

open Helpers
open Helpers.CopyDir
open Helpers.MoveDir
open Helpers.CommandLineWorkflow

module FreeMonadInterpret = 
 
    let private cmdBuilder = CommandLineProgramBuilder

    [<Struct>]
    type internal IO_Operation = 
        | Copy
        | Move    

    [<TailCall>]   
    let rec private interpret config io_operation clp =

        let fmFunction (source : Result<string, string>) (destination : Result<string, string>) : Result<unit, string> =

            match source, destination with
            | Ok s, Ok d 
                ->
                try                    
                    (*
                    #if WINDOWS
                    match io_operation with
                    | Copy //C++
                        ->                        
                        match Native.CopyDirContent64(s, d, 0, 0) with  //exn se musi chytat uz v C++
                        | 0 -> Ok ()
                        | _ -> Error <| sprintf "Chyba při kopírování adresáře %s do %s #300" s d
                       
                    | Move //C++
                        ->
                        match Native.MoveDirContent64(s, d, 0) with  //exn se musi chytat uz v C++
                        | 0 -> Ok ()
                        | _ -> Error <| sprintf "Chyba při přemístění adresáře %s do %s #310" s d 
                 
                    //| Copy -> runIO <| copyDirectory2 s d     //Rust       
                    //| Move -> runIO <| moveDirectory2 s d     //Rust
                    #endif
                    *)

                    match io_operation with
                    | Copy -> runIO <| copyDirectory s d 0 true //F#                      
                    | Move -> runIO <| moveDirectory s d        //F#                     
                   
                with
                | ex                    
                    ->                  
                    Error <| string ex.Message               

            | Error e, _ | _, Error e
                ->                
                Error e            

        match clp with 
        | Pure x       
            ->
            x

        | Free (SourceFilepath next) 
            ->
            let sourceFilepath (source : string) : Result<string,string> =       
            
                try
                    result
                        {
                            let dInfodat : DirectoryInfo = DirectoryInfo source   
                           //Added existence check for destination directory despite of TOCTOU risk (Android 7.1)
                            let! _ = Result.fromBool () (sprintf "Zdrojový adresář %s neexistuje #302-2" source) dInfodat.Exists    

                            return! SafeFullPath.safeFullPathResult >> runIO <| source
                        }   
                with
                | ex -> Error <| string ex.Message

            interpret config io_operation (next (sourceFilepath config.source))

        | Free (DestinFilepath next) 
            ->
            let destinFilepath destination =    
            
                try
                    result
                        {
                            let dInfodat : DirectoryInfo = DirectoryInfo destination   
                            //Added existence check for destination directory despite of TOCTOU risk (Android 7.1)
                            let! _ = Result.fromBool () (sprintf "Zdrojový adresář %s neexistuje #302" destination) dInfodat.Exists    
                        
                            return! SafeFullPath.safeFullPathResult >> runIO <| destination                          
                        }  
                with
                | ex -> Error <| string ex.Message
                
            interpret config io_operation (next (destinFilepath config.destination))

        | Free (CopyOrMove (s, d)) 
            ->          
            fmFunction s d            

    let internal copyOrMoveFiles config io_operation =  
        
        FreeMonad
            (fun () 
                -> 
                try
                    cmdBuilder
                        {
                            let! sourceFilepath = Free (SourceFilepath Pure)                
                            let! destinFilepath = Free (DestinFilepath Pure)

                            return! Free (CopyOrMove (sourceFilepath, destinFilepath))
                        }
                    |> interpret config io_operation  
                with
                | ex -> Error <| string ex.Message
            )