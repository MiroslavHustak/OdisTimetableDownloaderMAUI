namespace Helpers

open System
open System.IO
open System.Text
open System.Runtime.InteropServices

//***************************

open Types

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
            
module SafeFullPath =

    let internal safeFullPathResult path =

        IO (fun ()
                ->
                try
                    Path.GetFullPath path
                    |> Option.ofNullEmpty 
                    |> Option.toResult "Failed getting path"  
                with
                | ex -> Error <| sprintf "Path is invalid: %s" (string ex.Message)
        )

    let internal safeFullPathOption path =

        IO (fun ()
                ->
                try
                    Path.GetFullPath path
                    |> Option.ofNullEmpty 
                with
                | _ -> None
        )

module DirFileHelper = 
  
    let [<Literal>] internal jsonEmpty = """[ {} ]"""
 
    let internal writeAllTextAsync path content =

        IO (fun () 
                -> 
                try
                    result
                        {
                            let! filepath = SafeFullPath.safeFullPathResult >> runIO <| path                             
                            return File.WriteAllTextAsync(filepath, content) |> Async.AwaitTask
                        }   
                with
                | ex -> Error <| string ex.Message   

                |> Result.defaultWith (fun _ -> async { return () }) //Predelat pro konkretni pripad
        )   

    let internal readAllText path = 

        IO (fun () 
                ->  
                try
                    result
                        {
                            let! filepath = SafeFullPath.safeFullPathResult >> runIO <| path                         
                            return File.ReadAllText filepath    
                        }   
                with
                | ex -> Error <| string ex.Message   

                |> Result.defaultValue jsonEmpty //Tohle je pro parsing jsonu se specialni logikou, pro jine pripady upravit
        )
                    
    let internal readAllTextAsync path = 

        IO (fun () 
                -> 
                try
                    result
                        {
                            let! filepath = SafeFullPath.safeFullPathResult >> runIO <| path      
                            return File.ReadAllTextAsync filepath |> Async.AwaitTask  
                        }   
                with
                | ex -> Error <| string ex.Message   

                |> Result.defaultWith (fun _ -> async { return jsonEmpty }) //Tohle je pro parsing jsonu se specialni logikou, pro jine pripady upravit
        )

    //not used yet
    (*
        •	If checkFileCondition returns None (e.g., the file does not exist or the condition fails), then fileNames always returns Set.empty<string>.
        •	This means your results will be empty if the condition is not met, even if the directory exists and contains files.
    *)

    let internal checkFileCondition pathToFile condition =
    
        IO (fun () 
                -> 
                try
                    option
                        {
                            let! filepath = SafeFullPath.safeFullPathOption >> runIO <| pathToFile                  
                            let fInfodat : FileInfo = FileInfo filepath    
    
                            return! condition fInfodat |> Option.ofBool
                        }  
                with
                | _ -> None        
        )
    
    let internal checkDirectoryCondition pathToDir condition =
        
        IO (fun () 
                -> 
                try
                    option
                        {
                            let! dirpath = SafeFullPath.safeFullPathOption >> runIO <| pathToDir                 
                            let dInfodat : DirectoryInfo = DirectoryInfo dirpath
        
                            return! condition dInfodat |> Option.ofBool                               
                        }    
                with
                | _ -> None              
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

module Validation = 

    let internal isValidHttpsOption (s : string) =   //This code rejects IP-based URLs         
    
           try 
               match Uri.TryCreate(s, UriKind.Absolute) with
               | true, uri
                   -> 
                   match uri.Scheme = Uri.UriSchemeHttps && uri.Host.Contains(".") && not (uri.Host.Contains("://")) with
                   | true  -> Some s
                   | false -> None   
               | _ ->
                   None    
            with
            | _ -> None

    let internal isValidHttps (s : string) =   //This code rejects IP-based URLs         
    
        try 
            match Uri.TryCreate(s, UriKind.Absolute) with
            | true, uri
                -> uri.Scheme = Uri.UriSchemeHttps && uri.Host.Contains(".") && not (uri.Host.Contains("://"))
            | _ -> false    
        with
        | _ -> false