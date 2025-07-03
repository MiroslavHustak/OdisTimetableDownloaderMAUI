(*
Code in this file uses Fabulous, a functional-first UI framework.

https://fabulous.dev
https://github.com/fabulous-dev/Fabulous

Copyright 2016-2023 Timothée Larivoir, Edgar Gonzales, and contributors

Licensed under the Apache License, Version 2.0 (the "License")
*)

namespace OdisTimetableDownloaderMAUI

open System
open System.Threading

open FSharp.Control

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
open Types.Haskell_IO_Monad_Simulation

open Counters

open Api.Logging

open IO_Operations.IO_Operations

open Helpers
open Helpers.Builders
open Helpers.Connectivity

#if ANDROID
open Helpers.AndroidUIHelpers    
#endif

open ApplicationDesign.WebScraping_DPO
open ApplicationDesign.WebScraping_MDPO
open ApplicationDesign.WebScraping_KODISFMRecord
open ApplicationDesign4.WebScraping_KODISFMRecord4

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

module App =   

    type ProgressIndicator = 
        | Idle 
        | InProgress of float * float

    type Model = 
        {
            PermissionGranted : bool
            ProgressMsg : string
            NetConnMsg : string
            CloudProgressMsg : string
            ProgressIndicator : ProgressIndicator
            Progress : float 
            KodisVisible : bool
            DpoVisible : bool
            MdpoVisible : bool
            RestartVisible : bool
            ProgressCircleVisible : bool
            CancelVisible : bool
            CloudVisible : bool
            LabelVisible : bool
            Label2Visible : bool      
        }

    type Msg =
        | RequestPermission
        | PermissionResult of bool
        | Kodis  
        | Kodis4  
        | Dpo
        | Mdpo
        | Cancel
        | Home
        | RestartVisible of bool
        | Quit
        | QuitCountdown of string
        | NetConnMessage of string
        | IterationMessage of string    
        | UpdateStatus of float * float * bool 
        | WorkIsComplete of string * bool  

    let private cancellationActor =  //tady nelze IO Monad (pak se actor nespusti tak, jak je treba)

        //If no timeout or cancellation token is applied or the mailbox is not disposed (all three cases are under my control),
        //the mailbox will not raise an exception on its own. 
       
        MailboxProcessor<CancellationMessage>
            .StartImmediate
                <|
                fun inbox 
                    ->
                    let rec loop (cancelIsRequested : bool) (cts : CancellationTokenSource) =

                        async
                            {
                                match! inbox.Receive() with
                                | UpdateState2 (newState, newCts)
                                    ->
                                    match newState with
                                    | true  ->
                                            cts.Cancel()
                                            cts.Dispose()
                                    | false -> 
                                            () 
    
                                    return! loop newState newCts
    
                                | CheckState2 replyChannel 
                                    ->
                                    let ctsTokenOpt =                        
                                        try
                                            cts.Token |> Option.ofNull
                                        with
                                        | _ -> None                           
                     
                                    replyChannel.Reply ctsTokenOpt 

                                    return! loop cancelIsRequested cts
                            }
    
                    loop false (new CancellationTokenSource()) //whatever to initialise    
                      
    let init () =  
    
        let ensureMainDirectoriesExist = ensureMainDirectoriesExist ()
            
        let ctsInitial = new CancellationTokenSource()
            in
            cancellationActor.Post <| UpdateState2 (false, ctsInitial) //inicializace
                
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
                                            #if WINDOWS  
                                            match isConnected with
                                            | true  ->
                                                    () 
                                            | false -> 
                                                    NetConnMessage >> dispatch <| noNetConn 
                                                    do! Async.Sleep 2000
                                                    runIO <| countDown2 QuitCountdown RestartVisible NetConnMessage Quit dispatch
                                            #else                                            
                                            match isConnected with
                                            | true  -> NetConnMessage >> dispatch <| yesNetConn 
                                            | false -> NetConnMessage >> dispatch <| noNetConn                                                   
                                            #endif
                                           
                                        }
                                    |> Async.StartImmediate //nelze Async.Start 
                                
                            do! Async.Sleep 5000   
                        }
                )
            |> Async.StartImmediate  

        #if ANDROID        
        let permissionGranted = permissionCheck >> runIO >> Async.RunSynchronously <| ()  //available API employed by permissionCheck is async-only

        #else
        let permissionGranted = true
        #endif        
             
        let initialModel = 
            {         
                PermissionGranted = permissionGranted
                ProgressMsg = permissionGranted |> function true -> String.Empty | false -> appInfoInvoker
                NetConnMsg = String.Empty
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = permissionGranted
                DpoVisible = permissionGranted
                MdpoVisible = permissionGranted
                ProgressCircleVisible = false
                RestartVisible = false
                CancelVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
            } 

        let initialModelNoConn = 
            {       
                PermissionGranted = true //aby se button RequestPermission nezobrazoval
                ProgressMsg = String.Empty
                NetConnMsg = noNetConnInitial
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = false
                DpoVisible = false
                MdpoVisible = false
                ProgressCircleVisible = false
                RestartVisible = false
                CancelVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
            } 
            
        match runIO <| ensureMainDirectoriesExist with
        | Ok _ 
            -> 
            try          
                pyramidOfDoom   
                    {
                        //Po zruseni kodu zbylo jednoradkove CE, zatim ponechavam 
                        let! _ = connectivityListener >> runIO >> Option.ofBool <| (), (initialModelNoConn, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch))

                        return initialModel, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch)
                    }  
            with
            | ex 
                ->
                runIO (postToLog <| ex.Message <| "#1")
                { initialModel with ProgressMsg = ctsMsg }, Cmd.none

        | Error err 
            ->  
            runIO (postToLog <| err <| "#2")
            { initialModel with ProgressMsg = ctsMsg2; NetConnMsg = ctsMsg }, Cmd.none  

    let init2 () = 

        let ctsNew = new CancellationTokenSource()
            in
            cancellationActor.Post <| UpdateState2 (false, ctsNew)
        
        #if ANDROID
        let permissionGranted = permissionCheck >> runIO >> Async.RunSynchronously <| ()  //available API employed by permissionCheck is async-only        
        #else
        let permissionGranted = true
        #endif       
        
        let initialModel = 
            {       
                PermissionGranted = permissionGranted
                ProgressMsg = String.Empty
                NetConnMsg = String.Empty
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = permissionGranted
                DpoVisible = permissionGranted
                MdpoVisible = permissionGranted
                ProgressCircleVisible = false
                RestartVisible = false
                CancelVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
            } 

        let initialModelNoConn = 
            {    
                PermissionGranted = true  //aby se button RequestPermission nezobrazoval
                ProgressMsg = String.Empty
                NetConnMsg = noNetConnInitial
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = false
                DpoVisible = false
                MdpoVisible = false
                ProgressCircleVisible = false
                RestartVisible = false
                CancelVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
            }     
       
        try          
            pyramidOfDoom  
                {
                    //Po zruseni kodu zbylo jednoradkove CE, zatim ponechavam 
                    let! _ = connectivityListener >> runIO >> Option.ofBool <| (), (initialModelNoConn, Cmd.none)

                    return (initialModel, Cmd.none)
                }  
        with
        | ex 
            ->
            runIO (postToLog <| ex.Message <| "#3")
            { initialModel with ProgressMsg = ctsMsg }, Cmd.none

    let update msg m =

        match msg with   
        | RequestPermission 
            ->
            #if ANDROID

            let permissionStatus = 
                async
                    {
                        let! status = Permissions.CheckStatusAsync<Permissions.StorageRead>() |> Async.AwaitTask
                        return status = PermissionStatus.Granted
                    }
                |> Async.RunSynchronously //available API employed by status is async-only

            let cmd =
                Cmd.ofMsg
                    (                        
                        match permissionStatus with
                        | true  
                            ->                                         
                            PermissionResult true
                        | false
                            -> 
                            //PermissionGranted je tady false
                            openAppSettings >> runIO <| ()
                            PermissionResult true  //po settings nastavime PermissionGranted na true
                    )

            m, cmd
            #else
            m, Cmd.none
            #endif

        | PermissionResult granted 
            ->
            { m with PermissionGranted = granted }, Cmd.none

        | UpdateStatus (progressValue, totalProgress, isVisible)
            ->
            let progress =                 
                let value = (1.0 / totalProgress) * progressValue   
                 
                match value >= 1.000 with
                | true  -> 1.000
                | false -> value
            { 
                m with 
                    ProgressIndicator = InProgress (progressValue, totalProgress)
                    Progress = progress 
                    KodisVisible = false
                    ProgressCircleVisible = isVisible
                    CancelVisible = isVisible
                    DpoVisible = false
                    MdpoVisible = false
                    RestartVisible = false
            }, 
            Cmd.none

        | WorkIsComplete (result, isVisible)
            ->
            {
                m with                    
                    ProgressMsg = result
                    NetConnMsg = String.Empty
                    ProgressIndicator = Idle
                    Progress = 0.0
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    ProgressCircleVisible = false
                    CancelVisible = false
                    RestartVisible = isVisible
            }, 
            Cmd.none
                   
        | QuitCountdown message
            ->
            {
                m with                    
                    NetConnMsg = message
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

        | Quit  
            -> 
            #if WINDOWS
            runIO <| Api.Logging.saveJsonToFile () |> ignore<Result<unit, string>>
            #endif
            let message = HardRestart.exitApp >> runIO <| () 
            { m with ProgressMsg = message }, Cmd.none

        | Home  
            -> 
            init2 ()

        | Cancel 
            ->
            let ctsNew = new CancellationTokenSource() 
                in
                cancellationActor.Post <| UpdateState2 (true, ctsNew)    
           
            { m with ProgressMsg = cancelMsg3; ProgressCircleVisible = false; CancelVisible = false }, Cmd.none

        | RestartVisible isVisible 
            -> 
            { m with RestartVisible = isVisible; CancelVisible = not isVisible }, Cmd.none                   
             
        | NetConnMessage message
            ->
            { m with NetConnMsg = message; LabelVisible = true; Label2Visible = true }, Cmd.none           
             
        | Kodis 
            ->  
            //DeviceDisplay.KeepScreenOn <- true //throws an exception

            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn true
            #endif

            cancellationActor.PostAndReply <| fun replyChannel -> CheckState2 replyChannel
            |> function
                | Some token 
                    ->                     
                    let delayedCmd1 (token : CancellationToken) (dispatch : Msg -> unit) =                                

                        async
                            {
                                let! hardWork =                                                              
                                    async 
                                        {                                            
                                            let reportProgress (progressValue, totalProgress) =     

                                                match token.IsCancellationRequested with
                                                | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true) 
                                                | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)

                                            let result = 
                                                stateReducerCmd1
                                                <| token
                                                <| kodisPathTemp
                                                <| reportProgress 

                                            return runIO result                                                     
                                        }
                                    |> Async.StartChild

                                let! result = hardWork 
                                do! Async.Sleep 1000

                                match token.IsCancellationRequested with
                                | false ->
                                        match result with
                                        | Ok result -> WorkIsComplete >> dispatch <| (result, false)  
                                        | Error err -> WorkIsComplete >> dispatch <| (err, false)     
                                | true  ->
                                        WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                        dispatch Home                                    
                            }  

                    let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  

                        async 
                            {   
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
                                                <| fun message -> WorkIsComplete >> dispatch <| (message, false)
                                                <| fun message -> IterationMessage >> dispatch <| message 
                                                <| reportProgress            
                                       
                                            return runIO result
                                        }
                                    |> Async.StartChild 
                               
                                let! result = hardWork 
                                do! Async.Sleep 1000
                          
                                match token.IsCancellationRequested with
                                | false ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                        dispatch Home       
                            }     

                    let executeSequentially dispatch =
                    
                        async 
                            {   
                                RestartVisible >> dispatch <| false

                                do! delayedCmd1 token dispatch 
                                
                                match token.IsCancellationRequested with 
                                | true  -> ()
                                | false -> do! delayedCmd2 token dispatch
                            }
                        |> Async.StartImmediate

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgKodis 
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false  
                                ProgressCircleVisible = false
                        }, 
                        Cmd.none 
                                   
                | None      
                    -> 
                    m, Cmd.none  

        | Kodis4  
            ->
            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn true
            #endif

            cancellationActor.PostAndReply <| fun replyChannel -> CheckState2 replyChannel
            |> function
                | Some token 
                    ->
                    //delayedCmd1 nebude, neb json se zde nestahuje

                    let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =    

                        async 
                            {     
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
                                                <| fun message -> WorkIsComplete >> dispatch <| (message, false)
                                                <| fun message -> IterationMessage >> dispatch <| message 
                                                <| reportProgress      

                                            return runIO result  
                                        }
                                    |> Async.StartChild 

                                let! result = hardWork 
                                do! Async.Sleep 1000 

                                match token.IsCancellationRequested with
                                | false ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                        dispatch Home  
                            }  
                   
                    let executeSequentially dispatch =   

                        async 
                            {  
                               RestartVisible >> dispatch <| false

                               do! delayedCmd2 token dispatch                            
                            }
                        |> Async.StartImmediate  

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgKodis1 
                                NetConnMsg = yesNetConn
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false  
                                ProgressCircleVisible = false
                        }, 
                        Cmd.none 

                | None        
                    -> 
                    m, Cmd.none   
          
        | Dpo 
            -> 
            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn true
            #endif

            cancellationActor.PostAndReply <| fun replyChannel -> CheckState2 replyChannel
            |> function
                | Some token 
                    ->
                    let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                        async
                            {
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
                                do! Async.Sleep 1000
                              
                                match token.IsCancellationRequested with
                                | false ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ()) 
                                        dispatch Home         
                            }  
                     
                    let execute dispatch = 

                        async 
                            {     
                                RestartVisible >> dispatch <| false

                                match token.IsCancellationRequested with
                                | true  ->
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        | false -> do! delayedCmd token dispatch  
                            } 
                        |> Async.StartImmediate

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgDpo //progressMsgMdpo
                                NetConnMsg = String.Empty
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false 
                                ProgressCircleVisible = false
                        }, 
                        Cmd.none  

                | None  
                    -> 
                    m, Cmd.none

        | Mdpo //pridano network_security_config.xml, ale zda se, ze to nepomohlo
            ->   
            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn true
            #endif

            cancellationActor.PostAndReply <| fun replyChannel -> CheckState2 replyChannel
            |> function
                | Some token 
                    ->             
                    let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                        async
                            {
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
                                                return mauiMdpoMsg

                                            | Error err 
                                                ->
                                                RestartVisible >> dispatch <| true
                                                return err
                                        }
                                    |> Async.StartChild 
                               
                                let! result = hardWork 
                                do! Async.Sleep 1000
                           
                                match token.IsCancellationRequested with
                                | false ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ()) 
                                        dispatch Home  
                            }  
                     
                    let execute dispatch = 

                        async 
                            {         
                                RestartVisible >> dispatch <| false

                                match token.IsCancellationRequested with
                                | true  ->
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        | false -> do! delayedCmd token dispatch  
                            } 
                        |> Async.StartImmediate

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgMdpo
                                NetConnMsg = String.Empty
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false  
                                ProgressCircleVisible = false
                        }, 
                        Cmd.none  

                | None  
                    -> 
                    m, Cmd.none

    let view (m : Model) =
    
        Application(
            ContentPage(
                ScrollView(
                    (VStack(spacing = 25.) 
                        {       
                            // Progress circle
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

                            (VStack(spacing = 15.) //zatim nepouzivano
                                {    
                                    Border(ContentView
                                                (
                                                    HStack(spacing = 10.)
                                                        {
                                                            Label(m.CloudProgressMsg)
                                                                .font(size = 14.)
                                                                .centerTextHorizontal()
                                                                .padding(5.)
                                                                .height(30.)
                                                                .width(200.)
                                                                .horizontalOptions(LayoutOptions.Start)

                                                            Button("x", Home)
                                                                .font(size = 20., attributes = FontAttributes.Bold)
                                                                .padding(2.5,-5.5,2.5,2.5)
                                                                .width(25.)
                                                                .height(25.)
                                                                .cornerRadius(2) 
                                                                .horizontalOptions(LayoutOptions.End)
                                                        }
                                                ) 
                                          )
                                              .stroke(SolidColorBrush(Colors.Gray)) // Border color
                                              .strokeShape(RoundRectangle(cornerRadius = 15.))  // Rounded corners
                                              .background(SolidColorBrush(Colors.Gainsboro))  
                                              .strokeThickness(1.)
                                              .padding(5.)   
                                }
                            )
                                .width(250.)
                                .centerVertical()
                                .isVisible(m.CloudVisible)

                            #if ANDROID
                            Button(buttonRequestPermission, RequestPermission)
                                    .centerHorizontal()
                                    .semantics(hint = "Grant permission to access storage")
                                    .padding(10.)
                                    .isVisible(not m.PermissionGranted)
                            #endif
                                                             
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

                            Button(buttonHome, Home)
                                .semantics(hint = String.Empty)
                                .isVisible(m.RestartVisible && m.PermissionGranted)
                                .centerHorizontal()

                            Button(buttonQuit, Quit)
                                .semantics(hint = String.Empty)
                                .centerHorizontal()
                        }
                    )
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
    
    let program = Program.statefulWithCmd init update view