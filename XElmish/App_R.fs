(*
Code in this file uses Fabulous, a functional-first UI framework.

https://fabulous.dev
https://github.com/fabulous-dev/Fabulous

Copyright 2016-2023 Timothée Larivoir, Edgar Gonzales, and contributors

Licensed under the Apache License, Version 2.0 (the "License")
*)

// NOTE:
// UI/UX is intentionally minimal (a single view) and serves backend stress testing only.
// Final UX/UI/FE will be designed and implemented later by a UX/UI/FE professional.

// NOTE:
// Hints for UX/UI/FE professionals not familiar with Elmish (or Elm) are at the foot of this code.

namespace OdisTimetableDownloaderMAUI

open System
open System.Threading

open FSharp.Control

open FsToolkit.ErrorHandling

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Devices
open Microsoft.Maui.Storage
open Microsoft.Maui.Controls
open Microsoft.Maui.Graphics
open Microsoft.Maui.Networking
open Microsoft.Maui.Primitives
open Microsoft.Maui.Accessibility

#if ANDROID
open Xamarin
open Xamarin.Essentials  
#endif

open type Fabulous.Maui.View

//**********************************

open ProgressCircle

open Settings.Messages
open Settings.SettingsGeneral

open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Counters
open ActorModels
open Api.Logging
open IO_Operations.IO_Operations

open Helpers
open Helpers.Connectivity
open Helpers.ExceptionHelpers

#if ANDROID
open AndroidUIHelpers    
#endif

open ApplicationDesign.WebScraping_DPO
open ApplicationDesign.WebScraping_MDPO
open ApplicationDesign.WebScraping_KODIS
open ApplicationDesign4.WebScraping_KODIS4

(*     
    AndroidManifest.xml : Remember to review and update it if necessary. 
    OdisTimetableDownloaderMAUI.fsproj : Remember to review and update it if necessary.  
*)

(*
   do *.fsproj pridat:
   <PropertyGroup Condition="$(TargetPlatformIdentifier) == 'windows'">
	  <DefineConstants>WINDOWS</DefineConstants>
   </PropertyGroup> 

   //zkouska, jestli to nahodou nepomoze s problemem s www.mdpo.cz - zatim to vypada, ze ne
   <AndroidResource Include="Platforms\Android\Resources\xml\network_security_config.xml" />
*)

module App_R =  

    type ProgressIndicator = 
        | Idle 
        | InProgress of float * float

    type Model = // This is exactly how NOT to do it for real UI/UX/FE
        {
            PermissionGranted : bool
            ProgressMsg : string
            NetConnMsg : string
            CloudProgressMsg : string
            ProgressIndicator : ProgressIndicator
            Progress : float
            RestartVisible : bool
            ClearingVisible : bool
            KodisVisible : bool
            DpoVisible : bool
            MdpoVisible : bool
            BackHomeVisible : bool
            ProgressCircleVisible : bool
            CancelVisible : bool
            CloudVisible : bool
            LabelVisible : bool
            Label2Visible : bool
            InternetIsConnected : bool
            AnimatedButton : string option // Tracks animated button
        }

    type Msg = // This is exactly how NOT to do it for real UI/UX/FE
        | RequestPermission
        | Launch
        | DataClearing
        | DataClearingMessage of string
        | AllowDataClearing
        | CancelDataClearing
        | Kodis  
        | Kodis4  
        | Dpo
        | Mdpo
        | Cancel
        | Home
        | Home2
        | RestartVisible of bool
        | CancelVisible of bool
        | Quit
        | EmergencyQuit of bool
        | IntermediateQuitCase
        | QuitCountdown of string
        | NetConnMessage of string
        | IterationMessage of string    
        | UpdateStatus of float * float * bool 
        | WorkIsComplete of string * bool  
        | ClickClearingConfirmation
        | ClickClearingCancel
        | ClickRestart
        | ClickClearing  
        | ClearAnimation
        | CanopyDifferenceResult of Result<unit, string>  //For educational purposes only

    let private kodisActor = localCancellationActor()
    let private kodis4Actor = localCancellationActor()        
    let private dpoActor = localCancellationActor()
    let private mdpoActor = localCancellationActor()
    
    let init () =  
     
        let monitorConnectivity (dispatch : Msg -> unit) = //obsahuje countDown2, nelze odsunout do Connectivity             
                   
            AsyncSeq.initInfinite (fun _ -> true)
            |> AsyncSeq.mapi (fun index _ -> index)    // index for educational purposes
            |> AsyncSeq.takeWhile ((=) true << (>=) 0) // indefinite sequence // ((=) true << fun index -> index >= 0) 
            |> AsyncSeq.iterAsync 
                (fun index 
                    ->        
                    async 
                        {                                
                            connectivityListener2 >> runIO 
                                <|
                                fun isConnected 
                                    ->
                                    async
                                        {   
                                            match isConnected with
                                            | true  ->
                                                    return ()
                                            | false -> 
                                                    NetConnMessage >> dispatch <| noNetConn 
                                                    do! Async.Sleep 2000
                                                    return runIO <| countDown2 QuitCountdown RestartVisible NetConnMessage Quit dispatch
                                        }
                                    |> Async.StartImmediate //nelze Async.Start 
                                
                            do! Async.Sleep 100   
                        }
                )
            |> Async.StartImmediate  
       
        #if ANDROID
        let permissionGranted = permissionCheck >> runIO >> Async.RunSynchronously <| ()  //available API employed by permissionCheck is async-only
        #else
        let permissionGranted = true
        #endif
        
        let ensureMainDirectoriesExist = ensureMainDirectoriesExist permissionGranted
             
        let initialModel = 
            {         
                PermissionGranted = permissionGranted
                ProgressMsg = permissionGranted |> function true -> String.Empty | false -> appInfoInvoker
                NetConnMsg = String.Empty
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0   
                RestartVisible = false
                ClearingVisible = permissionGranted
                KodisVisible = permissionGranted
                DpoVisible = permissionGranted
                MdpoVisible = permissionGranted
                ProgressCircleVisible = false
                BackHomeVisible = false
                CancelVisible = false
                CloudVisible = false
                LabelVisible = true
                Label2Visible = true
                InternetIsConnected = true
                AnimatedButton = None
            } 

        let initialModelNoConn = 
            {       
                initialModel with
                    PermissionGranted = true
                    //ProgressMsg = String.Empty
                    NetConnMsg = noNetConnInitial
                    ClearingVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    AnimatedButton = None
            } 
            
        match runIO <| ensureMainDirectoriesExist with 
        | Ok _ 
            -> 
            try  
                runIO <| connectivityListener () //jen si zvykam na monadic composition / ROP-style function composition, pattern matching ma samozrejme o 1 radek mene :-)
                |> Option.ofBool 
                |> Option.map (fun _ -> initialModel, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch))
                |> Option.defaultWith (fun _ -> initialModelNoConn, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch))              
            with
            | ex 
                ->
                #if WINDOWS
                runIO (postToLog <| string ex.Message <| "#3-1") 
                #endif
                { initialModel with NetConnMsg = ctsMsg }, Cmd.none

        | Error err 
            ->  
            // match connectivityListener >> runIO <| () with true -> () | false ->  runIO (postToLog <| err <| "#002")  
            
            match initialModel.PermissionGranted with
            | true  -> { initialModel with ProgressMsg = ctsMsg2 }, Cmd.none  
            | false -> { initialModel with ProgressMsg = appInfoInvoker }, Cmd.none 

    let init2 isConnected = 
    
        #if ANDROID
        let permissionGranted = permissionCheck >> runIO >> Async.RunSynchronously <| ()  //available API employed by permissionCheck is async-only
        #else
        let permissionGranted = true
        #endif

        let ensureMainDirectoriesExist = ensureMainDirectoriesExist permissionGranted
        
        let initialModel = 
            {       
                PermissionGranted = permissionGranted
                ProgressMsg = String.Empty
                NetConnMsg = isConnected |> function true -> String.Empty | false -> noNetConn3
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                RestartVisible = false
                ClearingVisible = isConnected |> function true -> permissionGranted | false -> false
                KodisVisible = isConnected |> function true -> permissionGranted | false -> false
                DpoVisible = isConnected |> function true -> permissionGranted | false -> false
                MdpoVisible = isConnected |> function true -> permissionGranted | false -> false
                ProgressCircleVisible = false
                BackHomeVisible = false
                CancelVisible = false
                CloudVisible = false
                LabelVisible = true
                Label2Visible = true
                InternetIsConnected = isConnected
                AnimatedButton = None
            } 

        let initialModelNoConn = 
            {    
                initialModel with
                    PermissionGranted = true
                    //ProgressMsg = String.Empty
                    NetConnMsg = noNetConnInitial
                    ClearingVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    AnimatedButton = None
                    InternetIsConnected = false
            }   
            
        match runIO <| ensureMainDirectoriesExist with
        | Ok _ 
            -> 
            try        
                runIO <| connectivityListener () //jen si zvykam na monadic composition / ROP-style function composition, pattern matching ma samozrejme o 1 radek mene :-)
                |> Option.ofBool 
                |> Option.map (fun _ -> initialModel, Cmd.none)
                |> Option.defaultWith (fun _ -> initialModelNoConn, Cmd.none)   
            with
            | ex 
                ->
                #if WINDOWS
                runIO (postToLog <| string ex.Message <| "#1") 
                #endif
                { initialModel with NetConnMsg = ctsMsg }, Cmd.none

        | Error err 
            ->  
            match connectivityListener >> runIO <| () with true -> runIO (postToLog <| err <| "#003") | false -> ()
            { initialModel with ProgressMsg = ctsMsg2 }, Cmd.none            

    let update msg m =

        let cmdOnClickAnimation msg = 

            Cmd.batch
                [
                    Cmd.ofAsyncMsg
                        (
                            async
                                {
                                    do! Async.Sleep 300
                                    return ClearAnimation
                                }
                        )
                    Cmd.ofSub (fun dispatch -> dispatch msg)
                ]            

        match msg with   
        | ClickClearingConfirmation
            ->
            { m with AnimatedButton = Some buttonClearingConfirmation }, cmdOnClickAnimation AllowDataClearing            

        | ClickClearingCancel 
            ->
            { m with AnimatedButton = Some buttonClearingCancel }, cmdOnClickAnimation CancelDataClearing
           
        | ClickRestart 
            ->
            { m with AnimatedButton = Some buttonRestart }, cmdOnClickAnimation Home2            
                   
        | ClickClearing 
            ->
            { m with AnimatedButton = Some buttonClearing }, cmdOnClickAnimation DataClearing          
           
        | ClearAnimation 
            ->
            { m with AnimatedButton = None }, Cmd.none  
            
        | RequestPermission ->
            #if ANDROID
            let cmd : Cmd<Msg> =
                Cmd.ofSub 
                    (fun dispatch 
                        ->
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
                                    | false -> 
                                            // Already has permission → we can just refresh
                                            dispatch Home2
        
                                    | true  ->
                                            do openAppSettings >> runIO <| ()
        
                                            // Give Android time to process the permission change
                                            do! Async.Sleep 1000
        
                                            // Re-check after returning from settings
                                            let! newStatus = 
                                                Permissions.CheckStatusAsync<Permissions.StorageRead>() 
                                                |> Async.AwaitTask
        
        
                                            match newStatus = PermissionStatus.Granted with
                                            | true  -> return dispatch Home2
                                            | false -> return ()   
        
                                with 
                                | _ -> return ()
                            }
                        |> Async.StartImmediate
                )        
            m, cmd        
            #else
            m, Cmd.none
            #endif

        | UpdateStatus (progressValue, totalProgress, isVisible)
            ->
            let progress =                 
                let value = (1.0 / totalProgress) * progressValue   
                 
                match value >= 1.000 with
                | true  -> 1.000
                | false -> value
            { 
                m with 
                    ProgressIndicator = m.InternetIsConnected |> function true -> InProgress (progressValue, totalProgress) | false -> Idle
                    Progress = m.InternetIsConnected |> function true -> progress | false -> 0.0   
                    NetConnMsg = m.InternetIsConnected |> function true -> String.Empty | false -> noNetConn3
                    ClearingVisible = false
                    KodisVisible = false
                    ProgressCircleVisible = isVisible
                    CancelVisible = m.InternetIsConnected |> function true -> isVisible | false -> false 
                    DpoVisible = false
                    MdpoVisible = false
                    BackHomeVisible = false
            }, 
            Cmd.none

        | WorkIsComplete (result, isVisible)
            ->
            {
                m with                    
                    ProgressMsg = result
                    NetConnMsg = m.InternetIsConnected |> function true -> String.Empty | false -> noNetConn3
                    ProgressIndicator = Idle
                    Progress = 0.0
                    ClearingVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    ProgressCircleVisible = false
                    CancelVisible = false
                    BackHomeVisible = isVisible
            }, Cmd.none
                   
        | QuitCountdown message
            ->
            {
                m with                    
                    NetConnMsg = message
                    ClearingVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                    ProgressCircleVisible = false
                    CancelVisible = false
                    LabelVisible = true
                    Label2Visible = true
            }, 
            Cmd.none  
                 
        | IterationMessage message 
            ->
            { m with ProgressMsg = message }, Cmd.none   

        | DataClearingMessage message 
            ->
            { 
                m with 
                    ProgressMsg = message
                    ClearingVisible = false
                    KodisVisible = true
                    DpoVisible = true
                    MdpoVisible = true  
                    CloudVisible = false
                    LabelVisible = true
                    Label2Visible = true
            },
            Cmd.none   

        | Quit  
            ->              
            #if WINDOWS 
            (*
            let cmd () : Cmd<Msg> =
                async 
                    {
                        let! _ = runIO (Api.Logging.saveJsonToFileAsync ())
                        return <Msg> -> nechce se mi zavadet Dummy Msg
                    }
                |> Cmd.ofAsyncMsg                    
            *) 
            let cmd () : Cmd<Msg> =
                Cmd.ofSub 
                    (fun _ 
                        ->
                        async 
                            {
                                let! _ = runIO (Api.Logging.saveJsonToFileAsync ())
                                return ()
                            }
                        |> Async.StartImmediate
                    )

            kodisActor.PostAndReply(fun reply -> Stop reply)
            kodis4Actor.PostAndReply(fun reply -> Stop reply)
            mdpoActor.PostAndReply(fun reply -> Stop reply)
            dpoActor.PostAndReply(fun reply -> Stop reply)
            
            let message = HardRestart.exitApp >> runIO <| () 
            { m with ProgressMsg = message }, cmd ()
            #endif

            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn false  
            
            kodisActor.PostAndReply(fun reply -> Stop reply)
            kodis4Actor.PostAndReply(fun reply -> Stop reply)
            mdpoActor.PostAndReply(fun reply -> Stop reply)
            dpoActor.PostAndReply(fun reply -> Stop reply)

            let message = HardRestart.exitApp >> runIO <| () 

            { 
                m with ProgressMsg = message
            },
            Cmd.none
            #endif

        | IntermediateQuitCase 
            -> 
            m, Cmd.ofMsg Quit

        | EmergencyQuit isConnected
            ->
            let delayedQuit (dispatch : Msg -> unit) = 
                async
                    {
                        do! Async.Sleep 300000 //za dany cas mu to vypneme automaticky // 5 minut
                        //do! Async.AwaitTask (System.Threading.Tasks.Task.Delay(System.Threading.Timeout.InfiniteTimeSpan))                        
                        return dispatch IntermediateQuitCase
                    }
            
            let executeQuit (dispatch : Msg -> unit) = 
                async
                    {
                        return! delayedQuit dispatch
                    }
                |> Async.StartImmediate
          
            #if WINDOWS
            m, Cmd.none          
            #endif

            #if ANDROID
            { 
                m with 
                    ProgressMsg = String.Empty
                    NetConnMsg = noNetConn4
                    ProgressIndicator = Idle
                    Progress = 0.0   
                    RestartVisible = false
                    ClearingVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    CloudVisible = false  
                    ProgressCircleVisible = false
                    BackHomeVisible = false
                    CancelVisible = false
                    LabelVisible = true
                    Label2Visible = true  
                    InternetIsConnected = isConnected          
            }, Cmd.ofSub executeQuit
            #endif           

        | Home2  
            -> 
            init2 m.InternetIsConnected

        | Home  
            -> 
            init ()

        | Cancel 
            ->   
            [
                kodisActor
                kodis4Actor
                mdpoActor
                dpoActor
            ]
            |> List.iter cancelLocalActor
          
            { m with 
                ProgressMsg = cancelMsg3
                NetConnMsg = String.Empty
                ProgressCircleVisible = false
                CancelVisible = false
                BackHomeVisible = false
            }, Cmd.none

        | RestartVisible isVisible 
            -> 
            { m with BackHomeVisible = isVisible; CancelVisible = not isVisible }, Cmd.none      
            
        | CancelVisible isVisible 
            -> 
            { m with CancelVisible = isVisible }, Cmd.none                   
             
        | NetConnMessage message
            ->
            { m with NetConnMsg = message; LabelVisible = true; Label2Visible = true }, Cmd.none  
            
        | Launch 
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
                                | true  -> return NetConnMessage String.Empty 
                                | false -> return NetConnMessage launchErrorMsg                        
                            } 
                        |> Cmd.ofAsyncMsg
                           
                    | None
                        -> 
                        NetConnMessage >> Cmd.ofMsg <| launchErrorMsg  
                with
                |_ -> NetConnMessage >> Cmd.ofMsg <| launchErrorMsg   
            
            m, cmd 

        | DataClearing
            ->  
            { 
                m with
                    RestartVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    ClearingVisible = true
                    CloudVisible = true
                    LabelVisible = true
                    Label2Visible = true  
            }, Cmd.none 
            
        | AllowDataClearing 
            ->
            let delayedCmd (dispatch : Msg -> unit) : Async<unit> =

                async
                    {
                        try
                            let! results =
                                [
                                    async { return deleteOld >> runIO <| () }
                                    async { return deleteOld4 >> runIO <| () }
                                ]
                                |> Async.Parallel
                                |> Async.Catch
                            
                            results
                            |> Result.ofChoice
                            |> function
                                | Ok [| _; _ |] -> DataClearingMessage >> dispatch <| deleteOldTimetablesMsg2
                                | Ok _          -> DataClearingMessage >> dispatch <| deleteOldTimetablesMsg3
                                | Error _       -> DataClearingMessage >> dispatch <| deleteOldTimetablesMsg3        
            
                            return! Async.Sleep 1000

                        with 
                        | ex
                            ->
                            runIO (postToLog <| string ex.Message <| " #XElmish_ClearData")
                            DataClearingMessage >> dispatch <| deleteOldTimetablesMsg3
                    }
                     
            let execute dispatch = async { return! delayedCmd dispatch } |> Async.StartImmediate         

            { 
                m with
                    ProgressMsg = deleteOldTimetablesMsg1  
                    ClearingVisible = false
                    CloudVisible = false
                    LabelVisible = true
                    Label2Visible = true
            }, 
            Cmd.ofSub execute

        | CancelDataClearing
            ->
            { 
                m with   
                    ProgressMsg = String.Empty
                    ClearingVisible = true
                    KodisVisible = true
                    DpoVisible = true
                    MdpoVisible = true  
                    CloudVisible = false
                    LabelVisible = true
                    Label2Visible = true
            }, 
            Cmd.none

        | CanopyDifferenceResult result // For educational purposes only
            ->
            match result with
            | Ok _ 
                -> 
                m, Cmd.none
            | Error err 
                ->  
                { m with ProgressMsg = err }, Cmd.none
             
        | Kodis 
            ->  
            kodisActor.PostAndReply(fun reply -> GetToken reply) 
            |> function
                | Some token 
                    ->                     
                    let delayedCmd1 (token : CancellationToken) (dispatch : Msg -> unit) =                                

                        async
                            {
                                try
                                    let! hardWork =                                                              
                                        async 
                                            {                                            
                                                let reportProgress (progressValue, totalProgress) =     

                                                    match token.IsCancellationRequested with
                                                    | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true) 
                                                    | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)

                                                return runIO (stateReducerCmd1 token reportProgress)                                                      
                                            }
                                        |> Async.StartChild

                                    let! result = hardWork 
                                    //do! Async.Sleep 1000

                                    match token.IsCancellationRequested with
                                    | false ->
                                            match result with
                                            | Ok result -> return WorkIsComplete >> dispatch <| (result, false)  
                                            | Error err -> return WorkIsComplete >> dispatch <| (err, false)     
                                    | true  ->
                                            WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                            return dispatch Home2   
                                with 
                                | ex
                                    ->
                                    match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                    | err when err = StopDownloading 
                                        ->
                                        dispatch Home2   
                                    | _ ->
                                        runIO (postToLog <| string ex.Message <| " #XElmish_Kodis_Critical_Error_Json")
                                        NetConnMessage >> dispatch <| criticalElmishErrorKodisJson
                            }  

                    let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  

                        async 
                            {   
                                try
                                    let! hardWork =                             
                                        async 
                                            {   
                                                let reportProgress (progressValue, totalProgress) =     

                                                    match token.IsCancellationRequested with
                                                    | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true) 
                                                    | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)

                                                let result = 
                                                    stateReducerCmd2 
                                                    <| token
                                                    <| kodisPathTemp
                                                    <| fun isVisible -> CancelVisible >> dispatch <| isVisible   
                                                    <| fun message -> WorkIsComplete >> dispatch <| (message, false)
                                                    <| fun message -> IterationMessage >> dispatch <| message 
                                                    <| reportProgress            
                                       
                                                return runIO result
                                            }
                                        |> Async.StartChild 
                               
                                    let! result = hardWork 
                                    //do! Async.Sleep 1000
                          
                                    match token.IsCancellationRequested with
                                    | false ->
                                            return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                    | true  ->
                                            WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                            return dispatch Home2  
                                with 
                                | ex
                                    ->
                                    match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                    | err when err = StopDownloading 
                                        ->
                                        dispatch Home2   
                                    | _ ->
                                        runIO (postToLog <| string ex.Message <| " #XElmish_Kodis_Critical_Error")
                                        NetConnMessage >> dispatch <| criticalElmishErrorKodis
                            }     

                    let executeSequentially dispatch =
                        async 
                            {   
                                do! delayedCmd1 token dispatch 
                                
                                match token.IsCancellationRequested with 
                                | true  -> return ()
                                | false -> return! delayedCmd2 token dispatch
                            }
                        |> Async.StartImmediate                   

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgKodis 
                                NetConnMsg = m.InternetIsConnected |> function true -> String.Empty | false -> noNetConn3
                                ProgressIndicator = m.InternetIsConnected |> function true -> InProgress (0.0, 0.0) | false -> Idle  
                                ClearingVisible = false
                                CloudVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                ProgressCircleVisible = false
                        }, 
                        Cmd.ofSub executeSequentially  
                   
                    | None  
                        ->
                        { 
                            m with                               
                                ProgressMsg = String.Empty
                                NetConnMsg = noNetConn4
                                ProgressIndicator = Idle
                                Progress = 0.0   
                                RestartVisible = false
                                ClearingVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                CloudVisible = false  
                                ProgressCircleVisible = false
                                BackHomeVisible = false
                                CancelVisible = false
                                LabelVisible = true
                                Label2Visible = true   
                                InternetIsConnected = false
                        }, 
                        Cmd.none 
                                   
                | None      
                    -> 
                    m, Cmd.none  

        | Kodis4  
            ->
            kodis4Actor.PostAndReply(fun reply -> GetToken reply)
            |> function
                | Some token 
                    ->
                    let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =    

                        async 
                            {    
                                try
                                    let! hardWork =                             
                                        async 
                                            {                                                      
                                                let reportProgress (progressValue, totalProgress) =     

                                                    match token.IsCancellationRequested with
                                                    | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true) 
                                                    | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)

                                                let result = 
                                                    stateReducerCmd4
                                                    <| token
                                                    <| kodisPathTemp4
                                                    <| fun isVisible -> CancelVisible >> dispatch <| isVisible    
                                                    <| fun isVisible -> RestartVisible >> dispatch <| isVisible
                                                    <| fun message -> WorkIsComplete >> dispatch <| (message, false)
                                                    <| fun message -> IterationMessage >> dispatch <| message 
                                                    <| reportProgress      

                                                return runIO result  
                                            }
                                        |> Async.StartChild 

                                    let! result = hardWork 
                                    //do! Async.Sleep 1000 

                                    match token.IsCancellationRequested with
                                    | false ->
                                            return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                    | true  ->
                                            WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                            return dispatch Home2  
                                with 
                                | ex
                                    ->
                                    match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                    | err when err = StopDownloading 
                                        ->
                                        dispatch Home2   
                                    | _ ->
                                        runIO (postToLog <| string ex.Message <| " #XElmish_Kodis4_Critical_Error")
                                        NetConnMessage >> dispatch <| criticalElmishErrorKodis4
                            }  

                    let delayedCmd5 (dispatch : Msg -> unit) : Async<unit> =    
                    
                        async 
                            {    
                                try
                                    match! stateReducerCmd5 >> runIO <| () with
                                    | Ok _      -> return NetConnMessage >> dispatch <| String.Empty
                                    | Error err -> return NetConnMessage >> dispatch <| err
                                    
                                with 
                                | ex
                                    ->
                                    runIO (postToLog <| string ex.Message <| " #XElmish_Kodis4_Critical_Error")
                                    NetConnMessage >> dispatch <| criticalElmishErrorKodis4
                            }  
                   
                    let executeSequentially dispatch = 
                        async 
                            {          
                                //RestartVisible >> dispatch <| false
                                do! delayedCmd2 token dispatch
                                return! delayedCmd5 dispatch                           
                            }
                        |> Async.StartImmediate                     

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgKodis1 
                                NetConnMsg = m.InternetIsConnected |> function true -> String.Empty | false -> noNetConn3
                                ProgressIndicator = m.InternetIsConnected |> function true -> InProgress (0.0, 0.0) | false -> Idle  
                                ClearingVisible = false
                                CloudVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                ProgressCircleVisible = false
                        },
                        Cmd.ofSub executeSequentially  
                   
                    | None  
                        ->
                        { 
                            m with                               
                                ProgressMsg = String.Empty
                                NetConnMsg = noNetConn4
                                ProgressIndicator = Idle
                                Progress = 0.0   
                                RestartVisible = false
                                ClearingVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                CloudVisible = false  
                                ProgressCircleVisible = false
                                BackHomeVisible = false
                                CancelVisible = false
                                LabelVisible = true
                                Label2Visible = true   
                                InternetIsConnected = false
                        }, 
                        Cmd.none 

                | None        
                    -> 
                    m, Cmd.none   
          
        | Dpo 
            -> 
            dpoActor.PostAndReply(fun reply -> GetToken reply)
            |> function
                | Some token 
                    ->           
                    let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                        async
                            {
                                try
                                    NetConnMessage >> dispatch <| String.Empty 
                                                          
                                    let! hardWork =                            
                                        async 
                                            {
                                                let reportProgress (progressValue, totalProgress) =     

                                                    match token.IsCancellationRequested with
                                                    | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true) 
                                                    | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)    

                                                match runIO <| webscraping_DPO reportProgress token dpoPathTemp with  
                                                | Ok _     
                                                    -> 
                                                    return mauiDpoMsg

                                                | Error err 
                                                    ->
                                                    RestartVisible >> dispatch <| true
                                                    return err
                                            }
                                        |> Async.StartChild 
                               
                                    let! result = hardWork 
                                    //do! Async.Sleep 1000
                              
                                    match token.IsCancellationRequested with
                                    | false ->
                                            return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                    | true  ->
                                            WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ()) 
                                            return dispatch Home2                                          
                                with 
                                | ex
                                    ->
                                    match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                    | err when err = StopDownloading 
                                        ->
                                        dispatch Home2   
                                    | _ ->
                                        runIO (postToLog <| string ex.Message <| " #XElmish_Dpo_Critical_Error")
                                        NetConnMessage >> dispatch <| criticalElmishErrorDpo
                            }  
                     
                    let execute dispatch = 

                        async 
                            {   
                                RestartVisible >> dispatch <| false

                                match token.IsCancellationRequested with
                                | true  ->
                                        return UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  -> return UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        | false -> return! delayedCmd token dispatch                               
                            } 
                        |> Async.StartImmediate

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgDpo //progressMsgMdpo
                                NetConnMsg = m.InternetIsConnected |> function true -> String.Empty | false -> noNetConn3
                                ProgressIndicator = m.InternetIsConnected |> function true -> InProgress (0.0, 0.0) | false -> Idle  
                                ClearingVisible = false
                                CloudVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                ProgressCircleVisible = false
                        }, 
                        Cmd.ofSub execute 
                   
                    | None  
                        ->
                        { 
                            m with                               
                                ProgressMsg = String.Empty
                                NetConnMsg = noNetConn4
                                ProgressIndicator = Idle
                                Progress = 0.0   
                                RestartVisible = false
                                ClearingVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                CloudVisible = false  
                                ProgressCircleVisible = false
                                BackHomeVisible = false
                                CancelVisible = false
                                LabelVisible = true
                                Label2Visible = true  
                                InternetIsConnected = false
                        }, 
                        Cmd.none  

                | None  
                    -> 
                    m, Cmd.none

        | Mdpo //pridano network_security_config.xml, ale zda se, ze to nepomohlo
            ->  
            mdpoActor.PostAndReply(fun reply -> GetToken reply)
            |> function
                | Some token 
                    ->             
                    let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                        async
                            {
                                try
                                    NetConnMessage >> dispatch <| String.Empty 
                                                          
                                    let! hardWork =                            
                                        async 
                                            {
                                                let reportProgress (progressValue, totalProgress) =     

                                                    match token.IsCancellationRequested with
                                                    | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true) 
                                                    | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false) 

                                                match runIO <| webscraping_MDPO reportProgress token mdpoPathTemp with
                                                | Ok _     
                                                    ->                                                
                                                    return mauiMdpoMsg //Helpers.StringCombine.runTest() //mauiMdpoMsg

                                                | Error err 
                                                    ->
                                                    RestartVisible >> dispatch <| true
                                                    return err
                                            }
                                        |> Async.StartChild 
                               
                                    let! result = hardWork 
                                    //do! Async.Sleep 1000
                           
                                    match token.IsCancellationRequested with
                                    | false ->
                                            return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                    | true  ->
                                            WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ()) 
                                            return dispatch Home2  
                                with 
                                | ex
                                    ->
                                    match isCancellationGeneric StopDownloading TimeoutError FileDownloadError token ex with
                                    | err when err = StopDownloading 
                                        ->
                                        dispatch Home2                                                       
                                    | _ ->
                                        runIO (postToLog <| string ex.Message <| " #XElmish_Mdpo_Critical_Error")
                                        NetConnMessage >> dispatch <| criticalElmishErrorMdpo
                            }  
                  
                    let execute dispatch =

                        async 
                            {                                
                                RestartVisible >> dispatch <| false

                                match token.IsCancellationRequested with
                                | true  ->
                                        return UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  -> return UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        | false -> return! delayedCmd token dispatch 
                            }
                        |> Async.StartImmediate

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgMdpo
                                NetConnMsg = m.InternetIsConnected |> function true -> String.Empty | false -> noNetConn3
                                ProgressIndicator = m.InternetIsConnected |> function true -> InProgress (0.0, 0.0) | false -> Idle  
                                ClearingVisible = false
                                CloudVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                ProgressCircleVisible = false
                        }, 
                        Cmd.ofSub execute 
                   
                    | None  
                        ->
                        { 
                            m with                               
                                ProgressMsg = String.Empty
                                NetConnMsg = noNetConn4
                                ProgressIndicator = Idle
                                Progress = 0.0   
                                RestartVisible = false
                                ClearingVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                CloudVisible = false  
                                ProgressCircleVisible = false
                                BackHomeVisible = false
                                CancelVisible = false
                                LabelVisible = true
                                Label2Visible = true    
                                InternetIsConnected = false
                        }, 
                        Cmd.none  

                | None  
                    -> 
                    m, Cmd.none

    let view (m: Model) =

        let animate buttonLabel animatedButton = 
            animatedButton 
            |> function 
                | Some label 
                   when label = buttonLabel
                    -> 1.2 
                | _ 
                    -> 1.0
                       
        Application(
            ContentPage(
                ScrollView(
                    (VStack(spacing = 25.)
                        {
                            GraphicsView(runIO <| progressCircle m.Progress)
                                .height(130.)
                                .width(130.)
                                .isVisible(not m.KodisVisible || m.ProgressCircleVisible)
    
                            Label(labelOdis)
                                .semantics(SemanticHeadingLevel.Level1)
                                .font(size = 24.)
                                .centerTextHorizontal()
    
                            Label(m.ProgressMsg)
                                .semantics(SemanticHeadingLevel.Level2, "Welcome to dot.net multi platform app UI powered by Fabulous")
                                .font(size = 14.)
                                .centerTextHorizontal()
                                .isVisible(m.LabelVisible)
    
                            Label(m.NetConnMsg)
                                .semantics(SemanticHeadingLevel.Level2, String.Empty)
                                .font(size = 14.)
                                .centerTextHorizontal()
                                .isVisible(m.Label2Visible)
    
                            (VStack(spacing = 15.)
                                {
                                    Border(ContentView
                                        (
                                            HStack(spacing = 10.)
                                                {
                                                    HStack(spacing = 12.) {
                                                        Button(buttonClearingConfirmation, ClickClearingConfirmation)
                                                            .font(size = 14., attributes = FontAttributes.None)
                                                            .padding(2.5, -5.5, 2.5, 2.5)
                                                            .cornerRadius(2)
                                                            .height(25.)
                                                            .background(SolidColorBrush(Colors.DarkRed))
                                                            .scaleX(animate buttonClearingConfirmation m.AnimatedButton)
                                                            .scaleY(animate buttonClearingConfirmation m.AnimatedButton)
    
                                                        Button(buttonClearingCancel, ClickClearingCancel)
                                                            .font(size = 14., attributes = FontAttributes.None)
                                                            .padding(2.5, -5.5, 2.5, 2.5)
                                                            .cornerRadius(2)
                                                            .height(25.)
                                                            .background(SolidColorBrush(Colors.DarkRed))
                                                            .scaleX(animate buttonClearingCancel m.AnimatedButton)
                                                            .scaleY(animate buttonClearingCancel m.AnimatedButton)
                                                    }
                                                }
                                        )
                                    )
                                        .stroke(SolidColorBrush(Colors.Gray))
                                        .strokeShape(RoundRectangle(cornerRadius = 5.))
                                        .background(SolidColorBrush(Colors.White))
                                        .strokeThickness(0.5)
                                        .padding(5.)
                                }
                            )
                                .centerHorizontal()
                                .centerVertical()
                                .isVisible(m.CloudVisible)
    
                            #if ANDROID
                            Button(buttonRestart, ClickRestart)
                                .centerHorizontal()
                                .semantics(hint = hintRestart)
                                .padding(10.)
                                .isVisible(m.PermissionGranted && m.RestartVisible)
                                .scaleX(animate buttonRestart m.AnimatedButton)
                                .scaleY(animate buttonRestart m.AnimatedButton)

                            Button(buttonRequestPermission, RequestPermission)
                                .centerHorizontal()
                                .semantics(hint = "Grant permission to access storage")
                                .padding(10.)
                                .isVisible(not m.PermissionGranted)                            
                            #endif

                            Button(buttonLauncher, Launch)
                                .semantics(hint = String.Empty)
                                .centerHorizontal()
                                .background(SolidColorBrush(Colors.YellowGreen))
                                .isVisible(m.KodisVisible && m.PermissionGranted)

                            Button(buttonClearing, ClickClearing)
                                .semantics(hint = hintClearing)
                                .centerHorizontal()
                                .isVisible(m.ClearingVisible && m.PermissionGranted && not m.ProgressCircleVisible)
                                .background(SolidColorBrush(Colors.DarkRed))
                                .scaleX(animate buttonClearing m.AnimatedButton)
                                .scaleY(animate buttonClearing m.AnimatedButton)

                            Button(buttonKodis, Kodis)
                                .semantics(hint = hintOdis)
                                .centerHorizontal()
                                .isVisible(m.KodisVisible && m.PermissionGranted)

                            Button(buttonKodis4, Kodis4)
                                .semantics(hint = hintOdis)
                                .centerHorizontal()
                                .isVisible(m.KodisVisible && m.PermissionGranted)

                            Button(buttonDpo, Dpo)
                                .semantics(hint = hintDpo)
                                .centerHorizontal()
                                .isVisible(m.DpoVisible && m.PermissionGranted)

                            Button(buttonMdpo, Mdpo)
                                .semantics(hint = hintMdpo)
                                .centerHorizontal()
                                .isVisible(m.MdpoVisible && m.PermissionGranted)

                            Button(buttonCancel, Cancel)
                                .semantics(hint = String.Empty)
                                .isVisible(m.CancelVisible && m.PermissionGranted)
                                .centerHorizontal()

                            Button(buttonHome, Home2)
                                .semantics(hint = String.Empty)
                                .isVisible(m.BackHomeVisible && m.PermissionGranted)
                                .centerHorizontal()

                            Button(buttonQuit, Quit)
                                .semantics(hint = String.Empty)
                                .centerHorizontal()
                                .background(SolidColorBrush(Colors.Green))
                        }
                    )
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
        
    (*        
    Fabulous / Elmish World
        ↕
    MAUI World
    *)

    // MAUI lifecycle events fire completely outside the Elmish/Fabulous world.
    // Lifecycle events -> OnResume, OnStart, OnSleep,...

    type internal DispatchHolder = 
        static member val DispatchRef : System.WeakReference<Dispatch<Msg>> option = None with get, set

    let internal captureDispatchSub (_ : Model) : Cmd<Msg> =

        Cmd.ofSub 
            (fun (dispatch : Dispatch<Msg>)
                ->
                DispatchHolder.DispatchRef <- Some <| System.WeakReference<Dispatch<Msg>>(dispatch)
            )
            
    let program : Program<unit, Model, Msg, IFabApplication> = 
    
        Program.statefulWithCmd init update view 
        |> Program.withSubscription captureDispatchSub
                
    (* 
        The professional FE code should include patterns similar to the following ones:

        type AppState =
        | Normal of NormalModel
        | EmergencyNoInternet of EmergencyModel

        type Screen =
        | Home
        | Clearing
        | Progress of ProgressState
        | NoConnection
        | Completed of string

        type ProgressState =
        | Idle
        | Running of current :float * total :float
        | Cancelling
        | Finished

        type ClearingState =
        | NotStarted
        | Confirming
        | Clearing
        | Cleared

        type Connectivity =
        | Connected
        | Disconnected

        type Model =
            {
                Permission : PermissionState
                Connectivity : Connectivity
                Screen : Screen
                Clearing : ClearingState
                Progress : ProgressState
                Message : string option
                AnimatedButton : ButtonId option
            }

        match model.Screen with
        | Home ->
            homeView()
            
        | Clearing ->
            clearingView()
            
        | Progress p ->
            progressView p
            
        | NoConnection ->
            noConnectionView()

    App/             
    ├─ MVU/
    │   ├─ Home.fs
    │   ├─ Clearing.fs
    │   ├─ Progress.fs
    │   └─ NoConnection.fs
    └─ Commands/
        ├─ Connectivity.fs
        ├─ Kodis.fs
        └─ Clearing.fs 
    *)