﻿namespace Helpers

open System

open Helpers
open Helpers.Builders

#if ANDROID
open Android.App
open Android.Net
open Android.Content
open Android.Provider 

open Xamarin
open Xamarin.Essentials

module AndroidDownloadService =

    let internal downloadManager (url: string) (fileName: string) =

        pyramidOfDoom
            {
                let! context = Application.Context |> Option.ofNull, None
                let! downloadManager = context.GetSystemService(Context.DownloadService) :?> DownloadManager |> Option.ofNull, None                
                let! uri = Uri.Parse(url) |> Option.ofNull, None                
                let! request = new DownloadManager.Request(uri) |> Option.ofNull, None                
                let! _ = request.SetNotificationVisibility(DownloadVisibility.Hidden) |> Option.ofNull, None
                let! downloadsDir = Android.OS.Environment.DirectoryDownloads |> Option.ofNull, None
                let! _ = request.SetDestinationInExternalPublicDir(downloadsDir, fileName) |> Option.ofNull, None                                

                return Some <| downloadManager.Enqueue(request)        
            }

    let internal downloadManagerResult (url: string) (fileName: string) =

        try
            pyramidOfDoom 
                {
                    let! context = Application.Context |> Option.ofNull, Error "Application context is not available."
                    let! downloadManager = context.GetSystemService(Context.DownloadService) :?> DownloadManager |> Option.ofNull, Error "DownloadManager service is not available on this device."
                    let! uri = Uri.Parse(url) |> Option.ofNull, Error "Failed to parse the URL into a URI."
                    let! request = new DownloadManager.Request(uri) |> Option.ofNull, Error "Failed to create request."
                    let! _ = request.SetNotificationVisibility(DownloadVisibility.Hidden) |> Option.ofNull, Error "Failed to set notification visibility."
                    let! downloadsDir = Android.OS.Environment.DirectoryDownloads |> Option.ofNull, Error "Downloads directory is not accessible."
                    let! _ = request.SetDestinationInExternalPublicDir(downloadsDir, fileName) |> Option.ofNull, Error "Failed to set destination directory."
    
                    return Ok <| downloadManager.Enqueue(request)
                }
        with
        | ex -> Error (sprintf "Error in StartDownload: %s" ex.Message)

module AndroidUIHelpers =

    let internal permissionCheck () =
    
        async
            {
                let! status = Permissions.CheckStatusAsync<Permissions.StorageRead>() |> Async.AwaitTask
    
                match status with
                | PermissionStatus.Granted
                    -> 
                    return true
                | _ -> 
                    return false
            }   
             
    let internal bringAppToForeground () =
        
        try
            pyramidOfDoom
                {
                    let! context = Application.Context |> Option.ofNull, None
                    let! packageManager = context.PackageManager |> Option.ofNull, None
                    let! intent = packageManager.GetLaunchIntentForPackage(context.PackageName) |> Option.ofNull, None  
                    let! _ = intent.AddFlags(ActivityFlags.NewTask ||| ActivityFlags.ClearTask) |> Option.ofNull, None
                    let! _ = context.StartActivity(intent) |> Option.ofNull, None
                
                    return Some ()        
                }
        with
        | ex
            -> 
            string ex.Message |> ignore // TOTO logfile
            None

    let internal openAppSettings () =
        try
            pyramidOfDoom
                {
                    let!_ = bringAppToForeground (), None
                    do async { return! Async.Sleep 500 } |> Async.RunSynchronously // Allow the app to stabilize in the foreground
                    let! intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings) |> Option.ofNull, None
                    let!_ = intent.AddFlags(ActivityFlags.NewTask) |> Option.ofNull, None
                    let! uri = Uri.FromParts("package", Application.Context.PackageName, null) |> Option.ofNull, None
                    let!_ = intent.SetData(uri) |> Option.ofNull, None 
                    let!_ = Application.Context.StartActivity(intent)|> Option.ofNull, None

                    return Some ()
                }
        with
        | ex
            -> 
            string ex.Message |> ignore // TOTO logfile
            None 

        |> function   
            | Some value 
                -> async { return value }
            | None
                -> async { return () } //TODO vymyslet, co tady dat   
       
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