(*
Code in this file uses Fabulous, a functional-first UI framework.

https://fabulous.dev
https://github.com/fabulous-dev/Fabulous

Copyright 2016-2023 Timothée Larivoir, Edgar Gonzales, and contributors

Licensed under the Apache License, Version 2.0 (the "License")
*)

(*
 I'm looking for a UX/UI expert to create the design for this mobile app. 
 Since the project uses an Elmish architecture, front-end skills are welcomed.
*)

//^(?!\s*//)(?!\s*open[\s(])(?!\s*[(){}\s]*$).+$
//or replace open with some string not appearing in fs code

//dotnet fsi OdisDownloaderMAUI_build_release.fsx
//dotnet fsi OdisDownloaderMAUI_build_release_publish_apk.fsx

namespace OdisTimetableDownloaderMAUI

open System

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Controls
open Microsoft.Maui.Graphics

open FsToolkit.ErrorHandling

#if ANDROID
open Xamarin
open Xamarin.Essentials  
#endif

open type Fabulous.Maui.View

open ActorModels
open ProgressCircle

open Settings.Messages
open Settings.SettingsGeneral

open Types.Types
open Types.Haskell_IO_Monad_Simulation

open Theme
open ScreenHelpers

open Api.Logging
open IO_Operations.IO_Operations
open Helpers.ConnectivityWithDebouncing

#if ANDROID
open Helpers.Builders
open AndroidUIHelpers 
open JavaInteroperabilityCode.RealInternetChecker
#endif

open OdisTimetableDownloaderMAUI.Engines.Dpo
open OdisTimetableDownloaderMAUI.Engines.Mdpo

open OdisTimetableDownloaderMAUI.Engines.KodisTP
open OdisTimetableDownloaderMAUI.Engines.KodisCanopy

module App =

    type PermissionState = 
        | Granted 
        | NotGranted

    type Connectivity =
        | Connected of string
        | Disconnected of string       

    type DownloadType =
        | KodisJsonTP
        | KodisPdfTP
        | KodisCanopy4 
        | Dpo 
        | Mdpo

    type ProgressState =
        | Idle
        | InProgress of current : float * total : float

    type Screen =
        | Home
        | Utilities
        | ClearingConfirm
        | Downloading of DownloadType * ProgressState
        | Completed of string
        | CompletedUtilities of string
        | NoConnection
        | NoPermission
        | ErrorScreen of string

    type ButtonType =
        | Clear
        | ClearYes
        | ClearNot
        | Restart
       
    type Model =
        {
            Permission   : PermissionState
            Connectivity : Connectivity
            Screen       : Screen
            Status       : string
            ActiveButton : ButtonType option
            IsClearing   : bool 
        }

    type Msg =
        | KodisTPMsg        of KodisTPMsg
        | KodisCanopyMsg    of KodisCanopyMsg
        | DpoMsg            of DpoMsg
        | MdpoMsg           of MdpoMsg
        | NetConnMessage    of string
        | SetScreen         of Screen
        | Navigate          of Screen
        | Click             of ButtonType
        | StartDownload     of DownloadType
        | CancelDownload    of DownloadType
        | RequestPermission
        | OpenStorageViewer of string
        | RunFileLauncher
        | Dummy
        | Quit

    // =============================================
    // CANCELLATION AND CONNECTIVITY HELPERS
    // =============================================

    let connectivityDebouncerSubscription (_model : Model) =
    
        let sub (dispatch : Msg -> unit) =       
            let debounceActor = debounceActor NetConnMessage dispatch              
            //250 = debounceMs, (now - lastChangeTime).TotalSeconds > 0.5 resp. 0.2 - s temito hodnotami si pohrat
            runIO <| startConnectivityMonitoring 250 (fun isConnected -> debounceActor.Post isConnected)     
                
        Cmd.ofSub sub

    let private kodisJsonActor   = localCancellationActor()
    let private kodisPdfActor    = localCancellationActor()
    let private kodisCanopyActor = localCancellationActor()         
    let private dpoActor         = localCancellationActor()
    let private mdpoActor        = localCancellationActor()

    let private connectivity msg = 
        match isNowConnected () with    
        | true  -> Connected yesNetConn
        | false -> Disconnected msg // nech to tak, aji kdyz zatim je jen noNetConn, mozna se bude hodit
          
    // =============================================
    // INIT
    // =============================================
    
    let init () : Model * Cmd<Msg> =

        #if ANDROID
        let permissionGranted = 
            permissionCheck >> runIO >> Async.RunSynchronously <| ()
        #else
        let permissionGranted = true
        #endif
    
        let permission = 
            match permissionGranted with true -> Granted | false -> NotGranted        
    
        let connectivity = connectivity noNetConn
    
        let initialScreen = 
            match permission, connectivity with
            | NotGranted, _        -> NoPermission
            | _, Disconnected _    -> NoConnection  
            | Granted, Connected _ -> Home
    
        let baseModel =
            {
                Permission   = permission
                Connectivity = connectivity
                Screen       = initialScreen
                Status = 
                    match permission with
                    | Granted   
                        ->
                        match initialScreen with
                        | NoConnection -> noNetConn
                        | _            -> String.Empty                        
                    | NotGranted 
                        -> 
                        appInfoInvoker   
                ActiveButton = None
                IsClearing   = false
            }
    
        match permission with
        | NotGranted 
            ->
            baseModel, Cmd.none
        | Granted 
            ->
            match runIO <| ensureMainDirectoriesExist permissionGranted with
            | Ok _ 
                ->
                baseModel, Cmd.none
            | Error _ 
                ->
                let errMsg = ctsMsg2
                { baseModel with Screen = ErrorScreen errMsg; Status = errMsg }, Cmd.none

    // =============================================
    // UPDATE
    // =============================================

    let update (msg : Msg) (m : Model) : Model * Cmd<Msg> =

        let connectivity = connectivity noNetConn
         
        match msg with   
        | Dummy 
            -> 
            m, Cmd.none

        | SetScreen s 
            ->
            match m.Screen, s with
            | Downloading _, Home  // don't interrupt a download
                ->
                m, Cmd.none
            | _ 
                ->
                { m with Screen = s; Status = String.Empty }, Cmd.none
   
        | Navigate screen
            ->
            { 
                m with 
                    Screen = screen
                    ActiveButton = None
                    Status = String.Empty
                    IsClearing = false
            }, Cmd.none  
   
        | Click Clear 
            ->
            { m with ActiveButton = Some Clear },
            Cmd.ofMsg (Navigate ClearingConfirm)
   
        | Click ClearYes 
            ->
            let clearDataCmd =
                Cmd.ofSub
                    (fun dispatch 
                        ->
                        async
                            {
                                try
                                    do! Async.SwitchToThreadPool()

                                    let! results =
                                        [
                                            async { return deleteOld () |> runIO }
                                            async { return deleteOld4 () |> runIO }
                                        ]
                                        |> Async.Parallel
                                        |> Async.Catch
                                                
                                    match results |> Result.ofChoice with
                                    | Ok [| Ok (); Ok () |] -> dispatch (Navigate (CompletedUtilities deleteOldTimetablesMsg2)) 
                                    | _                     -> dispatch (Navigate (ErrorScreen deleteOldTimetablesMsg3))

                                    do! Async.Sleep 1000

                                    return ()
                                with
                                | ex ->
                                    runIO <| postToLog2 (string ex.Message) "#XElmish_ClearData"
                                    dispatch (Navigate (Completed deleteOldTimetablesMsg3))
                                    return ()
                            }
                        |> Async.Start
                    )

            { 
                m with 
                    ActiveButton = Some ClearYes
                    Status = deleteOldTimetablesMsg1
                    IsClearing = true
            }, clearDataCmd

        | Click ClearNot 
            ->
            { m with ActiveButton = Some ClearNot },
            Cmd.ofMsg (Navigate Utilities)
   
        | Click Restart
            ->
            { m with ActiveButton = Some Restart },
            Cmd.ofMsg (Navigate Home)

        | Quit  
            ->              
            #if WINDOWS           
            let cmd () : Cmd<Msg> =
                async 
                    {
                        let! _ = saveJsonToFileAsync >> runIO <| ()
                        return Dummy
                    }
                |> Cmd.ofAsyncMsg     
                
            match m.Screen with
            | Downloading _ 
                ->
                kodisJsonActor.PostAndReply(fun reply -> StopLocal reply)
                kodisPdfActor.PostAndReply(fun reply -> StopLocal reply)
                kodisCanopyActor.PostAndReply(fun reply -> StopLocal reply)
                mdpoActor.PostAndReply(fun reply -> StopLocal reply)
                dpoActor.PostAndReply(fun reply -> StopLocal reply)
            | _ 
                -> 
                ()
          
            let msg = HardRestart.exitApp >> runIO <| () 
            { m with Status = msg }, cmd ()
            #endif

            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn false  

            match m.Screen with
            | Downloading _ 
                ->
                kodisJsonActor.PostAndReply(fun reply -> StopLocal reply)
                kodisPdfActor.PostAndReply(fun reply -> StopLocal reply)
                kodisCanopyActor.PostAndReply(fun reply -> StopLocal reply)
                mdpoActor.PostAndReply(fun reply -> StopLocal reply)
                dpoActor.PostAndReply(fun reply -> StopLocal reply)
            | _ 
                -> 
                ()

            let msg = HardRestart.exitApp >> runIO <| () 

            { m with Status = msg }, Cmd.none
            #endif
      
        | CancelDownload dt 
            ->
            (*
            [
                kodisJsonActor
                kodisPdfActor
                mdpoActor
                dpoActor
            ]
            |> List.iter cancelLocalActor
            *)

            let cancelCmd =
                Cmd.ofAsyncMsg
                    (
                        async 
                            {
                                do! Async.SwitchToThreadPool()

                                match dt with
                                | KodisJsonTP  -> cancelLocalActor2 kodisJsonActor
                                | KodisPdfTP   -> cancelLocalActor2 kodisPdfActor
                                | KodisCanopy4 -> cancelLocalActor2 kodisCanopyActor
                                | Dpo          -> cancelLocalActor2 dpoActor
                                | Mdpo         -> cancelLocalActor2 mdpoActor

                                System.GC.Collect(2, System.GCCollectionMode.Forced, blocking = false, compacting = false)

                                return Navigate Home
                            }
                    )
            
            { m with Status = cancelMsg42 }, cancelCmd        
   
        | RequestPermission
            ->
            #if ANDROID
            let cmd =
                Cmd.ofAsyncMsg
                    (
                        async 
                            {
                                try
                                    let! currentStatus = 
                                        Permissions.CheckStatusAsync<Permissions.StorageRead>() 
                                        |> Async.AwaitTask

                                    let needsRequest = 
                                        match Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R with
                                        | true  -> not Android.OS.Environment.IsExternalStorageManager
                                        | false -> currentStatus <> PermissionStatus.Granted

                                    match needsRequest with
                                    | false 
                                        -> 
                                        return Navigate Home
                                    | true  
                                        ->
                                        do openAppSettings >> runIO <| ()
                                        do! Async.Sleep 1000

                                        let! newStatus = 
                                            Permissions.CheckStatusAsync<Permissions.StorageRead>() 
                                            |> Async.AwaitTask

                                        match newStatus = PermissionStatus.Granted with
                                        | true  -> return SetScreen Home
                                        | false -> return Dummy
                                with 
                                | _ -> return Dummy
                            }
                    )
            m, cmd
            #else
            m, Cmd.none
            #endif

        | OpenStorageViewer fabulousTimetablesFolder
            ->
            #if ANDROID
                m,
                    Cmd.ofSub 
                        (fun dispatch 
                            ->
                            try
                                runIO 
                                <| FileLauncher.openStorageRoot Android.App.Application.Context fabulousTimetablesFolder
                            with 
                            | _ -> dispatch (ErrorScreen >> SetScreen <| androidFolderAccessError)
                        )
            #else
                m, Cmd.none
            #endif

        | RunFileLauncher
            ->
            let cmd = 
                try
                    let logFileNameDiff =                   
                        #if ANDROID
                        logFileNameAndroid 
                        #else
                        logFileNameWindows                        
                        #endif

                    match runIO <| ComparisonResultFileLauncher.openTextFileReadOnly logFileNameDiff with
                    | Some app 
                        ->                           
                        async
                            {
                                match! app with    
                                | true  -> return Dummy
                                | false -> return SetScreen (ErrorScreen launchErrorMsg)                       
                            } 
                        |> Cmd.ofAsyncMsg
                       
                    | None
                        -> 
                        SetScreen >> Cmd.ofMsg <| ErrorScreen launchErrorMsg
                with
                | _ -> SetScreen >> Cmd.ofMsg <| ErrorScreen launchErrorMsg
        
            m, cmd        
 
        | NetConnMessage msg
            ->
            {
                m with 
                    Connectivity = 
                        m.Connectivity
                        |> function 
                            | Connected _    -> Connected msg
                            | Disconnected _ -> Disconnected msg
            }, Cmd.none    
   
        | StartDownload KodisJsonTP 
            ->    
            kodisJsonActor.PostAndReply(fun reply -> GetToken reply) 
            |> function
                | Some token 
                    ->  
                    let cmd =
                        Cmd.ofSub
                            (fun dispatch
                                ->
                                Engines.KodisTP.executeJson
                                    <| fun m -> KodisTPMsg >> dispatch <| m
                                    <| token
                                |> Async.Start                                 
                            )   
                    { 
                        m with
                            Screen       = Downloading (KodisJsonTP, Idle)
                            Status       = progressMsgKodis
                            Connectivity = connectivity 
                    }, cmd

                | None 
                    -> 
                    m, Cmd.none 

        | StartDownload KodisPdfTP 
            ->      
            kodisPdfActor.PostAndReply(fun reply -> GetToken reply) 
            |> function
                | Some token 
                    ->  
                    let cmd = 
                        Cmd.ofSub
                            (fun dispatch
                                ->
                                Engines.KodisTP.executePdf
                                    <| fun m -> KodisTPMsg >> dispatch <| m
                                    <| token
                                |> Async.Start 
                            )                       
                    { 
                        m with
                            Screen       = Downloading (KodisPdfTP, Idle)
                            Status       = dispatchMsg2
                            Connectivity = connectivity 
                    }, cmd    
                    
                | None 
                    -> 
                    m, Cmd.none 

        | StartDownload KodisCanopy4 
            ->
            kodisCanopyActor.PostAndReply(fun reply -> GetToken reply)            
            |> function
                | Some token 
                    ->   
                    let cmd = 
                        Cmd.ofSub
                            (fun dispatch
                                ->
                                Engines.KodisCanopy.execute
                                    <| fun m -> KodisCanopyMsg >> dispatch <| m
                                    <| token
                                |> Async.Start  
                            )
                    { 
                        m with
                            Screen       = Downloading (KodisCanopy4, Idle)
                            Status       = dispatchMsg2_1
                            Connectivity = connectivity 
                    }, cmd

                | None 
                    ->
                    m, Cmd.none 
       
        | StartDownload Dpo
            -> 
            dpoActor.PostAndReply(fun reply -> GetToken reply) 
            |> function
                | Some token 
                    ->
                    let cmd =
                        Cmd.ofSub 
                            (fun dispatch
                                ->
                                Engines.Dpo.executeDpo
                                    <| fun m -> DpoMsg >> dispatch <| m
                                    <| token
                                |> Async.Start  
                            )   
                    { 
                        m with
                            Screen       = Downloading (Dpo, Idle)
                            Status       = hintDpo
                            Connectivity = connectivity 
                    }, cmd 

                | None 
                    -> 
                    m, Cmd.none 
         
        | StartDownload Mdpo
            -> 
            mdpoActor.PostAndReply(fun reply -> GetToken reply) 
            |> function
                | Some token 
                    ->         
                    let cmd =
                        Cmd.ofSub 
                            (fun dispatch
                                ->
                                Engines.Mdpo.executeMdpo
                                    <| fun m -> MdpoMsg >> dispatch <| m
                                    <| token
                                |> Async.Start    
                            )   
                    { 
                        m with
                            Screen       = Downloading (Mdpo, Idle)
                            Status       = hintMdpo  
                            Connectivity = connectivity 
                    }, cmd 

                | None 
                    -> 
                    m, Cmd.none

        | KodisTPMsg msg
            ->
            match msg with
            | Engines.KodisTP.Progress (c, t)
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> 
                    let ps = 
                        match c, t with
                        | 0.0, 1.0 -> Idle  
                        | _        -> InProgress (c, t)
                    { m with Screen = Downloading (dt, ps) }, Cmd.none
                | _ -> 
                    m, Cmd.none      
            
            | Engines.KodisTP.IterationMsg text 
                ->
                { m with Status = text }, Cmd.none
        
            | Engines.KodisTP.Completed result
                ->   
                match m.Screen with            
                | Downloading (KodisJsonTP, _)
                    ->   
                    kodisJsonActor.PostAndReply(fun reply -> StopLocal reply)
                    { m with Status = dispatchMsg2 }, StartDownload >> Cmd.ofMsg <| KodisPdfTP           
               
                | Downloading (KodisPdfTP, _) 
                    ->      
                    kodisPdfActor.PostAndReply(fun reply -> StopLocal reply)
                    { m with Screen = Completed result; Status = String.Empty }, Cmd.none
            
                | _ ->
                    m, Cmd.none
        
            | Engines.KodisTP.ErrorKodis err
                ->
                kodisJsonActor.PostAndReply(fun reply -> StopLocal reply)
                kodisPdfActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = ErrorScreen err }, Cmd.none
        
            | Engines.KodisTP.NavigateHome 
                -> 
                kodisJsonActor.PostAndReply(fun reply -> StopLocal reply)
                kodisPdfActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = Home }, Cmd.none      
                
            | Engines.KodisTP.NoInternet 
                ->
                kodisJsonActor.PostAndReply(fun reply -> StopLocal reply)
                kodisPdfActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = NoConnection; Status = noNetConn }, Cmd.none
                
        | KodisCanopyMsg msg
            ->
            match msg with
            | Engines.KodisCanopy.Progress (c, t)
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> 
                    let ps = 
                        match c, t with
                        | 0.0, 1.0 -> Idle 
                        | _        -> InProgress (c, t)
                    { m with Screen = Downloading (dt, ps) }, Cmd.none
                | _ -> 
                    m, Cmd.none
                   
            | Engines.KodisCanopy.IterationMsg text 
                ->
                { m with Status = text }, Cmd.none
        
            | Engines.KodisCanopy.Completed result
                ->       
                match m.Screen with               
                | Downloading (KodisCanopy4, _) 
                    ->  
                    kodisCanopyActor.PostAndReply(fun reply -> StopLocal reply)
                    { m with Screen = Completed result; Status = String.Empty }, Cmd.none
        
                | _ ->
                    m, Cmd.none
        
            | Engines.KodisCanopy.ErrorKodis err
                -> 
                kodisCanopyActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = ErrorScreen err }, Cmd.none
        
            | Engines.KodisCanopy.NavigateHome 
                -> 
                kodisCanopyActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = Home }, Cmd.none                   
          
            | Engines.KodisCanopy.NoInternet 
                ->
                kodisCanopyActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = NoConnection; Status = noNetConn }, Cmd.none   
                
        | DpoMsg msg
            ->
            match msg with
            | Engines.Dpo.Progress (c, t)
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> { m with Screen = Downloading (dt, InProgress (c, t)) }, Cmd.none
                | _ -> m, Cmd.none       
        
            | Engines.Dpo.IterationMsg text 
                ->
                { m with Status = text }, Cmd.none
        
            | Engines.Dpo.Completed result
                ->            
                match m.Screen with               
                | Downloading (Dpo, _) 
                    ->    
                    dpoActor.PostAndReply(fun reply -> StopLocal reply)
                    { m with Screen = Completed result; Status = hintDpo }, Cmd.none
        
                | _ ->
                    m, Cmd.none
        
            | Engines.Dpo.ErrorDpo err
                -> 
                dpoActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = ErrorScreen err }, Cmd.none
        
            | Engines.Dpo.NavigateHome 
                ->
                dpoActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = Home }, Cmd.none  

            | Engines.Dpo.NoInternet 
                ->
                dpoActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = NoConnection; Status = noNetConn }, Cmd.none
                
        | MdpoMsg msg
            ->
            match msg with
            | Engines.Mdpo.Progress (c, t)
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> { m with Screen = Downloading (dt, InProgress (c, t)) }, Cmd.none
                | _ -> m, Cmd.none       
               
            | Engines.Mdpo.IterationMsg text 
                ->
                { m with Status = text }, Cmd.none
               
            | Engines.Mdpo.Completed result
                ->            
                match m.Screen with               
                | Downloading (Mdpo, _) 
                    ->    
                    mdpoActor.PostAndReply(fun reply -> StopLocal reply)
                    { m with Screen = Completed result; Status = hintMdpo }, Cmd.none
               
                | _ ->
                    m, Cmd.none
               
            | Engines.Mdpo.ErrorMdpo err
                -> 
                mdpoActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = ErrorScreen err }, Cmd.none
               
            | Engines.Mdpo.NavigateHome  
                -> 
                mdpoActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = Home }, Cmd.none  

            | Engines.Mdpo.NoInternet 
                ->
                mdpoActor.PostAndReply(fun reply -> StopLocal reply)
                { m with Screen = NoConnection; Status = noNetConn }, Cmd.none

    // ══════════════════════════════════════════════════════════════════════════
    //  VIEW
    // ══════════════════════════════════════════════════════════════════════════
 
    let view (m : Model) : WidgetBuilder<Msg, IFabApplication> =
      
        let homeButton =
            Border(
                Button(buttonHome, Navigate Home)
                    .background(SolidColorBrush(Colors.Transparent))
                    .textColor(teal800)
                    .font(size = 14.)
                    .borderWidth(0.)
            )
                .background(teal050Brush<Msg>())
                .stroke(teal100Brush<Msg>())
                .strokeShape(RoundRectangle(cornerRadius = 10.))
                .strokeThickness(0.5)
                .padding(Thickness(20., 2., 20., 2.))
                .centerHorizontal() 

        let backToUtilitiesButton =
            Border(
                Button(buttonBackToUtilities, Navigate Utilities)
                    .background(SolidColorBrush(Colors.Transparent))
                    .textColor(teal800)
                    .font(size = 14.)
                    .borderWidth(0.)
            )
                .background(teal050Brush<Msg>())
                .stroke(teal100Brush<Msg>())
                .strokeShape(RoundRectangle(cornerRadius = 10.))
                .strokeThickness(0.5)
                .padding(Thickness(20., 2., 20., 2.))
                .centerHorizontal()   

        let quitButton =
            Border(
                Button(buttonQuit, Quit)
                    .background(SolidColorBrush(Colors.Transparent))
                    .textColor(textSecond)
                    .font(size = 13.)
                    .borderWidth(0.)
            )
                .background(teal100Brush<Msg>())
                .stroke(cardBorderBrush<Msg>())
                .strokeShape(RoundRectangle(cornerRadius = 10.))
                .strokeThickness(0.5)
                .padding(Thickness(0., 2., 0., 2.))
                .margin(Thickness(18., 12., 18., 20.))

        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  1 – Home
        // ══════════════════════════════════════════════════════════════════════════

        let homeView (m : Model) =
        
            let kodisCard =
                actionCard
                    (iconBadge teal050 teal600 "🚌")
                    (StartDownload KodisJsonTP)
                    buttonKodis
                    hintOdis
                |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 0.))
        
            let canopyCard =
                actionCard
                    (iconBadge teal050 teal600 "🚌")
                    (StartDownload KodisCanopy4)
                    buttonKodis4
                    hintOdis
                |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 0.))
        
            let divider1 =
                divider ()
                |> fun v -> v.margin(Thickness(18., 8., 18., 0.))
        
            let dpoCard =
                actionCard
                    (iconBadge blue050 blue800 "🚋")
                    (StartDownload Dpo)
                    buttonDpo
                    hintDpo
                |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 0.))
        
            let mdpoCard =
                #if ANDROID
                disabledCard
                    (iconBadge gray050 gray400 "🚎")
                    buttonMdpo
                    hintMdpo
                |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 0.))
                #else
                actionCard
                    (iconBadge blue050 blue800 "🚎")
                    (StartDownload Mdpo)
                    buttonMdpo
                    hintMdpo
                |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 0.))
                #endif
        
            let divider2 =
                divider ()
                |> fun v -> v.margin(Thickness(18., 8., 18., 0.))
        
            let utilitiesCard =
                actionCard
                    (iconBadge amber050 amber400 "⚙")
                    (Navigate Utilities)
                    buttonUtilities
                    hintUtilities
                |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 12.))
        
            let sectionOdis =
                (sectionLabel labelOdis2)
                    .margin(Thickness(18., 16., 18., 0.))
        
            let sectionDopravci =
                (sectionLabel labelOdis3)
                    .margin(Thickness(18., 4., 18., 0.))
        
            let sectionOstatni =
                (sectionLabel labelOdis4)
                    .margin(Thickness(18., 4., 18., 0.))
        
            let scrollContent =
                VStack(spacing = 0.) {
                    sectionOdis
                    kodisCard
                    canopyCard
                    divider1
                    sectionDopravci
                    dpoCard
                    mdpoCard
                    divider2
                    sectionOstatni
                    utilitiesCard
                }

            let connText =
                match m.Connectivity with
                | Connected msg    -> msg
                | Disconnected msg -> msg
        
            (VStack(spacing = 0.) {
                   topBar connText labelOdis labelOdisExpl                   
                   ScrollView(scrollContent)                   
                   quitButton
               })
                  .centerVertical()
                  .padding(Thickness(20., 32., 20., 20.))
        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  2 – Downloading
        // ══════════════════════════════════════════════════════════════════════════

        let downloadingView (m : Model) (dt : DownloadType) (ps : ProgressState) =

            let subtitle =
                match dt with
                | KodisJsonTP  -> downloadingVariantTP
                | KodisPdfTP   -> downloadingVariantTP
                | KodisCanopy4 -> downloadingVariantCanopy
                | Dpo          -> downloadingVariantDpo
                | Mdpo         -> downloadingVariantMdpo      

            let progressValue =
                match ps with
                | InProgress (curr, total) 
                    when total > 0.0
                    -> min 1.0 (curr / total)
                | _ -> 0.0         
                          
            let progressPct = sprintf "%.0f %%" (progressValue * 100.0)

            let isInProgress =
                match ps with
                | InProgress _ -> true
                | _            -> false

            let percentPill =
                Border(
                    Label(progressPct)
                        .font(size = 20.)
                        .textColor(teal600)
                        .centerTextHorizontal()
                        .centerVertical()
                )
                    .background(teal050Brush<Msg>())
                    .strokeShape(RoundRectangle(cornerRadius = 20.))
                    .stroke(SolidColorBrush(Colors.Transparent))
                    .strokeThickness(0.)
                    .padding(Thickness(18., 6., 18., 6.))
                    .centerHorizontal()

            let progressBar =
                Border(
                    BoxView(color = teal400)
                        .cornerRadius(3.)
                        .width(260. * progressValue)
                        .height(5.)
                        .horizontalOptions(LayoutOptions.Start)
                )
                    .background(gray050Brush<Msg>())
                    .strokeShape(RoundRectangle(cornerRadius = 3.))
                    .stroke(SolidColorBrush(Colors.Transparent))
                    .strokeThickness(0.)
                    .width(260.)
                    .height(5.)
                    .centerHorizontal()

            let statusLabel =
                Label(m.Status)
                    .font(size = 13.)
                    .textColor(textSecond)
                    .centerTextHorizontal()
                    .margin(Thickness(24., 0., 24., 0.))

            let cancelButton =
                Border(
                    Button(buttonCancel, CancelDownload dt)
                        .background(SolidColorBrush(Colors.Transparent))
                        .textColor(red600)
                        .font(size = 13.)
                        .borderWidth(0.)
                )
                    .background(red050Brush<Msg>())
                    .stroke(red100Brush<Msg>())
                    .strokeShape(RoundRectangle(cornerRadius = 10.))
                    .strokeThickness(0.5)
                    .padding(Thickness(12., 2., 12., 2.))
                    .centerHorizontal()
                    .isVisible(isInProgress)

            let progressCircleView =
                GraphicsView(runIO <| progressCircle progressValue)
                    .height(130.)
                    .width(130.)
                    .centerHorizontal()

            (VStack(spacing = 0.) {

                let connText =
                    match m.Connectivity with
                    | Connected msg    -> msg
                    | Disconnected msg -> msg

                topBar connText labelOdis subtitle
                ContentView(
                    (VStack(spacing = 18.) {
                        progressCircleView
                        percentPill
                        progressBar
                        statusLabel
                        cancelButton
                    })
                        .centerVertical()
                        .padding(Thickness(20., 32., 20., 20.))
                )
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.))

        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  3 – Completed
        // ══════════════════════════════════════════════════════════════════════════
     
        let completedView (m : Model) msg =

            let connText, result =
                match m.Connectivity with
                | Connected msg    
                    ->                     
                    msg, 
                        (resultCircle teal050 "🚋") //(resultCircle teal050 "✓")
                            .centerHorizontal()
                | Disconnected msg 
                    -> 
                    msg, 
                        (resultCircle teal050 "🚋") //(resultCircle red050 "!")
                            .centerHorizontal()

            let labelFinished = 
                Label(labelOperationResult)
                        .font(size = 16.)
                        .textColor(textPrimary)
                        .centerTextHorizontal()  

            let labelMsg = 
                 Label(msg)
                     .font(size = 13.)
                     .textColor(textSecond)
                     .centerTextHorizontal()
                     .margin(Thickness(24., 0., 24., 0.))
     
            (VStack(spacing = 0.) {                
     
                topBar connText labelOdis m.Status
                
                (VStack(spacing = 18.) {
                    result
                    labelFinished
                    labelMsg
                    homeButton
                })
                    .centerVertical()
                    .padding(Thickness(20., 40., 20., 20.))
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.))

        let completedUtilitiesView (m : Model) msg =
        
            let connText, result =
                match m.Connectivity with
                | Connected msg    
                    ->                     
                    msg, 
                        (resultCircle teal050 "🚋") //(resultCircle teal050 "✓")
                            .centerHorizontal()
                | Disconnected msg 
                    -> 
                    msg, 
                        (resultCircle teal050 "🚋") //(resultCircle red050 "!")
                            .centerHorizontal()
        
            let labelFinished = 
                Label(labelOperationResult)
                        .font(size = 16.)
                        .textColor(textPrimary)
                        .centerTextHorizontal()  
        
            let labelMsg = 
                    Label(msg)
                        .font(size = 13.)
                        .textColor(textSecond)
                        .centerTextHorizontal()
                        .margin(Thickness(24., 0., 24., 0.))
             
            (VStack(spacing = 0.) {                
             
                topBar connText labelOdis m.Status
                        
                (VStack(spacing = 18.) {
                    result
                    labelFinished
                    labelMsg
                    backToUtilitiesButton
                })
                    .centerVertical()
                    .padding(Thickness(20., 40., 20., 20.))
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.))
     
        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  4 – Utilities
        // ══════════════════════════════════════════════════════════════════════════
     
        let utilitiesView (m : Model) =

            (VStack(spacing = 0.) {

                let connText =
                    match m.Connectivity with
                    | Connected msg    -> msg
                    | Disconnected msg -> msg
     
                topBar connText labelUtilities m.Status
                
                let sectionLabel1 = 
                    (sectionLabel labelKodisMismatch)
                     .margin(Thickness(18., 4., 18., 0.))
                
                let actionCardFileLauncher = 
                    actionCard
                        (iconBadge amber050 amber400 "📄")
                        RunFileLauncher
                        buttonLauncher
                        hintLauncher
                        |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 12.))

                let divider =       
                    divider ()
                    |> fun v -> v.margin(Thickness(18., 8., 18., 0.))
                                    
                let sectionLabel2 = 
                    (sectionLabel labelAccessDirectories)
                        .margin(Thickness(18., 4., 18., 0.))   

                let actionCardOpenStorage = 
                    actionCard
                        (iconBadge amber050 amber400 "📄")
                        (OpenStorageViewer String.Empty)
                        labelFileManager //buttonClearing
                        labelTimetableAccess   //hintClearing
                        |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 12.))     

                let sectionLabel3 = 
                    (sectionLabel labelClearing)
                        .margin(Thickness(18., 4., 18., 0.))     
                
                let actionCardClearing = 
                    actionCard
                        (iconBadge red050 red600 "🗑")
                        (Click Clear)
                        buttonClearing
                        hintClearing
                        |> fun (v : WidgetBuilder<Msg, IFabBorder>) -> v.margin(Thickness(18., 0., 18., 12.))                   

                ScrollView(
                    (VStack(spacing = 0.) {  
                        divider  
                        sectionLabel1     
                        actionCardFileLauncher                            
                        divider      
                        sectionLabel2   
                        actionCardOpenStorage
                        divider 
                        sectionLabel3
                        actionCardClearing    
                    })
                         .centerVertical()
                         .padding(Thickness(20., 32., 20., 20.))
                )
     
                Border(
                    Button(buttonHome, Navigate Home)
                        .background(SolidColorBrush(Colors.Transparent))
                        .textColor(teal800)
                        .font(size = 14.)
                        .borderWidth(0.)
                )
                    .background(teal050Brush<Msg>())
                    .stroke(teal100Brush<Msg>())
                    .strokeShape(RoundRectangle(cornerRadius = 10.))
                    .strokeThickness(0.5)
                    .padding(Thickness(0., 2., 0., 2.))
                    .margin(Thickness(18., 0., 18., 20.))
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.)) 

        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  5 – Clearing confirmation dialog
        // ══════════════════════════════════════════════════════════════════════════
     
        let clearingConfirmView (m : Model) =

            (VStack(spacing = 0.) {
                
                let connText =
                    match m.Connectivity with
                    | Connected msg    -> msg
                    | Disconnected msg -> msg

                topBar connText labelUtilities String.Empty
        
                // ── labels ───────────────────────────────
                let label1 =
                    Label("🗑")
                        .font(size = 28.)
                        .margin(Thickness(0., 0., 0., 12.))
        
                let label2 =
                    Label(labelClearingConfirmation)
                        .font(size = 16.)
                        .textColor(textPrimary)
                        .margin(Thickness(0., 0., 0., 8.))
        
                let label3 =
                    Label(labelClearingWarning)
                        .font(size = 13.)
                        .textColor(textSecond)
                        .margin(Thickness(0., 0., 0., 20.))

                let label4 =
                    Label(m.Status)
                        .font(size = 13.)
                        .textColor(textSecond)
                        .margin(Thickness(0., 0., 0., 20.))
        
                // ── buttons row ───────────────────────────
                let hsStack =
                    HStack(spacing = 10.) {
        
                        Border(
                            Button(buttonClearingConfirmation, Click ClearYes)
                                .background(SolidColorBrush(Colors.Transparent))
                                .textColor(m.IsClearing |> function true -> gray400 | false -> Colors.White)
                                .font(size = 13.)
                                .borderWidth(0.)
                                .isEnabled(not m.IsClearing)
                        )
                            .background(red400Brush<Msg>())
                            .strokeShape(RoundRectangle(cornerRadius = 8.))
                            .stroke(SolidColorBrush(Colors.Transparent))
                            .strokeThickness(0.)
                            .horizontalOptions(LayoutOptions.Fill)
        
                        Border(
                            Button(buttonClearingCancel, Click ClearNot)
                                .background(SolidColorBrush(Colors.Transparent))
                                .textColor(m.IsClearing |> function true -> gray400 | false -> textPrimary)
                                .font(size = 13.)
                                .borderWidth(0.)
                                .isEnabled(not m.IsClearing)
                        )
                            .background(gray050Brush<Msg>())
                            .stroke(brush cardBorder)
                            .strokeShape(RoundRectangle(cornerRadius = 8.))
                            .strokeThickness(0.5)
                            .horizontalOptions(LayoutOptions.Fill)
                    }
        
                // ── modal card ────────────────────────────
                let card =
                    Border(
                        VStack(spacing = 0.) {
                            label1
                            label2
                            label3
                            label4
                            hsStack
                        }
                    )
                        .padding(Thickness(20., 24., 20., 20.))
                        .background(cardBgBrush<Msg>())
                        .stroke(brush cardBorder)
                        .strokeShape(RoundRectangle(cornerRadius = 16.))
                        .strokeThickness(0.5)
                        .margin(Thickness(18., 0., 18., 0.))
        
                // ── final layout ──────────────────────────
                ContentView(card)
                    .padding(Thickness(0., 40., 0., 0.))
                    
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.)) 

        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  6 – No connection
        // ══════════════════════════════════════════════════════════════════════════
            
        let noConnectionView (m : Model) =

            (VStack(spacing = 0.) {

                let connText =
                    match m.Connectivity with
                    | Connected msg    -> msg
                    | Disconnected msg -> msg
            
                topBar connText labelOdis labelOdisExpl

                let resultCircle = 
                    (resultCircle red050 "✕")
                        .centerHorizontal()

                let label1 = 
                    Label(m.Status)
                        .font(size = 16.)
                        .textColor(textPrimary)
                        .centerTextHorizontal()

                let label2 =   
                    Label(String.Empty) //Label(noNetConn4)
                        .font(size = 13.)
                        .textColor(textSecond)
                        .centerTextHorizontal()
                        .margin(Thickness(24., 0., 24., 0.))
            
                (VStack(spacing = 18.) {
                    resultCircle
                    label1
                    label2     
                    homeButton
                })
                    .centerVertical()
                    .padding(Thickness(20., 40., 20., 20.))
            
                //quitButton 
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.))
            
        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  7 – Error
        // ══════════════════════════════════════════════════════════════════════════
            
        let errorView (m : Model) (err : string) =
        
            let icon =
                (resultCircle red050 "!")
                    .centerHorizontal()
        
            let titleLabel =
                Label(labelDesparatesness)
                    .font(size = 16.)
                    .textColor(textPrimary)
                    .centerTextHorizontal()
        
            let errorBox =
                Border(
                    Label(err)
                        .font(size = 12.) 
                        .textColor(red800)
                        .margin(Thickness(12.))
                )
                    .background(red050Brush<Msg>())
                    .stroke(red100Brush<Msg>())
                    .strokeShape(RoundRectangle(cornerRadius = 8.))
                    .strokeThickness(0.5)
                    .margin(Thickness(18., 0., 18., 0.))            
        
            let innerStack =
                (VStack(spacing = 18.) {
                    icon
                    titleLabel
                    errorBox
                    homeButton
                })
                    .centerVertical()
                    .padding(Thickness(20., 40., 20., 20.))
        
            (VStack(spacing = 0.) {

                let connText =
                    match m.Connectivity with
                    | Connected msg    -> msg
                    | Disconnected msg -> msg

                topBar connText labelOdis m.Status
                innerStack
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.))
            
        // ══════════════════════════════════════════════════════════════════════════
        //  SCREEN  8 – No permission  (Android only)
        // ══════════════════════════════════════════════════════════════════════════
            
        let noPermissionView (m : Model) =
        
            let icon =
                (resultCircle amber050 "🔒")
                    .centerHorizontal()
        
            let titleLabel =
                Label(m.Status)
                    .font(size = 16.)
                    .textColor(textPrimary)
                    .centerTextHorizontal()
                    .margin(Thickness(24., 0., 24., 0.))
        
            #if ANDROID
            let permissionButton =
                Border(
                    Button(buttonRequestPermission, RequestPermission)
                        .background(SolidColorBrush(Colors.Transparent))
                        .textColor(blue800)
                        .font(size = 14.)
                        .borderWidth(0.)
                )
                    .background(blue050Brush<Msg>())
                    .stroke(brush (Color.FromArgb("#FFB5D4F4")))
                    .strokeShape(RoundRectangle(cornerRadius = 10.))
                    .strokeThickness(0.5)
                    .padding(Thickness(20., 2., 20., 2.))
                    .centerHorizontal()
            #endif
        
            let innerStack =
                (VStack(spacing = 18.) {
                    icon
                    titleLabel
                    #if ANDROID
                    permissionButton
                    #endif
                })
                    .centerVertical()
                    .padding(Thickness(20., 40., 20., 20.))
        
            (VStack(spacing = 0.) {

                let connText =
                    match m.Connectivity with
                    | Connected msg    -> msg
                    | Disconnected msg -> msg

                topBar connText labelOdis String.Empty
                innerStack
            })
                .centerVertical()
                .padding(Thickness(20., 32., 20., 20.))

        // ══════════════════════════════════════════════════════════════════════════
        //  PAGE ROUTER
        // ══════════════════════════════════════════════════════════════════════════

        let pageContent =
            match m.Screen with
            | Home                   -> homeView               m
            | Utilities              -> utilitiesView          m
            | ClearingConfirm        -> clearingConfirmView    m
            | Downloading (dt, ps)   -> downloadingView        m dt ps
            | Completed msg          -> completedView          m msg
            | CompletedUtilities msg -> completedUtilitiesView m msg
            | NoConnection           -> noConnectionView       m
            | NoPermission           -> noPermissionView       m
            | ErrorScreen err        -> errorView              m err

        Application(
            ContentPage(
                ScrollView(pageContent)
            )
                .background(pageBgBrush<Msg>())
        )

    (*        
    Fabulous / Elmish World
        ↕
    MAUI World  

    MAUI lifecycle events fire completely outside the Elmish/Fabulous world.
    Lifecycle events -> OnResume, OnStart, OnSleep,...
    *)

    type internal DispatchHolder = 
        static member val DispatchRef : System.WeakReference<Dispatch<Msg>> option = None with get, set

    let captureDispatchSub (_ : Model) : Cmd<Msg> =
        Cmd.ofSub (fun dispatch -> DispatchHolder.DispatchRef <- Some (WeakReference<Dispatch<Msg>>(dispatch)))
  
    let program : Program<unit, Model, Msg, IFabApplication> = 
        Program.statefulWithCmd init update view 
        |> Program.withSubscription connectivityDebouncerSubscription
        |> Program.withSubscription captureDispatchSub