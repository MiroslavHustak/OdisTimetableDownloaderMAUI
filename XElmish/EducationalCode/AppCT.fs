﻿namespace OdisTimetableDownloaderMAUI

open System
open System.Threading
open System.Net.NetworkInformation

open FSharp.Control

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Storage
open Microsoft.Maui.Controls
open Microsoft.Maui.Graphics
open Microsoft.Maui.Networking
open Microsoft.Maui.Primitives
open Microsoft.Maui.Accessibility

open type Fabulous.Maui.View

//**********************************

open ProgressCircle

open Settings.Messages
open Settings.SettingsGeneral

open Types.Types

open Helpers
open Helpers.Builders
open Helpers.CheckNetConnection

open ApplicationDesign.WebScraping_DPO
open ApplicationDesign.WebScraping_MDPO
open ApplicationDesign.WebScraping_KODISFMRecord
open ApplicationDesign4.WebScraping_KODISFMRecord4

(*     
    AndroidManifest.xml !!!!!!!!!!!!
*)

//Cancellation tokens only work under the Windows Machine mode.

module AppCT =

    //******************************** Potential helpers *******************************************

    let private xor2 a b = (a && not b) || (not a && b)   
    let private xor3 a b c = (a && not b && not c) || (not a && b && not c) || (not a && not b && c)

    //**********************************************************************************************

    type ProgressIndicator = 
        | Idle 
        | InProgress of float * float

    type Model = 
        {
            ProgressMsg : string
            NetConnMsg : string
            CloudProgressMsg : string
            ProgressIndicator : ProgressIndicator
            Progress : float 
            KodisVisible : bool
            DpoVisible : bool
            MdpoVisible : bool
            CloudVisible : bool
            LabelVisible : bool
            Label2Visible : bool
            RestartVisible : bool
            RestartButtonName : string 
            Cts : CancellationTokenSource
        }

    type Msg =
        | Kodis  
        | Kodis4  
        | Dpo
        | Mdpo
        | Cancel of string 
        | CancelIsComplete 
        | Restart  
        | Quit
        | RestartVisible2 of bool * bool
        | NetConnMessage of string
        | IterationMessage of string    
        | UpdateStatus of float * float * bool
        | WorkIsComplete of string 

    let actor = //actor model

        MailboxProcessor.StartImmediate(fun inbox ->

            let rec loop (isConnected : bool) = 
                async
                    {
                        match! inbox.Receive() with
                        | UpdateState newState
                            ->
                            return! loop newState

                        | CheckState replyChannel
                            ->                            
                            replyChannel.Reply(isConnected) 
                            return! loop isConnected
                    }
            
            loop false // Start the loop with whatever initial value
        )

    let connectivityListener () = //vysledek je bool
    
        let initialConnected = Connectivity.NetworkAccess = NetworkAccess.Internet
        actor.Post(UpdateState initialConnected) // prvotni inicializace mailboxu
    
        let connectivityChangedHandler (args : ConnectivityChangedEventArgs) =

            let isConnected = args.NetworkAccess = NetworkAccess.Internet  
            actor.Post(UpdateState isConnected)
    
        Connectivity.ConnectivityChanged.Add connectivityChangedHandler 
            
        actor.PostAndAsyncReply (fun replyChannel -> CheckState replyChannel)
        |> Async.RunSynchronously

    let connectivityListener2 onConnectivityChange = //vysledek je unit
        
        let connectivityChangedHandler (args : ConnectivityChangedEventArgs) =
        
            let isConnected = args.NetworkAccess = NetworkAccess.Internet
            onConnectivityChange isConnected
            
        Connectivity.ConnectivityChanged.Add connectivityChangedHandler
       
    let init () =    

        let monitorConnectivity (dispatch : Msg -> unit) (token : CancellationToken) =  

            AsyncSeq.initInfinite (fun _ -> true)
            |> AsyncSeq.mapi (fun index _ -> index) 
            |> AsyncSeq.takeWhile ((=) true << fun index -> index >= 0) // indefinite sequence
            |> AsyncSeq.iterAsync 
                (fun index 
                    ->        
                    async 
                        {    
                            connectivityListener2 
                                (fun isConnected 
                                    ->
                                    match isConnected |> Option.ofBool with
                                    | Some _
                                         when token.CanBeCanceled = true && token.IsCancellationRequested = false 
                                            ->                                           
                                            RestartVisible2 >> dispatch <| (false, false) 

                                    | Some _
                                         when token.IsCancellationRequested = true 
                                            ->                                           
                                            NetConnMessage >> dispatch <| yesNetConnPlus
                                            RestartVisible2 >> dispatch <| (true, false) 

                                    | Some _
                                            -> 
                                            NetConnMessage >> dispatch <| String.Empty

                                    | None
                                            -> 
                                            RestartVisible2 >> dispatch <| (false, false)
                                            Cancel >> dispatch <| cancelMsg1NoConn
                                            NetConnMessage >> dispatch <| noNetConnPlus
                                            RestartVisible2 >> dispatch <| (false, false)
                                ) 
                                
                            do! Async.Sleep 20     
                        }
                )
            |> Async.StartImmediate
        
        let initialModel = 
            {                 
                ProgressMsg = String.Empty
                NetConnMsg = String.Empty
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = true
                DpoVisible = true
                MdpoVisible = true
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
                RestartVisible = false
                RestartButtonName = buttonRestart 
                Cts = new CancellationTokenSource() //s tim nic tady nenarobim, pokud null, zrejme to vyhodi exn pro Cts.Token
            } 

        let initialModelNoConn = 
            {                 
                ProgressMsg = String.Empty
                NetConnMsg = noNetConnInitial
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = false
                DpoVisible = false
                MdpoVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
                RestartVisible = false
                RestartButtonName = buttonRestart
                Cts = new CancellationTokenSource() //s tim nic tady nenarobim, pokud null, zrejme to vyhodi exn pro Cts.Token
            }     
        
        try
            pyramidOfDoom
                {
                    let initialModelCtsToken =    
                        try
                            Some <| initialModel.Cts.Token               
                        with
                        | _ -> None    

                    let! initialModelCtsToken = initialModelCtsToken, ({ initialModel with RestartVisible = true; NetConnMsg = ctsMsg }, Cmd.none)
                    let! _ = connectivityListener () |> Option.ofBool, (initialModelNoConn, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch initialModelCtsToken))

                    return initialModel, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch initialModelCtsToken)
                }  
        with
        | _ -> { initialModel with ProgressMsg = ctsMsg }, Cmd.none

    let update msg m =

        match msg with      
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
                    DpoVisible = false
                    MdpoVisible = false
                    RestartVisible = false
            }, 
            Cmd.none

        | WorkIsComplete result
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
                    //RestartVisible = true
                    RestartButtonName = buttonRestart 
            }, 
            Cmd.none
             
        | Cancel message
            ->     
            let ctsCancel () =    
                try
                    try
                        Some <| m.Cts.Cancel() //This signal is irreversible once sent.
                    finally                      
                        m.Cts.Dispose() //Any code that has already received or responded to the cancellation won’t be affected by the disposal.
                with
                | _ -> None
           
            pyramidOfDoom
                {
                    let!_ = ctsCancel (), (m, Cmd.none)

                    // Create a new CancellationTokenSource for future use
                    let! newCts = new CancellationTokenSource() |> Option.ofNull, (m, Cmd.none) 
                                                      
                    let execute dispatch = //TODO verify whether this code is of any use here
                        async
                            {
                                WorkIsComplete >> dispatch <| message
                                RestartVisible2 >> dispatch <| (false, false)
                            }
                        |> Async.StartImmediate  

                    return  
                        { 
                            m with     
                                ProgressIndicator = InProgress (0.0, 0.0)
                                Progress = 0.0
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                RestartVisible = true
                                Cts = newCts  // Replace the old cts with a new one
                        },
                        Cmd.ofSub execute   
                }    

        | CancelIsComplete
            ->           
            {
                m with                    
                    ProgressMsg = cancelMsg2
                    ProgressIndicator = InProgress (0.0, 0.0)
                    Progress = 0.0
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    RestartVisible = true
                    RestartButtonName = buttonRestart 
                    Cts = new CancellationTokenSource()   
            }, 
            Cmd.none

        | IterationMessage message 
            ->
            { m with ProgressMsg = message }, Cmd.none   

        | Restart   
            -> 
            {                 
                ProgressMsg = String.Empty
                NetConnMsg = String.Empty
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = true
                DpoVisible = true
                MdpoVisible = true
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
                RestartVisible = false
                RestartButtonName = buttonRestart
                Cts = new CancellationTokenSource() //s tim nic tady nenarobim, pokud null, zrejme to vyhodi exn pro Cts.Token
            }, Cmd.none   

        | Quit  
            -> 
            HardRestart.exitApp () 
            m, Cmd.none
            
        | RestartVisible2 (isVisible1, isVisible2)  
            ->
            { m with  
                            
                KodisVisible = isVisible2
                DpoVisible = isVisible2
                MdpoVisible = isVisible2
                RestartVisible = isVisible1
            }, Cmd.none   
             
        | NetConnMessage message
            ->
            { m with NetConnMsg = message; LabelVisible = true; Label2Visible = true }, Cmd.none           
             
        | Kodis 
            ->
            match new CancellationTokenSource() |> Option.ofNull with
            | Some newCts 
                ->
                let path = kodisPathTemp 
                                                
                let delayedCmd1 (token : CancellationToken) (dispatch : Msg -> unit) =                                

                    async
                        {
                            //async tady ignoruje async code obsahujici Request.sendAsync request/result (FsHttp)   

                            let! hardWork =                                                              
                                async 
                                    {
                                        let reportProgress (progressValue, totalProgress) = 
                                            match token.IsCancellationRequested with
                                            | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                            | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true)                                           

                                        return 
                                            stateReducerCmd1
                                            <| token
                                            <| path
                                            <| reportProgress
                                    }
                                |> Async.StartChild

                            let! result = hardWork 
                            do! Async.Sleep 1000
                                
                            match token.IsCancellationRequested with
                            | true  -> 
                                    WorkIsComplete >> dispatch <| netConnError
                                    RestartVisible2 >> dispatch <| (true, false)
                            | false ->                
                                    match result with
                                    | Ok result -> 
                                                WorkIsComplete >> dispatch <| result  
                                                RestartVisible2 >> dispatch <| (false, true)
                                    | Error err -> 
                                                WorkIsComplete >> dispatch <| err 
                                                RestartVisible2 >> dispatch <| (true, false) 
                        }  

                let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  

                    async 
                        {   
                            let! hardWork =                             
                                async 
                                    {   
                                        let reportProgress (progressValue, totalProgress) = 
                                            match token.IsCancellationRequested with
                                            | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                            | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true)        
                                       
                                        return
                                            stateReducerCmd2 
                                            <| token
                                            <| path
                                            <| fun message -> WorkIsComplete >> dispatch <| message
                                            <| fun message -> IterationMessage >> dispatch <| message 
                                            <| reportProgress            
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000
                          
                            match token.IsCancellationRequested with
                            | false -> 
                                        WorkIsComplete >> dispatch <| result
                                        RestartVisible2 >> dispatch <| (false, true)
                            | true  -> 
                                        WorkIsComplete >> dispatch <| netConnError
                                        RestartVisible2 >> dispatch <| (true, false)
                        }     

                let executeSequentially dispatch =

                    async 
                        {                                         
                            let token =    
                                try
                                    Some <| newCts.Token                                                 
                                with
                                | _ -> None       
                         
                            match token with
                            | Some token 
                                -> 
                                RestartVisible2 >> dispatch <| (false, false) 

                                match token.IsCancellationRequested with
                                | true  -> 
                                        RestartVisible2 >> dispatch <| (false, false)  
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        dispatch CancelIsComplete   
                                | false ->
                                        RestartVisible2 >> dispatch <| (false, false) 
                                        do! delayedCmd1 token dispatch      
                                                      
                                        match token.IsCancellationRequested with
                                        | true  ->
                                                RestartVisible2 >> dispatch <| (false, false)  
                                                UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                                dispatch CancelIsComplete                                 
                                        | false ->                                                               
                                                match token.IsCancellationRequested with
                                                | true  ->
                                                        RestartVisible2 >> dispatch <| (false, false)   
                                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                                        dispatch CancelIsComplete               
                                                | false ->
                                                        RestartVisible2 >> dispatch <| (false, false)
                                                        do! delayedCmd2 token dispatch  
                                                        RestartVisible2 >> dispatch <| (true, false)
                               
                            | None      
                                -> 
                                ()    
                        }
                    |> Async.StartImmediate

                match connectivityListener () |> Option.ofBool with
                | Some _
                    ->             
                    { 
                        m with                               
                            ProgressMsg = progressMsgKodis 
                            NetConnMsg = String.Empty
                            ProgressIndicator = InProgress (0.0, 0.0)
                            KodisVisible = false
                            DpoVisible = false
                            MdpoVisible = false
                            Cts = newCts  // Update the cts
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
                            Cts = newCts  // Update the cts
                    }, 
                    Cmd.none      
                                   
            | None      
                -> 
                m, Cmd.none           

        | Kodis4 
            -> 
            match new CancellationTokenSource() |> Option.ofNull with
            | Some newCts
                ->   
                let path = kodisPathTemp4   
                            
                //delayedCmd1 nebude, neb json se zde nestahuje

                let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  

                    async 
                        {   
                            let! hardWork =                             
                                async 
                                    {   
                                        let reportProgress (progressValue, totalProgress) = 
                                            match token.IsCancellationRequested with
                                            | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                            | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true)        
                                       
                                        return
                                            stateReducerCmd4
                                            <| token
                                            <| path
                                            <| fun message -> WorkIsComplete >> dispatch <| message
                                            <| fun message -> IterationMessage >> dispatch <| message 
                                            <| reportProgress            
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000
                          
                            match token.IsCancellationRequested with
                            | false -> 
                                    WorkIsComplete >> dispatch <| result
                                    RestartVisible2 >> dispatch <| (false, true)
                            | true  -> 
                                    WorkIsComplete >> dispatch <| netConnError
                                    RestartVisible2 >> dispatch <| (true, false)
                        }     

                let executeSequentially dispatch =

                    async 
                        {                                         
                            let token =    
                                try
                                    Some <| newCts.Token                                                 
                                with
                                | _ -> None       
                         
                            match token with
                            | Some token 
                                -> 
                                RestartVisible2 >> dispatch <| (false, false)                                
                                                      
                                match token.IsCancellationRequested with
                                | true  ->
                                        RestartVisible2 >> dispatch <| (false, false)  
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        dispatch CancelIsComplete                                 
                                | false ->  
                                        do! Async.Sleep 4000
                                        
                                        match token.IsCancellationRequested with
                                        | true  ->
                                                RestartVisible2 >> dispatch <| (false, false)   
                                                UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                                dispatch CancelIsComplete               
                                        | false ->
                                                RestartVisible2 >> dispatch <| (false, false)
                                                do! delayedCmd2 token dispatch  
                                                RestartVisible2 >> dispatch <| (true, false)
                               
                            | None      
                                -> 
                                ()    
                        }
                    |> Async.StartImmediate  

                match connectivityListener () |> Option.ofBool with
                | Some _
                    ->             
                    { 
                        m with                               
                            ProgressMsg = progressMsgKodis1 
                            NetConnMsg = String.Empty
                            ProgressIndicator = InProgress (0.0, 0.0)
                            KodisVisible = false
                            DpoVisible = false
                            MdpoVisible = false
                            Cts = newCts  // Update the cts
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
                            Cts = newCts  // Update the cts
                    }, 
                    Cmd.none  

            | None        
                -> 
                m, Cmd.none   
          
        | Dpo 
            -> 
            match new CancellationTokenSource() |> Option.ofNull with
            | Some newCts
                ->
                let path = dpoPathTemp
                 
                let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                    async
                        {
                            NetConnMessage >> dispatch <| String.Empty 
                            RestartVisible2 >> dispatch <| (false, false)
                                                          
                            let! hardWork =                            
                                async 
                                    {
                                        let reportProgress (progressValue, totalProgress) = 
                                            match token.IsCancellationRequested with
                                            | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                            | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true)    

                                        match webscraping_DPO reportProgress token path with  //match webscraping_MDPO reportProgress token path with
                                        | Ok _      -> return mauiDpoMsg //mauiMdpoMsg
                                        | Error err -> return err
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000
                           
                            match token.IsCancellationRequested with
                            | false -> 
                                    WorkIsComplete >> dispatch <| result
                                    RestartVisible2 >> dispatch <| (false, true)
                            | true  -> 
                                    WorkIsComplete >> dispatch <| netConnError
                                    RestartVisible2 >> dispatch <| (true, false)
                        }  
                     
                let execute dispatch = 

                    async 
                        { 
                            let token =    
                                try
                                    Some <| newCts.Token 
                                with
                                | _ -> None  
                                
                            match token with
                            | Some token 
                                -> 
                                RestartVisible2 >> dispatch <| (false, false)   

                                match token.IsCancellationRequested with
                                | true  ->
                                        RestartVisible2 >> dispatch <| (false, false)  
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        dispatch CancelIsComplete                                 
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  ->
                                                RestartVisible2 >> dispatch <| (false, false)   
                                                UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                                dispatch CancelIsComplete               
                                        | false ->
                                                RestartVisible2 >> dispatch <| (false, false)
                                                do! delayedCmd token dispatch  
                                                RestartVisible2 >> dispatch <| (true, false)
                            | None      
                                -> 
                                ()  
                        } 

                    |> Async.StartImmediate

                match connectivityListener () |> Option.ofBool with
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
                            Cts = newCts  // Update the cts
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
                            Cts = newCts  // Update the cts
                    }, 
                    Cmd.none  

            | None  
                -> 
                m, Cmd.none

        | Mdpo 
            ->   
            match new CancellationTokenSource() |> Option.ofNull with
            | Some newCts
                ->             
                let path = mdpoPathTemp

                let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                    async
                        {
                            NetConnMessage >> dispatch <| String.Empty 
                            RestartVisible2 >> dispatch <| (false, false)
                                                          
                            let! hardWork =                            
                                async 
                                    {
                                        let reportProgress (progressValue, totalProgress) = 
                                            match token.IsCancellationRequested with
                                            | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                            | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true)    

                                        match webscraping_MDPO reportProgress token path with
                                        | Ok _      -> return mauiMdpoMsg
                                        | Error err -> return err
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000
                           
                            match token.IsCancellationRequested with
                            | false -> 
                                    WorkIsComplete >> dispatch <| result
                                    RestartVisible2 >> dispatch <| (false, true)
                            | true  -> 
                                    WorkIsComplete >> dispatch <| netConnError
                                    RestartVisible2 >> dispatch <| (true, false)
                        }  
                     
                let execute dispatch = 

                    async 
                        { 
                            let token =    
                                try
                                    Some <| newCts.Token 
                                with
                                | _ -> None  
                                
                            match token with
                            | Some token 
                                -> 
                                RestartVisible2 >> dispatch <| (false, false)   

                                match token.IsCancellationRequested with
                                | true  ->
                                        RestartVisible2 >> dispatch <| (false, false)  
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        dispatch CancelIsComplete                                 
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  ->
                                                RestartVisible2 >> dispatch <| (false, false)   
                                                UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                                dispatch CancelIsComplete               
                                        | false ->
                                                RestartVisible2 >> dispatch <| (false, false)
                                                do! delayedCmd token dispatch  
                                                RestartVisible2 >> dispatch <| (true, false)
                            | None      
                                -> 
                                ()  
                        } 

                    |> Async.StartImmediate

                match connectivityListener () |> Option.ofBool with
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
                            Cts = newCts  // Update the cts
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
                            Cts = newCts  // Update the cts
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
                            Button(buttonQuit, Quit)
                                .semantics(hint = String.Empty)
                                .centerHorizontal()

                            // Progress circle
                            GraphicsView(progressCircle m.Progress)
                                .height(150.)
                                .width(150.)      
    
                            Label(labelOdis)
                                .semantics(SemanticHeadingLevel.Level1)
                                .font(size = 26.)
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

                                                            Button("x", Cancel String.Empty)
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
                                 
                            Button(buttonKodis, Kodis)
                                .semantics(hint = hintOdis)
                                .centerHorizontal()
                                .isVisible(m.KodisVisible)

                            Button(buttonKodis4, Kodis4)
                                .semantics(hint = hintOdis)
                                .centerHorizontal()
                                .isVisible(m.KodisVisible)   
    
                            Button(buttonDpo, Dpo)
                                .semantics(hint = hintDpo)
                                .centerHorizontal()
                                .isVisible(m.DpoVisible)
    
                            Button(buttonMdpo, Mdpo)
                                .semantics(hint = hintMdpo)
                                .centerHorizontal()
                                .isVisible(m.MdpoVisible)

                            Button(buttonCancel, Cancel cancelMsg1)
                                .semantics(hint = hintCancel)
                                .centerHorizontal()
                                .isVisible(false) //not needed here

                            Button(m.RestartButtonName, Restart)
                                .semantics(hint = hintRestart)
                                .centerHorizontal()
                                .isVisible(m.RestartVisible) 
                        }
                    )
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
    
    let program = Program.statefulWithCmd init update view