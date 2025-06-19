namespace Helpers

open System
open System.Net.Http
open System.Threading

open Helpers
open Helpers.Builders

open FsToolkit.ErrorHandling

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

//**********************************************************************************

open Java.Interop
open Javax.Net.Ssl

// Java Interoperability Code for Custom SSL/TLS Handling on Android
// For testing unsafe code only ! Not to be used in production !

type TrustAllHostnameVerifier() =

    inherit Java.Lang.Object() 

    interface IHostnameVerifier with
        member _.Verify(hostname : string, session : Javax.Net.Ssl.ISSLSession) = true

type TrustAllCertsManager() =

    inherit Java.Lang.Object() 

    interface IX509TrustManager with
        member _.GetAcceptedIssuers() = null
        member _.CheckClientTrusted(chain, authType) = ()
        member _.CheckServerTrusted(chain, authType) = ()

// Custom HttpClientHandler for bypassing SSL on Android
type UnsafeAndroidClientHandler() =

    inherit HttpClientHandler()

    do
        let trustAllCerts = [| new TrustAllCertsManager() :> ITrustManager |]

        let sslContext = SSLContext.GetInstance("TLS")
        sslContext.Init(null, trustAllCerts, new Java.Security.SecureRandom())

        HttpsURLConnection.DefaultSSLSocketFactory <- sslContext.SocketFactory
        HttpsURLConnection.DefaultHostnameVerifier <- new TrustAllHostnameVerifier()


//**************************************************************************************

module WakeLockHelper = //pouze pro Android API 33 a Android API 34

    let internal acquireWakeLock (lock : PowerManager.WakeLock) = 
    
        match lock with
        | lock
            when lock.IsHeld 
                ->
                ()
        | _     -> 
                lock.Acquire()
       
    let internal releaseWakeLock (lock : PowerManager.WakeLock) =  

        match lock with
        | lock
            when lock.IsHeld 
                ->
                lock.Release()
        | _     -> 
                ()

module KeepScreenOnManager = //DeviceDisplay.KeepScreenOn z .NET MAUI hodil exn, proto primo API z Androidu

    let internal keepScreenOn enable =
        
        pyramidOfDoom
            {
                let! activity = Platform.CurrentActivity |> Option.ofNull, ()

                let flags =
                    WakeLockFlags.ScreenDim ||| WakeLockFlags.AcquireCausesWakeup ||| WakeLockFlags.OnAfterRelease

                let! getSystemService = activity.GetSystemService(Context.PowerService) |> Option.ofNull, ()  
                let! lock = 
                    let powerManager = getSystemService :?> PowerManager        
                    Some <| powerManager.NewWakeLock(flags, "MyApp:PreventSleepDuringDownload"), ()  
                    
                let!_ = enable |> Option.ofBool, WakeLockHelper.releaseWakeLock lock
                do WakeLockHelper.acquireWakeLock lock               
                let!_ = enable |> Option.ofBool, activity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn) 

                return activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn)        
            }    

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

    //Not used yet
    let internal bringAppToForeground () =

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
            
                    do context.StartActivity(intent) 
                    return Some () // Return unit option
                }
        with
        | ex
            -> 
            string ex.Message |> ignore<string> // TODO: logfile
            None  
    
    //Not used yet
    let internal sendAppToBackground () =
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

                    return! Some <| context.StartActivity(homeIntent) 
                }
        with
        | ex 
            ->
            string ex.Message |> ignore<string> // TODO: logfile
            None    

    let internal openAppSettings () =
    
        try
            Thread.Sleep 500 
                    
            pyramidOfDoom
                {
                    use! intent = new Intent(Android.Provider.Settings.ActionApplicationDetailsSettings) |> Option.ofNull, None
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
                        intent.SetData(uri)
                        |> Option.ofNull 
                        |> Option.map (fun _ -> ()), None

                    return! Some <| Application.Context.StartActivity(intent)
                }

            |> Option.defaultValue () //TODO logfile + vymysli tady neco, co zrobit v teto situaci
                    
        with
        | ex
            ->
            string ex.Message |> ignore<string> // Log error
            ()
            

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