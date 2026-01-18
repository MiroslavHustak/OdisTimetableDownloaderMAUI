namespace OdisTimetableDownloaderMAUI

// nechej reference, jak jsou, intellisense jen halucinuje
open System
open System.Net.Http
open System.Threading

open FsToolkit.ErrorHandling

//***********************************************

open Api.Logging

open Helpers
open Helpers.Builders

open Types.Haskell_IO_Monad_Simulation

#if ANDROID

open Android.OS
open Android.App
open Android.Net
open Android.Views
open Android.Content
open Android.Runtime
open Android.Provider 

open Xamarin
open Xamarin.Essentials

module WakeLockHelper = //pouze pro Android API 33 a Android API 34

    let internal acquireWakeLock (lock : PowerManager.WakeLock) = 

        IO (fun () 
                ->
                match lock with
                | lock when lock.IsHeld 
                    ->
                    // WakeLock is already held, no need to acquire it again
                    ()
                | _ ->
                    lock.Acquire() // Acquire the WakeLock if not already held
           )     
       
    let internal releaseWakeLock (lock : PowerManager.WakeLock) =  

        IO (fun () 
                ->
                match lock with
                | lock
                    when lock.IsHeld 
                        ->
                        lock.Release()
                | _     -> 
                        ()
           )

module KeepScreenOnManager = //DeviceDisplay.KeepScreenOn z .NET MAUI hodil exn, proto primo API z Androidu

    let internal keepScreenOn enable =

        IO (fun () 
                ->
                pyramidOfDoom
                    {
                        let! activity = Platform.CurrentActivity |> Option.ofNull, ()
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
                            | true  -> 
                                    return Environment.IsExternalStorageManager
                            | false ->
                                    let! status = Permissions.CheckStatusAsync<Permissions.StorageRead>() |> Async.AwaitTask
                                    return status = PermissionStatus.Granted
                                    
                        with
                        | ex 
                            -> 
                            runIO (postToLog2 <| string ex.Message <| "#0001Android") 
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
                            use! context = Application.Context |> Option.ofNull, None
                            use! packageManager = context.PackageManager |> Option.ofNull, None            
                            use! intent = packageManager.GetLaunchIntentForPackage(context.PackageName) |> Option.ofNull, None
            
                            do! 
                                intent.AddFlags
                                    (
                                        ActivityFlags.NewTask |||
                                        ActivityFlags.ClearTop |||
                                        ActivityFlags.ClearTask |||
                                        ActivityFlags.BroughtToFront |||
                                        ActivityFlags.SingleTop
                                    )
                                |> Option.ofNull
                                |> Option.map (fun _ -> ()), None
            
                            do context.StartActivity intent 
                            return Some () // Return unit option
                        }
                with
                | ex
                    -> 
                    runIO (postToLog2 <| string ex.Message <| "#0002Android")
                    None  
           )
    
    //Not used yet
    let internal sendAppToBackground () =

        IO (fun () 
                ->
                try
                    pyramidOfDoom
                        {
                            use! context = Application.Context |> Option.ofNull, None                
                            use homeIntent : Intent = new Intent(Intent.ActionMain)
                            do! 
                                homeIntent.AddCategory(Intent.CategoryHome)
                                          .SetFlags(ActivityFlags.NewTask)
                                          |> Option.ofNull 
                                          |> Option.map (fun _ -> ()), None

                            return! Some <| context.StartActivity homeIntent 
                        }
                with
                | ex 
                    ->
                    runIO (postToLog2 <| string ex.Message <| "#0003Android")
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

                            //use! intent = new Intent(Settings.ActionApplicationDetailsSettings) |> Option.ofNull, None
                            use! intent = new Intent(intentAction : string) |> Option.ofNull, None
                            do!
                                intent.AddFlags
                                    (
                                        ActivityFlags.NewTask |||
                                        ActivityFlags.ClearTop |||
                                        ActivityFlags.ClearTask |||
                                        ActivityFlags.BroughtToFront |||
                                        ActivityFlags.SingleTop
                                    )
                                |> Option.ofNull
                                |> Option.map (fun _ -> ()), None
    
                            use! uri = Uri.FromParts("package", Application.Context.PackageName, null) |> Option.ofNull, None
                            do!
                                intent.SetData uri
                                |> Option.ofNull
                                |> Option.map (fun _ -> ()), None
    
                            return Some <| Application.Context.StartActivity intent
                        }
                        |> Option.defaultValue () //TODO logfile + vymysli tady neco, co zrobit v teto situaci
                with
                | ex -> runIO (postToLog2 <| string ex.Message <| "#0004Android")
                
        )

#endif

(*

C#

//Predpoklad: url a fileName mame pod kontrolou ohledne null, nejsou z .NET library
//Predpoklad: mame slozite typy, kde nejsou "default" hodnoty jako Cancellation.None nebo String.Empty 

public class AndroidDownloadManager
{
    public void StartDownload(string url, string fileName)
    {
        try
        {
            var context = Application.Context;
            if (context == null)
                throw new InvalidOperationException("Application context is not available.");

            var downloadManager = (DownloadManager)context.GetSystemService(Context.DownloadService);
            if (downloadManager == null)
                throw new InvalidOperationException("DownloadManager service is not available on this device.");

            var uri = Uri.Parse(url);
            if (uri == null)
                throw new ArgumentNullException(nameof(uri), "Failed to parse the URL into a URI.");

            var request = new DownloadManager.Request(uri);
            if (request == null)
                throw new InvalidOperationException("Failed to create download request.");

            request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);

            var downloadsDir = Android.OS.Environment.DirectoryDownloads;
            if (downloadsDir == null)
                throw new InvalidOperationException("Downloads directory is not accessible.");

            request.SetDestinationInExternalPublicDir(downloadsDir, fileName);

            downloadManager.Enqueue(request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StartDownload: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("StartDownload execution finished.");
        }
    }

    public void StartDownloadFunctionalStyle(string url, string fileName)
    {
        try
        {
            var context = Application.Context;

            if (context != null)
            {
                var downloadManager = (DownloadManager)context.GetSystemService(Context.DownloadService);

                if (downloadManager != null)
                {
                    Uri uri = Uri.Parse(url);

                    if (uri != null)
                    {
                        var request = new DownloadManager.Request(uri);

                        if (request != null)
                        {
                            request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);

                            var downloadsDir = Android.OS.Environment.DirectoryDownloads;

                            if (downloadsDir != null)
                            {
                                request.SetDestinationInExternalPublicDir(downloadsDir, fileName);

                                // If everything is valid, enqueue the download
                                downloadManager.Enqueue(request);
                            }
                            else
                            {
                                throw new InvalidOperationException("Downloads directory is not accessible.");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException("Failed to create download request.");
                        }
                    }
                    else
                    {
                        throw new ArgumentNullException(nameof(uri), "Failed to parse the URL into a URI.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("DownloadManager service is not available on this device.");
                }
            }
            else
            {
                throw new InvalidOperationException("Application context is not available.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in StartDownloadFunctionalStyle: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("StartDownloadFunctionalStyle execution finished.");
        }
    }
}



When you throw exceptions in C#, even if they're not triggered, you still introduce overhead because the runtime must account for the possibility that exceptions 
might occur, which can affect overall performance.

*)