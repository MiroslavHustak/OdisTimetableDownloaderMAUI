(*
Code in this file uses Fabulous, a functional-first UI framework.

https://fabulous.dev
https://github.com/fabulous-dev/Fabulous

Copyright 2016-2023 Timothée Larivoir, Edgar Gonzales, and contributors

Licensed under the Apache License, Version 2.0 (the "License")
*)

namespace OdisTimetableDownloaderMAUI

open System
open System.IO
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
open Types.Haskell_IO_Monad_Simulation

open Counters
open Types.ErrorTypes

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
            AnimatedButton : string option // Tracks animated button
        }

    type Msg =
        | RequestPermission
        | PermissionResult of bool
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
        | Quit
        | QuitCountdown of string
        | NetConnMessage of string
        | IterationMessage of string    
        | UpdateStatus of float * float * bool 
        | WorkIsComplete of string * bool  
        | ClickClearingConfirmation
        | ClickClearingCancel
        | ClickRestart
        | ClickRequestPermission
        | ClickClearing  
        | ClearAnimation

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
                                                    return () 
                                            | false -> 
                                                    NetConnMessage >> dispatch <| noNetConn 
                                                    do! Async.Sleep 2000
                                                    return runIO <| countDown2 QuitCountdown RestartVisible NetConnMessage Quit dispatch
                                            #else                                            
                                            match isConnected with
                                            | true  -> return NetConnMessage >> dispatch <| yesNetConn 
                                            | false -> return NetConnMessage >> dispatch <| noNetConn                                                   
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
                AnimatedButton = None
            } 

        let initialModelNoConn = 
            {       
                initialModel with
                    PermissionGranted = true
                    ProgressMsg = String.Empty
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
                runIO <| connectivityListener () //jen si zvykam na monadic function composition, pattern matching ma samozrejme o 1 radek mene :-)
                |> Option.ofBool 
                |> Option.map (fun _ -> initialModel, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch))
                |> Option.defaultWith (fun _ -> initialModelNoConn, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch))              
            with
            | ex 
                ->
                #if WINDOWS
                runIO (postToLog <| ex.Message <| "#3-1") 
                #endif
                { initialModel with NetConnMsg = ctsMsg }, Cmd.none

        | Error err 
            ->  
            // match connectivityListener >> runIO <| () with true -> () | false ->  runIO (postToLog <| err <| "#002")  
            
            match initialModel.PermissionGranted with
            | true  -> { initialModel with ProgressMsg = ctsMsg2 }, Cmd.none  
            | false -> { initialModel with ProgressMsg = appInfoInvoker }, Cmd.none 

    let init2 () = 

        let ctsNew = new CancellationTokenSource()
            in
            cancellationActor.Post <| UpdateState2 (false, ctsNew)
        
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
                AnimatedButton = None
            } 

        let initialModelNoConn = 
            {    
                initialModel with
                    PermissionGranted = true
                    ProgressMsg = String.Empty
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
                runIO <| connectivityListener () //jen si zvykam na monadic function composition, pattern matching ma samozrejme o 1 radek mene :-)
                |> Option.ofBool 
                |> Option.map (fun _ -> initialModel, Cmd.none)
                |> Option.defaultWith (fun _ -> initialModelNoConn, Cmd.none)   
            with
            | ex 
                ->
                #if WINDOWS
                runIO (postToLog <| ex.Message <| "#1") 
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
            { m with AnimatedButton = Some buttonRestart }, cmdOnClickAnimation Home
            
        | ClickRequestPermission
            ->
            { m with AnimatedButton = Some buttonRequestPermission }, cmdOnClickAnimation RequestPermission
           
        | ClickClearing 
            ->
            { m with AnimatedButton = Some buttonClearing }, cmdOnClickAnimation DataClearing          
           
        | ClearAnimation 
            ->
            { m with AnimatedButton = None }, Cmd.none  
            
        | RequestPermission
            ->
            #if ANDROID
            let cmd : Cmd<Msg> =
                Cmd.ofSub
                    (fun _
                        ->                        
                        async   
                            {
                                try
                                    let! status =
                                        Permissions.CheckStatusAsync<Permissions.StorageRead>() |> Async.AwaitTask
                                                                            
                                    match Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.R with
                                    | true  -> Android.OS.Environment.IsExternalStorageManager
                                    | false -> status = PermissionStatus.Granted
        
                                    |> function
                                        | true  -> ()
                                        | false -> openAppSettings >> runIO <| ()

                                    return ()

                                with 
                                | _ -> return ()                                
                            }
                        |> Async.StartImmediate
                    )
            m, cmd
            #else
            m, Cmd.none
            #endif

        | PermissionResult granted 
            ->
            { m with PermissionGranted = granted; RestartVisible = true }, Cmd.none

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
                    ClearingVisible = false
                    KodisVisible = false
                    ProgressCircleVisible = isVisible
                    CancelVisible = isVisible
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
                    NetConnMsg = String.Empty
                    ProgressIndicator = Idle
                    Progress = 0.0
                    ClearingVisible = false
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    ProgressCircleVisible = false
                    CancelVisible = false
                    BackHomeVisible = isVisible
            }, 
            Cmd.none
                   
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
            { m with ProgressMsg = message; ClearingVisible = true }, Cmd.none   

        | Quit  
            -> 
            #if WINDOWS
            runIO <| Api.Logging.saveJsonToFile () |> ignore<Result<unit, string>>
            #endif

            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn false
            #endif

            let message = HardRestart.exitApp >> runIO <| () 
            { m with ProgressMsg = message }, Cmd.none

        | Home2  
            -> 
            init2 ()

        | Home  
            -> 
            init ()

        | Cancel 
            ->
            let ctsNew = new CancellationTokenSource() 
                in
                cancellationActor.Post <| UpdateState2 (true, ctsNew)    
           
            { m with ProgressMsg = cancelMsg3; ProgressCircleVisible = false; CancelVisible = false }, Cmd.none

        | RestartVisible isVisible 
            -> 
            { m with BackHomeVisible = isVisible; CancelVisible = not isVisible }, Cmd.none                   
             
        | NetConnMessage message
            ->
            { m with NetConnMsg = message; LabelVisible = true; Label2Visible = true }, Cmd.none    

        | DataClearing
            ->  
            { 
                m with
                    ClearingVisible = true
                    CloudVisible = true
                    LabelVisible = false
                    Label2Visible = false  
                    ProgressCircleVisible = false
            }, Cmd.none 
            
        | AllowDataClearing 
            ->
            let delayedCmd (dispatch : Msg -> unit) : Async<unit> =

                async
                    {                                                                                 
                        let! hardWork =                            
                            async 
                                {
                                    [
                                        async { return deleteOld >> runIO <| () }
                                        async { return deleteOld4 >> runIO <| () }
                                    ]         
                                    |> Async.Parallel 
                                    |> Async.Catch
                                    |> Async.RunSynchronously //obecne neni dobre pouzivat Async.RunSynchronously uvnitr async, ale tady to jinak nefunguje
                                    |> Result.ofChoice                      
                                    |> function
                                        | Ok [|a; b|] -> DataClearingMessage >> dispatch <| deleteOldTimetablesMsg2
                                        | Ok _        -> DataClearingMessage >> dispatch <| deleteOldTimetablesMsg3
                                        | Error _     -> DataClearingMessage >> dispatch <| deleteOldTimetablesMsg3                                   
                                }
                            |> Async.StartChild 
                               
                        let! result = hardWork 
                        do! Async.Sleep 1000
                              
                        return result   
                    }  
                     
            let execute dispatch = async { return! delayedCmd dispatch } |> Async.StartImmediate         

            { 
                m with
                    ProgressMsg = deleteOldTimetablesMsg1
                    ClearingVisible = false
                    CloudVisible = false
                    LabelVisible = true
                    Label2Visible = true  
            }, Cmd.ofSub execute

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
             
        | Kodis 
            ->  
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

                                            return runIO (stateReducerCmd1 token reportProgress)                                                      
                                        }
                                    |> Async.StartChild

                                let! result = hardWork 
                                do! Async.Sleep 1000

                                match token.IsCancellationRequested with
                                | false ->
                                        match result with
                                        | Ok result -> return WorkIsComplete >> dispatch <| (result, false)  
                                        | Error err -> return WorkIsComplete >> dispatch <| (err, false)     
                                | true  ->
                                        WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                        return dispatch Home2                                    
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
                                        return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                        return dispatch Home2       
                            }     

                    let executeSequentially dispatch =
                    
                        async 
                            {   
                                RestartVisible >> dispatch <| false
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
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                ClearingVisible = false
                                CloudVisible = false
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
                                        return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (String.Empty, connectivityListener >> runIO <| ()) 
                                        return dispatch Home2  
                            }  
                   
                    let executeSequentially dispatch =   

                        async 
                            {  
                               RestartVisible >> dispatch <| false
                               return! delayedCmd2 token dispatch                            
                            }
                        |> Async.StartImmediate  

                    match connectivityListener >> runIO >> Option.ofBool <| () with
                    | Some _
                        ->             
                        { 
                            m with                               
                                ProgressMsg = progressMsgKodis1 
                                //NetConnMsg = yesNetConn
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                ClearingVisible = false
                                CloudVisible = false
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
                                        return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ()) 
                                        return dispatch Home2         
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
                                NetConnMsg = String.Empty
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                ClearingVisible = false
                                CloudVisible = false
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
                                                return mauiMdpoMsg //Helpers.StringCombine.runTest() //mauiMdpoMsg

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
                                        return WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ())    
                                | true  ->
                                        WorkIsComplete >> dispatch <| (result, connectivityListener >> runIO <| ()) 
                                        return dispatch Home2  
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
                                NetConnMsg = String.Empty
                                ProgressIndicator = InProgress (0.0, 0.0)
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
                                NetConnMsg = noNetConn1
                                ProgressIndicator = InProgress (0.0, 0.0)
                                ClearingVisible = false
                                CloudVisible = false
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false  
                                ProgressCircleVisible = false
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

                            Button(buttonRequestPermission, ClickRequestPermission)
                                .centerHorizontal()
                                .semantics(hint = "Grant permission to access storage")
                                .padding(10.)
                                .isVisible(not m.PermissionGranted)
                                .scaleX(animate buttonRequestPermission m.AnimatedButton)
                                .scaleY(animate buttonRequestPermission m.AnimatedButton)
                            #endif

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

    type DispatchHolder = static member val DispatchRef : System.WeakReference<Dispatch<Msg>> option = None with get, set

    let captureDispatchSub (_ : Model) : Cmd<Msg> =

        Cmd.ofSub 
            (fun (dispatch : Dispatch<Msg>)
                ->
                DispatchHolder.DispatchRef <- Some <| System.WeakReference<Dispatch<Msg>>(dispatch)
            )
    
    let program : Program<unit, Model, Msg, IFabApplication> = 
    
        Program.statefulWithCmd init update view 
        |> Program.withSubscription captureDispatchSub