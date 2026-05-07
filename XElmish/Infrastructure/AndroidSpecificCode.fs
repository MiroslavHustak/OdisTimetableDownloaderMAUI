namespace OdisTimetableDownloaderMAUI

#if ANDROID

open System.IO
open System.Threading

open FsToolkit.ErrorHandling

//***********************************************

open Api.Logging

open Helpers
open Helpers.Builders

open Types.Haskell_IO_Monad_Simulation

open Android.OS
open Android.App
open Android.Net
open Android.Views
open Android.Content
open Android.Provider 
open Android.Content.PM 

open Xamarin.Essentials

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
    
        let startForegroundSucceeded =
            try
                // StartForeground is now called immediately before anything else, so Android 12+ can never hit the 5-second timeout.
                this.StartForeground(notificationId, minimalNotification)
                true
            with
            | ex 
                ->
                runIO <| postToLog2 (string ex.Message) " #1002Android"
                false
    
        match startForegroundSucceeded with
        | false
            ->
            StartCommandResult.NotSticky
    
        | true
            ->
            match this.GetSystemService Context.NotificationService |> Option.ofNull' with
            | None
                ->
                runIO <| postToLog2 "Could not get NotificationManager" " #1001Android"
            | Some gss 
                ->
                let manager = gss :?> NotificationManager

                match Build.VERSION.SdkInt >= BuildVersionCodes.O with
                | true 
                    ->
                    let channel =
                        new NotificationChannel(
                            notificationChannelId,
                            "Timetable Downloads",
                            NotificationImportance.Low
                        )
                    manager.CreateNotificationChannel channel
                | false 
                    ->
                    ()
    
            StartCommandResult.Sticky

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
                    let intent = new Intent(ctx, typeof<DownloadForegroundService>)

                    match Build.VERSION.SdkInt >= BuildVersionCodes.O with
                    | true  -> ctx.StartForegroundService intent |> ignore<ComponentName>
                    | false -> ctx.StartService intent           |> ignore<ComponentName>
        )
    let internal stopService (context : Android.Content.Context) =
    
        IO (fun ()
                ->                
                match context |> Option.ofNull' with
                | None ->
                    runIO <| postToLog2 "Context is null" " #1005Android"
                | Some ctx ->
                    let intent = new Android.Content.Intent(ctx, typeof<DownloadForegroundService>)
                    ctx.StopService intent |> ignore<bool>
        )

module openFolder = 
    
    let internal openFolderInFileManager (context: Context) (folderPath: string) =
    
        IO (fun ()
                ->
                try
                    let authority = context.PackageName + ".fileprovider"
                
                    // Find any PDF in the folder to use as the "entry point"
                    let anyPdf = 
                        Directory.EnumerateFiles(folderPath, "*.pdf", SearchOption.AllDirectories)
                        |> Seq.tryHead
    
                    match anyPdf with
                    | None ->
                        // Folder is empty - nothing to show yet
                        runIO <| postToLog2 "No PDF found to open" "#FileManager002"
                    | Some pdfPath ->
                        let file = new Java.IO.File(pdfPath)
                        let uri =
                            AndroidX.Core.Content.FileProvider.GetUriForFile(context, authority, file)
    
                        use intent = new Intent(Intent.ActionView)
                        intent.SetDataAndType(uri, "application/pdf") |> ignore<Intent>
                        intent.AddFlags(ActivityFlags.NewTask ||| ActivityFlags.GrantReadUriPermission) |> ignore<Intent>
    
                        let chooser = Intent.CreateChooser(intent, "Otevřít v správci souborů")
                        chooser.AddFlags(ActivityFlags.NewTask) |> ignore<Intent>
                        context.StartActivity(chooser)
                with
                | ex ->
                    runIO <| postToLog2 (string ex.Message) "#FileManager001"
                    ()
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

    // Quli Google Play zatim permissionCheck/MANAGE_EXTERNAL_STORAGE nepouzivan, nicmene ponechat pro pripadne budouci pouziti
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