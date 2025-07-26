namespace Helpers

open System
open System.IO
open System.Text
open System.Diagnostics
open System.Runtime.InteropServices

open Fabulous.Maui
open Microsoft.Maui.ApplicationModel 

//***************************
open Types
open Types.Types
open Types.ErrorTypes

#if WINDOWS
open NativeHelpers
open NativeHelpers.Native
#endif

open FSharpHelpers
open FSharpHelpers.MoveDir
open FSharpHelpers.CopyDir

open FreeMonad
open FsToolkit.ErrorHandling
open Haskell_IO_Monad_Simulation

open Helpers.Builders  
open Helpers.CommandLineWorkflow

//***************************

module CopyOrMoveDirectories = 
 
    let private cmdBuilder = CommandLineProgramBuilder

    [<Struct>]
    type internal IO_Operation = 
        | Copy
        | Move    

    [<TailCall>]   
    let rec private interpret config io_operation clp =

        let f (source : Result<string, string>) (destination : Result<string, string>) : Result<unit, string> =

            match source, destination with
            | Ok s, Ok d 
                ->
                try
                    #if WINDOWS
                    match io_operation with
                    | Copy
                        ->                        
                        match Native.CopyDirContent64(s, d, 0, 0) with  //exn se musi chytat uz v C++
                        | 0 -> Ok ()
                        | _ -> Error <| sprintf "Chyba při kopírování adresáře %s do %s #300" s d
                       
                    | Move 
                        ->
                        match Native.MoveDirContent64(s, d, 0) with  //exn se musi chytat uz v C++
                        | 0 -> Ok ()
                        | _ -> Error <| sprintf "Chyba při přemístění adresáře %s do %s #310" s d 

                    #else
                    match io_operation with
                    | Copy -> CopyDir.runIO <| copyDirectory s d 0 0                       
                    | Move -> MoveDir.runIO <| moveDirectory s d 0                        
                    #endif
                with
                | ex -> Error <| string ex.Message

            | Error e, _ | _, Error e
                ->
                Error e            

        match clp with 
        | Pure x       
            ->
            x

        | Free (SourceFilepath next) 
            ->
            let sourceFilepath source =      
            
                try
                    pyramidOfDoom
                        {
                            let! value = Path.GetFullPath source |> Option.ofNullEmpty, Error <| sprintf "Chyba při čtení cesty k %s #301" source   
                            let! value = 
                                (
                                    let dInfodat : DirectoryInfo = DirectoryInfo value   
                                    Option.fromBool value dInfodat.Exists
                                ), Error <| sprintf "Zdrojový adresář %s neexistuje #302" value
                            return Ok value
                        }
                with
                | ex -> Error <| sprintf "Chyba při čtení cesty k %s. %s #303" source (string ex.Message)

            interpret config io_operation (next (sourceFilepath config.source))

        | Free (DestinFilepath next) 
            ->
            let destinFilepath destination =  
                try
                    pyramidOfDoom
                        {
                            let! value = Path.GetFullPath destination |> Option.ofNullEmpty, Error <| sprintf "Chyba při čtení cesty k %s #304" destination                        
                            let! value = 
                                (
                                    let dInfodat: DirectoryInfo = DirectoryInfo value   
                                    Option.fromBool value dInfodat.Exists
                                ), Error <| sprintf "Chyba při čtení cesty k %s #305" value
                        
                            return Ok value
                        }
                with
                | ex -> Error <| sprintf "Chyba při čtení cesty k %s. %s #306" destination (string ex.Message)

            interpret config io_operation (next (destinFilepath config.destination))

        | Free (CopyOrMove (s, d)) 
            ->          
            f s d            

    let internal copyOrMoveFiles config io_operation =  
        
        FreeMonad
            (fun () 
                -> 
                cmdBuilder
                    {
                        let! sourceFilepath = Free (SourceFilepath Pure)                
                        let! destinFilepath = Free (DestinFilepath Pure)

                        return! Free (CopyOrMove (sourceFilepath, destinFilepath))
                    }
                |> interpret config io_operation  
            )

module StringCombine = //testovaci modul pro Rust dll

    let internal sprintfNative str1 str2 : string option =

        let toUtf16Ptr (text : string) : IntPtr =
            text
            |> Option.ofPtrOrNull
            |> Option.map 
                (fun txt 
                    ->
                    let bytes = Encoding.Unicode.GetBytes(txt + "\u0000")
                    let ptr = Marshal.AllocHGlobal bytes.Length
                    Marshal.Copy(bytes, 0, ptr, bytes.Length)
                    ptr
                )
            |> Option.defaultValue IntPtr.Zero
    
        let fromUtf8Ptr (ptr : IntPtr) : string option =
            ptr
            |> Option.ofPtrOrNull
            |> Option.map Marshal.PtrToStringAnsi
    
        let s1 = toUtf16Ptr str1
        let s2 = toUtf16Ptr str2
    
        try
            NativeHelpers.Native.combine_strings(s1, s2)
            |> Option.ofPtrOrNull
            |> Option.bind 
                (fun combinedPtr
                    ->
                    try
                        fromUtf8Ptr combinedPtr
                    finally
                        NativeHelpers.Native.free_string combinedPtr
                )
        finally
            match s1 |> Option.ofPtrOrNull with
            | Some _ -> Marshal.FreeHGlobal s1
            | None   -> ()

            match s2 |> Option.ofPtrOrNull with
            | Some _ -> Marshal.FreeHGlobal s2
            | None   -> ()            

module DirFileHelper = 
  
    let [<Literal>] internal jsonEmpty = """[ {} ]"""

    let internal writeAllText path content =

        IO (fun () 
                ->  
                pyramidOfDoom
                    {
                        let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError
                        let fInfoDat = FileInfo filepath
                        let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                        return Ok <| File.WriteAllText(filepath, content)
                    }
                |> Result.defaultValue ()
        )

    let internal writeAllTextAsync path content =

        IO (fun () 
                ->  
                pyramidOfDoom
                    {
                        let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError
                        let fInfoDat = FileInfo filepath
                        let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError
    
                        return Ok (File.WriteAllTextAsync(filepath, content) |> Async.AwaitTask)
                    }
                |> Result.defaultWith (fun _ -> async { return () }) 
        )   

    let internal readAllText path = 

        IO (fun () 
                ->  
                pyramidOfDoom
                    {
                        //path je sice casto pod kontrolou a filepath nebude null, nicmene pro jistotu...  
                        let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError                                                
                        let fInfoDat = FileInfo filepath
                        let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                        return Ok <| File.ReadAllText filepath                                           
                    }              
                |> Result.defaultValue jsonEmpty //TODO logfile, nestoji to za to vytahovat Result nahoru     
        )
                    
    let internal readAllTextAsync path = 

        IO (fun () 
                -> 
                pyramidOfDoom
                    {   
                        //path je sice casto pod kontrolou a filepath nebude null, nicmene pro jistotu...  
                        let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError
                        let fInfoDat = FileInfo filepath
                        let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                        return Ok (File.ReadAllTextAsync filepath |> Async.AwaitTask)                                          
                    }             
                |> Result.defaultWith (fun _ -> async { return jsonEmpty }) //TODO logfile, nestoji to za to vytahovat Result nahoru
        )

    let internal checkFileCondition pathToFile condition =

        IO (fun () 
                -> 
                option
                    {
                        let! filepath = pathToFile |> Path.GetFullPath |> Option.ofNullEmpty                     
                        let fInfodat : FileInfo = FileInfo filepath

                        return! condition fInfodat |> Option.ofBool  
                    }     
        )

    let internal checkDirectoryCondition pathToDir condition =
    
            IO (fun () 
                    -> 
                    option
                        {
                            let! dirpath = pathToDir |> Path.GetFullPath |> Option.ofNullEmpty                     
                            let dInfodat : DirectoryInfo = DirectoryInfo dirpath
    
                            return! condition dInfodat |> Option.ofBool  
                        }     
            )

module MyString = 
            
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings : int, stringToAdd : string) : string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty

module Xor = 

    //pro xor CE musi byt explicitne typ, type inference bere u yield typ unit, coz tady jaksi nejde, bo bool

    //jen priklad pouziti, v realnem pripade pouzij primo xor { a; b } nebo xor { a; b; c }    
    let internal xor2 (a : bool) (b : bool) = xor { a; b }
    let internal xor3 (a : bool) (b : bool) (c : bool) = xor { a; b; c }   