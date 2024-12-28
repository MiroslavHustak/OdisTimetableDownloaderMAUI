namespace ApplicationDesign

open System
open System.IO
open System.Net
open System.Threading

open Microsoft.Maui.Devices

//**********************************

open Types.Types   
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open BusinessLogic.DPO_BL
   
open Settings.Messages 
open Settings.SettingsGeneral  

module WebScraping_DPO =

    //Design pattern for WebScraping_DPO : AbstractApplePlumCherryApricotBrandyProxyDistilleryBean 
    
    //************************Main code********************************************************************************
  
    type private State =  
        { 
            TimetablesDownloadedAndSaved: unit //zatim nevyuzito
        }

    let private stateDefault = 
        {          
            TimetablesDownloadedAndSaved = () //zatim nevyuzito
        }

    type private Actions =       
        | DeleteOneODISDirectory
        | CreateFolders
        | FilterDownloadSave  

    type private Environment = 
        {
            FilterTimetables : unit -> string -> (string * string) list
            DownloadAndSaveTimetables : (float * float -> unit) -> CancellationToken -> (string * string) list -> Result<unit, MHDErrors>
        }

    let private environment: Environment =
        { 
            FilterTimetables = filterTimetables
            DownloadAndSaveTimetables = downloadAndSaveTimetables
        }    

    let internal webscraping_DPO reportProgress token pathToDir =  

        let stateReducer token (state : State) (action : Actions) (environment : Environment) =
            
            let dirList pathToDir = [ sprintf"%s/%s"pathToDir ODISDefault.OdisDir5 ] //Android jen forward slash %s/%s

            match action with       
            | DeleteOneODISDirectory 
                ->                                   
                try
                    let dirName = ODISDefault.OdisDir5

                    //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                    let dirInfo = DirectoryInfo pathToDir   
                        in 
                        dirInfo.EnumerateDirectories()
                        |> Seq.filter (fun item -> item.Name = dirName) 
                        |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce 
                        |> Ok
                with
                | _ -> Error FileDownloadErrorMHD //dpoMsg1                                         
                                    
            | CreateFolders         
                -> 
                try                                          
                    dirList pathToDir
                    |> List.iter (fun dir -> Directory.CreateDirectory(dir) |> ignore)   
                    |> Ok
                with
                | _ -> Error FileDownloadErrorMHD //dpoMsg1

            | FilterDownloadSave   
                ->                                      
                try  
                    try                        
                        //DeviceDisplay.KeepScreenOn <- true //throws an exception

                        #if ANDROID
                        KeepScreenOnManager.keepScreenOn true
                        #endif

                        let pathToSubdir =
                            dirList pathToDir 
                            |> List.tryHead 
                            |> function Some value -> value | None -> String.Empty
                            in
                            match pathToSubdir |> Directory.Exists with 
                            | false ->
                                    Error FileDeleteErrorMHD                             
                            | true  -> 
                                    environment.FilterTimetables () pathToSubdir 
                                    |> environment.DownloadAndSaveTimetables reportProgress token
                    finally
                        //DeviceDisplay.KeepScreenOn <- false //throws an exception

                        #if ANDROID
                        KeepScreenOnManager.keepScreenOn false
                        #endif
                        ()                         
                with
                | _ -> Error FileDownloadErrorMHD //dpoMsg2                                               
                       
        pyramidOfInferno
            {  
                let errFn err =  

                    match err with
                    | BadRequest            -> "400 Bad Request"
                    | InternalServerError   -> "500 Internal Server Error"
                    | NotImplemented        -> "501 Not Implemented"
                    | ServiceUnavailable    -> "503 Service Unavailable"        
                    | NotFound              -> "404 Page Not Found"
                    | CofeeMakerUnavailable -> "418 I'm a teapot. Look for a coffee maker elsewhere."
                    | FileDownloadErrorMHD  -> dpoMsg1
                    | ConnectionError       -> noNetConn
                    | FileDeleteErrorMHD    -> fileDeleteError

                let! _ = stateReducer token stateDefault DeleteOneODISDirectory environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault CreateFolders environment, fun err -> Error <| errFn err
                let! _ = stateReducer token stateDefault FilterDownloadSave environment, fun err -> Error <| errFn err
            
                return Ok ()
            }

(*

//ScreenHelper.fs (Android Project)
namespace YourAppNamespace

open Android.App
open Android.Views
open Microsoft.Maui

module ScreenHelper =
    let setKeepScreenOn (activity: Activity) (keepScreenOn: bool) =
        if activity <> null then
            if keepScreenOn then
                activity.Window.AddFlags(WindowManagerFlags.KeepScreenOn)
            else
                activity.Window.ClearFlags(WindowManagerFlags.KeepScreenOn)


KeepScreenOnManager.fs (Android Project)

namespace YourAppNamespace

open Android.App
open YourAppNamespace.ScreenHelper
open Microsoft.Maui

module KeepScreenOnManager =
    let setKeepScreenOn (enable: bool) =
        let activity = Platform.CurrentActivity
        setKeepScreenOn activity enable


. Platform.CurrentActivity
Ensure that your app is using MAUI Essentials or the required APIs to access Platform.CurrentActivity. This setup is typically already configured in a MAUI project. If Platform.CurrentActivity is not available, make sure your Android project has the correct setup.

Check for MainActivity in MainActivity.fs:
fsharp
Copy code
[<Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true)>]
type MainActivity() =
    inherit MauiAppCompatActivity()
If the above is in place, you’re set to use Platform.CurrentActivity.


open YourAppNamespace.KeepScreenOnManager

let performDownload dispatch =
    async {
        try
            // Keep the screen on during the download
            setKeepScreenOn true

            // Simulate a long-running download task
            do! Async.Sleep 5000 // Replace this with your actual download logic

            // Dispatch success message
            dispatch "Download complete"
        finally
            // Always clear the KeepScreenOn flag to prevent battery drain
            setKeepScreenOn false
    }
    |> Async.Start

*)

