namespace Helpers

open System
open System.IO
open System.Text
open System.Runtime.InteropServices

//***************************
open Types
open Types.ErrorTypes

#if WINDOWS
open NativeHelpers
open NativeHelpers.Native
#endif

open Helpers.Builders  
open FsToolkit.ErrorHandling
open Haskell_IO_Monad_Simulation

//***************************

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

    //not used yet
    (*
        •	If checkFileCondition returns None (e.g., the file does not exist or the condition fails), then fileNames always returns Set.empty<string>.
        •	This means your results will be empty if the condition is not met, even if the directory exists and contains files.
    *)
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