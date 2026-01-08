(*
Code in this file uses Fabulous, a functional-first UI framework.

https://fabulous.dev
https://github.com/fabulous-dev/Fabulous

Copyright 2016-2023 Timothée Larivoir, Edgar Gonzales, and contributors

Licensed under the Apache License, Version 2.0 (the "License")
*)

namespace OdisTimetableDownloaderMAUI

open System.IO
open System.Net
open System.Threading

open Microsoft.Maui.Hosting
open Microsoft.Maui.LifecycleEvents
open Microsoft.Maui.ApplicationModel

open Fabulous
open Fabulous.Maui

//******************************************

open Api.Logging
open Types.Haskell_IO_Monad_Simulation

type MauiProgram = 

    // MAUI World

    static member CreateMauiApp(): MauiApp =

        try
            ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 

            let builder : MauiAppBuilder =

                MauiApp
                    .CreateBuilder()
                    .UseFabulousApp(App.program)
                    .ConfigureFonts(
                        fun (fonts : IFontCollection)
                            ->
                            fonts
                                .AddFont("OpenSans-Regular.ttf", "OpenSansRegular")
                                .AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold")
                            |> ignore<IFontCollection>
                    )

            #if ANDROID
            builder.ConfigureLifecycleEvents(
                fun (events : ILifecycleBuilder) 
                    ->
                    events.AddAndroid(
                        fun (android : IAndroidLifecycleBuilder) 
                            ->
                            (*
                            // When app goes to background 
                            android.OnPause(
                                fun _
                                    ->
                                    //App.cancellationActor.Post Types.Types.CancelCurrent   //not my intent  
                                    ()
                                )
                                |> ignore<ILifecycleBuilder>    

                            android.OnStop
                                (fun _ -> App.stopCancellationActorAsync()) |> ignore<ILifecycleBuilder> //not my intent
                            *) 

                            android.OnResume(
                                fun (_activity : Android.App.Activity) 
                                    ->
                                    match App.DispatchHolder.DispatchRef with
                                    | Some (weakRef : System.WeakReference<Dispatch<App.Msg>>) 
                                        ->
                                        match weakRef.TryGetTarget() with
                                        | true, (dispatch : Dispatch<App.Msg>)
                                            ->                                        
                                            async 
                                                {
                                                    try
                                                        let! (granted : bool) =
                                                            async
                                                                {
                                                                    match Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R with
                                                                    | true 
                                                                        ->
                                                                        return Android.OS.Environment.IsExternalStorageManager
                                                                    | false
                                                                        ->
                                                                        let! (status : PermissionStatus) =
                                                                            Permissions.CheckStatusAsync<Permissions.StorageRead>()
                                                                            |> Async.AwaitTask
                                                                        return status = PermissionStatus.Granted
                                                                  }
        
                                                        match granted with
                                                        | true 
                                                            ->
                                                            (dispatch : Dispatch<App.Msg>) <| App.PermissionResult true
                                                            (dispatch : Dispatch<App.Msg>) <| App.Home2
                                                        | false
                                                            -> 
                                                            ()
                                                    with
                                                    | ex -> runIO (postToLog (string ex.Message) "#3002")

                                                    return ()
                                                }

                                            |> Async.StartImmediate 

                                        | false, _ 
                                            ->
                                            () //runIO (postToLog "For testing purposes" "#3001")

                                    | None 
                                        ->
                                        () //runIO (postToLog "For testing purposes" "#3000")

                                ) |> ignore<IAndroidLifecycleBuilder>
                        ) |> ignore<ILifecycleBuilder>
                ) |> ignore<MauiAppBuilder>

            #endif        
       
            builder.Build()

        with
        | ex
            ->
            runIO (postToLog (string ex.Message) "#3008")
            MauiApp.CreateBuilder().Build() //dummy process quli typu, dulezite je logging exception
