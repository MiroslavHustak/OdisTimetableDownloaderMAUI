namespace OdisTimetableDownloaderMAUI

open Android.App
open Android.OS
open Android.Content

open Microsoft.Maui

open Helpers
open Api.Logging
open Types.Haskell_IO_Monad_Simulation

module NotificationChannelManager =

    let createChannel (context : Context) =
    
        match Build.VERSION.SdkInt >= BuildVersionCodes.O with
        | false
            -> 
            ()
        | true  
            ->
            match context.GetSystemService(Context.NotificationService) |> Option.ofNull' with
            | None 
                -> 
                runIO <| postToLog2 "Could not get NotificationManager" " #1006Android"
            | Some gss 
                ->
                let manager = gss :?> NotificationManager
                let channel =
                    new NotificationChannel(
                        "download_channel",
                        "Timetable Downloads",
                        NotificationImportance.Low
                    )
                manager.CreateNotificationChannel channel

[<Application>]
type MainApplication(handle, ownership) =

    inherit MauiApplication(handle, ownership)
    override _.CreateMauiApp() = MauiProgram.CreateMauiApp()
    override this.OnCreate() =
        base.OnCreate()
        NotificationChannelManager.createChannel this