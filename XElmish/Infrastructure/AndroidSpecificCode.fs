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

// Pro Google Play, zatim jen pro testovaci ucely 
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

    let private writeFileToMediaStore (resolver : ContentResolver) (rootName : string) (baseDir : string) (filePath : string) : bool =

        try
            let fileName = Path.GetFileName filePath

            let relativeDir =
                Path.GetDirectoryName filePath
                |> fun dir
                    ->
                    match dir |> Option.ofNullEmptySpace with
                    | None     -> String.Empty
                    | Some dir -> Path.GetRelativePath(baseDir, dir).Replace("\\", "/")

            let values = new ContentValues()

            values.Put(MediaStore.IMediaColumns.DisplayName, fileName)
            values.Put(MediaStore.IMediaColumns.MimeType, "application/pdf")

            values.Put(
                MediaStore.IMediaColumns.RelativePath,
                sprintf "Download/%s/%s" rootName relativeDir  
            )

            let collection = MediaStore.Downloads.GetContentUri(MediaStore.VolumeExternalPrimary)

            option
                {
                    let! uri = 
                        resolver.Insert(collection, values) 
                        |> Option.ofNull'
                    let! stream =
                        resolver.OpenOutputStream uri 
                        |> Option.ofNull'

                    use stream = stream
                    use input = File.OpenRead filePath
                    input.CopyTo stream
                    stream.Flush()

                    return true
                }
            |> Option.defaultValue false

        with
        | _ -> false
     
    let internal exportPdf (rootName : string) (sourceDir : string) : IO<Async<unit option>> =

        IO (fun ()
                ->
                asyncOption 
                    {
                        try
                            let baseDir = Directory.GetParent(sourceDir).FullName    
                            let! (ctx : Context) = Application.Context |> Option.ofNull' 
                            let! resolver = ctx.ContentResolver |> Option.ofNull'
                            let! files = getFiles sourceDir
                            
                            let failures =
                                files
                                |> Array.filter 
                                    (fun file 
                                        ->
                                        not (writeFileToMediaStore resolver rootName baseDir file)
                                    )
                            return! Array.isEmpty failures |> Option.ofBool     
                        with
                        | _ -> return! None                                            
                    }
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