namespace Platform

open System
open System.IO

open FsToolkit.ErrorHandling

#if ANDROID
open Android.Content
#endif

module Paths =

    let private ensureDir path =
        try
            Directory.CreateDirectory path |> ignore<DirectoryInfo>
            path
        with
        | _ -> String.Empty  //to pak vyhodi exception jinde

#if ANDROID

    let private basePath (context : Context) =
        match context.GetExternalFilesDir null with
        | null -> String.Empty
        | dir  -> dir.AbsolutePath
         
    let private publicBase =
        match Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads) with
        | null -> String.Empty
        | dir  -> dir.AbsolutePath
    
    let internal downloads  (context : Context) = Path.Combine(publicBase, "FabulousTimetables")     |> ensureDir
    let internal downloads4 (context : Context) = Path.Combine(publicBase, "FabulousTimetables4")    |> ensureDir
    let internal old        (context : Context) = Path.Combine(publicBase, "FabulousTimetablesOld")  |> ensureDir
    let internal old4       (context : Context) = Path.Combine(publicBase, "FabulousTimetablesOld4") |> ensureDir
    let internal logs       (context : Context) = Path.Combine(publicBase, "Logs")                   |> ensureDir

    let internal jsonTemp   (context : Context) = Path.Combine(basePath context, "JsonData")         |> ensureDir

#else
    let private basePath = @"g:\Users\User\"

    let internal downloads  () = Path.Combine(basePath, "Data")       |> ensureDir
    let internal downloads4 () = Path.Combine(basePath, "Data4")      |> ensureDir
    let internal old        () = Path.Combine(basePath, "DataOld")    |> ensureDir
    let internal old4       () = Path.Combine(basePath, "DataOld4")   |> ensureDir
    let internal logs       () = Path.Combine(basePath, "Logs")       |> ensureDir
    let internal jsonTemp   () = Path.Combine(basePath, "KODISJson2") |> ensureDir
#endif

//Path.Combine je zajisteno, ze nevyhodi exception (path nebude nikdy null, .NET > 2.1)