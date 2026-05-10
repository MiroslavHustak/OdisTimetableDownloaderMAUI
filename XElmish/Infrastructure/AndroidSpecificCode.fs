namespace OdisTimetableDownloaderMAUI

#if ANDROID

open System
open System.IO
open System.IO.Compression

open System.Threading
open System.Threading.Tasks

open CommunityToolkit.Maui.Storage
open Microsoft.Maui.ApplicationModel

open FsToolkit.ErrorHandling

//***********************************************

open Android.OS
open Android.App
open Android.Net
open Android.Views
open Android.Content
open Android.Provider 
open Android.Content.PM 

open Xamarin.Essentials

//***********************************************

open Api.Logging
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open Types.Haskell_IO_Monad_Simulation

//************************************************
// ANDROID-SPECIFIC CODE
//*************************************************

[<Service(ForegroundServiceType = ForegroundService.TypeDataSync)>]
type private DownloadForegroundService() =

    inherit Service()

    let [<Literal>] notificationChannelId = "download_channel"
    let [<Literal>] notificationId = 1

    override this.OnBind intent = null  // not a bound service // intentionally null - binding not supported

    override this.OnStartCommand(intent : Intent, flags : StartCommandFlags, startId : int) =

        let iconId : int = Resource.Drawable.ic_download

        let minimalNotification =
            (new Notification.Builder(this, notificationChannelId))
                .SetContentTitle("Stahují se jízdní řády")
                .SetContentText("Probíhá stahování ...")
                .SetSmallIcon(iconId)
                .SetOngoing(true)
                .Build()

        try
            this.StartForeground(notificationId, minimalNotification)
            StartCommandResult.Sticky
        with
        | ex 
            ->
            runIO <| postToLog2 (string ex.Message) " #1002Android"
            StartCommandResult.NotSticky

    override this.OnTaskRemoved(rootIntent : Intent) =
        this.StopForeground StopForegroundFlags.Remove
        this.StopSelf()
        base.OnTaskRemoved rootIntent

    override this.OnDestroy() =
        this.StopForeground StopForegroundFlags.Remove
        base.OnDestroy()

module DownloadServiceController =
    
    let internal startService (context : Context) =
   
        IO (fun ()
                ->
                match context |> Option.ofNull' with
                | None 
                    ->
                    runIO <| postToLog2 "Context is null" " #1004Android"
                | Some ctx 
                    ->   
                    match int Build.VERSION.SdkInt with
                    | v 
                        when v >= 29
                        ->  // Android 10+ (incl.) 
                        let intent = new Intent(ctx, typeof<DownloadForegroundService>)
                        ctx.StartForegroundService intent |> ignore<ComponentName>                       
                    | _ -> 
                        ()  // Android 10- (excl.)
        )
   
    let internal stopService (context : Context) = 
   
        IO (fun ()
                ->                
                match context |> Option.ofNull' with
                | None
                    ->
                    runIO <| postToLog2 "Context is null" " #1005Android"

                | Some ctx
                    ->
                    match int Build.VERSION.SdkInt with
                    | v 
                        when v >= 29 
                        ->  // Android 10+ (incl.) 
                        let intent = new Intent(ctx, typeof<DownloadForegroundService>)
                        ctx.StopService intent |> ignore<bool>
                    | _ -> 
                        () // Android 10- (excl.)
        )

// Pro Google Play, v teto verzi zatim nepouzivano
module PdfExport =

    let private getFiles sourceDir =
    
        try
            match not (Directory.Exists sourceDir) with
            | true 
                ->
                None
            | false
                ->
                let files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
    
                match files with
                | null | [||] -> None
                | arr         -> Some arr
        with
        | _ -> None

    let private deleteExistingInDownloads (resolver : ContentResolver) (mediaRelativePath : string) (fileName : string) : unit =

        let collection = 
            MediaStore.Downloads.GetContentUri(MediaStore.VolumeExternalPrimary)
        let selection =
            sprintf "%s = ? AND %s = ?" 
                <| MediaStore.IMediaColumns.DisplayName
                <| MediaStore.IMediaColumns.RelativePath
        let selectionArgs = [| fileName; mediaRelativePath |]
        resolver.Delete(collection, selection, selectionArgs) |> ignore

    let private writeFileToMediaStore (resolver : ContentResolver) (baseDir : string) (filePath : string) : Async<unit option> =
        
        try
            let fileName = Path.GetFileName filePath

            let relativePath =
                Path.GetDirectoryName filePath
                |> Option.ofNullEmptySpace
                |> Option.map 
                    (fun dir 
                        ->
                        Path.GetRelativePath(baseDir, dir).Replace("\\", "/")
                    )
                |> Option.filter (fun p -> p <> ".")
                |> Option.defaultValue String.Empty

            let mediaRelativePath =
                match relativePath with
                | s 
                    when s = String.Empty
                    -> @"Download/"
                | p -> sprintf "Download/%s/" p

            deleteExistingInDownloads resolver mediaRelativePath fileName

            let values = new ContentValues()
            
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName)
            values.Put(MediaStore.IMediaColumns.MimeType, "application/pdf")
            values.Put(MediaStore.IMediaColumns.RelativePath, mediaRelativePath)
            
            let collection =
                MediaStore.Downloads.GetContentUri(MediaStore.VolumeExternalPrimary)

            asyncOption 
                {
                    let! uri = resolver.Insert(collection, values) |> Option.ofNull'
                    let! stream = resolver.OpenOutputStream uri |> Option.ofNull'
                    use stream = stream
                    use input = File.OpenRead filePath
                    do! input.CopyToAsync stream |> Async.AwaitTask
                    do! stream.FlushAsync()      |> Async.AwaitTask
                    return ()
                }
          
        with
        | _ -> async { return None }

    let internal exportPdf (token : CancellationToken) (baseDir : string) (sourceDir : string) reportProgress: IO<Async<unit option>> =

        IO (fun ()
                ->
                let checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()
                       
                asyncOption 
                    {
                        try   
                            let! (ctx : Context) = Application.Context |> Option.ofNull'
                            let! resolver = ctx.ContentResolver |> Option.ofNull'
                            let! files = getFiles sourceDir
                            //let counterAndProgressBar = counterAndProgressBar files.Length token checkCancel reportProgress
                               
                            let! results =
                                files
                                |> Array.map
                                    (fun file
                                        ->
                                        async
                                            {
                                                let! result = writeFileToMediaStore resolver baseDir file
                                                //counterAndProgressBar.Post <| Inc 1
                                                return result
                                            }
                                    )
                                |> Async.Sequential //runs each file write one after another but yields the thread between files — safe for MediaStore, no blocking
                                // Async.Parallel is intentionally avoided because of certain risks related to how MediaStore works, and also MediaStore itself is designed to support concurrent access 
                           
                            let failures = results |> Array.filter (fun r -> r.IsNone)

                            //counterAndProgressBar.PostAndReply(fun reply -> StopAndReply reply) 
                               
                            return! Array.isEmpty failures |> Option.ofBool
                        with
                        | _ -> return! None                                            
                    }
           )

// Zatim nepouzivano
module PdfExportZip =
 
    let private getFiles sourceDir =
        
        try
            match not (Directory.Exists sourceDir) with
            | true 
                ->
                None
            | false
                ->
                let files = Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories)
        
                match files with
                | null | [||] -> None
                | arr         -> Some arr
        with
        | _ -> None

    let private createMediaStoreUri (context : Context) (fileName : string) =

        try
            let resolver = context.ContentResolver
            let values = new ContentValues()

            values.Put(MediaStore.IMediaColumns.DisplayName, fileName)
            values.Put(MediaStore.IMediaColumns.MimeType, "application/zip")
            values.Put(MediaStore.IMediaColumns.RelativePath, "Download/JR_ODIS")

            let collection =
                MediaStore.Downloads.GetContentUri(MediaStore.VolumeExternalPrimary)

            match resolver.Insert(collection, values) with
            | null -> None
            | uri  -> Some (resolver, uri)

        with
        | _ -> None

    let internal exportPdfZip (sourceDir: string) : IO<unit> =

        IO (fun () 
                ->
                option
                    {
                        let! (ctx : Context) = Application.Context |> Option.ofNull'
                        let! files = getFiles sourceDir
                        let! (resolver : ContentResolver, uri : Android.Net.Uri) = createMediaStoreUri ctx "JR_ODIS_export.zip"
                        let! (stream : Stream) = resolver.OpenOutputStream uri |> Option.ofNull'
                        
                        use stream = stream
                        use (zip : ZipArchive) = new ZipArchive(stream, ZipArchiveMode.Create, false)
                        
                        files
                        |> Array.iter
                            (fun (file : string)
                                ->
                                let relative =
                                    Path.GetRelativePath(sourceDir, file)
                                    |> fun p -> p.Replace("\\", "/")

                                option
                                    {
                                        let! (entry : ZipArchiveEntry) = 
                                            zip.CreateEntry(relative, CompressionLevel.Optimal) |> Option.ofNull'
                                        use (entryStream : Stream) = entry.Open()
                                        use (fileStream  : FileStream) = File.OpenRead file
                                        fileStream.CopyTo entryStream
                                        return ()
                                    }
                                |> ignore<unit option>
                            )
                        return ()
                    }
                |> ignore<unit option>
        )       
       
module WakeLockHelper = //pouze pro Android API 33 a Android API 34

    let internal acquireWakeLock (lock : PowerManager.WakeLock) = 

        IO (fun () 
                ->
                match lock with
                | lock 
                    when lock.IsHeld // WakeLock is already held, no need to acquire it again
                    -> ()
                | _ -> lock.Acquire() // Acquire the WakeLock if not already held
        )     
       
    let internal releaseWakeLock (lock : PowerManager.WakeLock) =  

        IO (fun () 
                ->
                match lock with
                | lock
                    when lock.IsHeld 
                    -> lock.Release()
                | _ -> ()
        )

module KeepScreenOnManager =

    let internal keepScreenOn enable =

        IO (fun () 
                ->
                pyramidOfDoom
                    {
                        let! activity = Platform.CurrentActivity |> Option.ofNull', ()
                        let!_ = enable |> Option.ofBool, activity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn) 

                        return activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn)        
                    }    
        )

module AndroidUIHelpers =

    let internal permissionCheck () =

        IO (fun () 
                ->
                async 
                    {
                        try
                            // Check if running on Android 11+ and use Environment.IsExternalStorageManager
                            match Build.VERSION.SdkInt >= BuildVersionCodes.R with
                            | true 
                                -> 
                                return Environment.IsExternalStorageManager
                            | false
                                ->
                                let! status = Permissions.CheckStatusAsync<Permissions.StorageRead>() |> Async.AwaitTask
                                return status = PermissionStatus.Granted
                                    
                        with
                        | ex 
                            -> 
                            runIO <| postToLog2 (string ex.Message) "#0001Android"
                            return false  
                    }
        )

    //Not used yet - asi nebude fungovat u Android 11+
    let internal bringAppToForeground () =

        IO (fun () 
                ->
                try
                    pyramidOfDoom 
                        {
                            use! context = Application.Context |> Option.ofNull', None
                            use! packageManager = context.PackageManager |> Option.ofNull', None            
                            use! intent = packageManager.GetLaunchIntentForPackage(context.PackageName) |> Option.ofNull', None
            
                            do! 
                                intent.AddFlags
                                    (
                                        ActivityFlags.NewTask |||
                                        ActivityFlags.ClearTop |||
                                        ActivityFlags.ClearTask |||
                                        ActivityFlags.BroughtToFront |||
                                        ActivityFlags.SingleTop
                                    )
                                |> Option.ofNull'
                                |> Option.map (fun _ -> ()), None
            
                            do context.StartActivity intent 
                            return Some () // Return unit option
                        }
                with
                | ex
                    -> 
                    runIO <| postToLog2 (string ex.Message) "#0002Android"
                    None  
        )
    
    //Not used yet
    let internal sendAppToBackground () =

        IO (fun () 
                ->
                try
                    pyramidOfDoom
                        {
                            use! context = Application.Context |> Option.ofNull', None                
                            use homeIntent : Intent = new Intent(Intent.ActionMain)
                            do! 
                                homeIntent.AddCategory(Intent.CategoryHome)
                                          .SetFlags(ActivityFlags.NewTask)
                                          |> Option.ofNull' 
                                          |> Option.map (fun _ -> ()), None

                            return! Some <| context.StartActivity homeIntent 
                        }
                with
                | ex 
                    ->
                    runIO <| postToLog2 (string ex.Message) "#0003Android"
                    None    
        )
    
    //Tato funkce je pouzivana misto Xamarin.Essentials.AppInfo.ShowSettingsUI() quli moznosti Android 11+ pro "Manage all files" 
    let internal openAppSettings () =

        IO (fun ()
                ->
                try
                    Thread.Sleep 500 
                                       
                    pyramidOfDoom
                        {
                            let! intentAction =
                                match Build.VERSION.SdkInt >= BuildVersionCodes.R with
                                | true  -> Settings.ActionManageAppAllFilesAccessPermission // Android 11+ for "Manage all files"
                                | false -> Settings.ActionApplicationDetailsSettings // Fallback for older versions
                                
                                |> Option.ofNullEmpty, None

                            //use! intent = new Intent(Settings.ActionApplicationDetailsSettings) |> Option.ofNull', None
                            use! intent = new Intent(intentAction : string) |> Option.ofNull', None
                            do!
                                intent.AddFlags
                                    (
                                        ActivityFlags.NewTask |||
                                        ActivityFlags.ClearTop |||
                                        ActivityFlags.ClearTask |||
                                        ActivityFlags.BroughtToFront |||
                                        ActivityFlags.SingleTop
                                    )
                                |> Option.ofNull'
                                |> Option.map (fun _ -> ()), None
    
                            use! uri = Uri.FromParts("package", Application.Context.PackageName, null) |> Option.ofNull', None
                            do!
                                intent.SetData uri
                                |> Option.ofNull'
                                |> Option.map (fun _ -> ()), None
    
                            return Some <| Application.Context.StartActivity intent
                        }
                        |> Option.defaultValue () //TODO logfile + vymysli tady neco, co zrobit v teto situaci
                with
                | ex -> runIO <| postToLog2 (string ex.Message) "#0004Android"                
        )
#endif