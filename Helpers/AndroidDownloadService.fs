namespace Helpers

open System
open Helpers.Builders

#if ANDROID
open Android.App
open Android.Net
open Android.Content

module AndroidDownloadService =

    let internal downloadManager (url: string) (fileName: string) =

        pyramidOfDoom
            {
                let! downloadManager = Application.Context.GetSystemService(Context.DownloadService) :?> DownloadManager |> Option.ofNull, None
                
                let! uri = Uri.Parse(url) |> Option.ofNull, None
                
                let! request = new DownloadManager.Request(uri) |> Option.ofNull, None
                
                let! _ = request.SetNotificationVisibility(DownloadVisibility.Hidden) |> Option.ofNull, None

                let! _ = request.SetDestinationInExternalPublicDir(Android.OS.Environment.DirectoryDownloads, fileName) |> Option.ofNull, None                                

                return Some <| downloadManager.Enqueue(request)        
            }

#endif

(*
public class AndroidDownloadManager
{
    public void StartDownload(string url, string fileName)
    {
        var downloadManager = (DownloadManager)Application.Context.GetSystemService(Context.DownloadService);

        var uri = Android.Net.Uri.Parse(url);

        var request = new DownloadManager.Request(uri);

        request.SetNotificationVisibility(DownloadVisibility.VisibleNotifyCompleted);

        request.SetDestinationInExternalPublicDir(Android.OS.Environment.DirectoryDownloads, fileName);

        downloadManager.Enqueue(request);
    }
}
*)