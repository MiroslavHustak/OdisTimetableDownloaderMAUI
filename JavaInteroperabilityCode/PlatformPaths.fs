namespace Platform

open System
open System.IO

#if ANDROID
open Android.Content
#endif

// ======================================================
// PATHS (platform-specific, SAME SHAPE)
// ======================================================
module Paths =

    let private ensureDir path =
        Directory.CreateDirectory path |> ignore<DirectoryInfo>
        path

#if ANDROID

    let private basePath (context: Context) =
        context.GetExternalFilesDir(null).AbsolutePath
         
    let private publicBase =
        Android.OS.Environment
            .GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDownloads)
            .AbsolutePath
    
    let internal downloads  (context: Context) = Path.Combine(publicBase, "FabulousTimetables")     |> ensureDir
    let internal downloads4 (context: Context) = Path.Combine(publicBase, "FabulousTimetables4")    |> ensureDir
    let internal old        (context: Context) = Path.Combine(publicBase, "FabulousTimetablesOld")  |> ensureDir
    let internal old4       (context: Context) = Path.Combine(publicBase, "FabulousTimetablesOld4") |> ensureDir
    let internal logs       (context: Context) = Path.Combine(publicBase, "Logs")                   |> ensureDir

    let internal jsonTemp   (context: Context) = Path.Combine(basePath context, "JsonData")         |> ensureDir

#else
    let private basePath = @"g:\Users\User\"

    let internal downloads  () = Path.Combine(basePath, "Data")       |> ensureDir
    let internal downloads4 () = Path.Combine(basePath, "Data4")      |> ensureDir
    let internal old        () = Path.Combine(basePath, "DataOld")    |> ensureDir
    let internal old4       () = Path.Combine(basePath, "DataOld4")   |> ensureDir
    let internal logs       () = Path.Combine(basePath, "Logs")       |> ensureDir
    let internal jsonTemp   () = Path.Combine(basePath, "KODISJson2") |> ensureDir
#endif