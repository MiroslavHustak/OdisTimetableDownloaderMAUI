namespace OdisTimetableDownloaderMAUI

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
open Helpers.Connectivity

open ApplicationDesign.WebScraping_DPO
open ApplicationDesign.WebScraping_MDPO
open ApplicationDesign.WebScraping_KODISFMRecord
open ApplicationDesign4.WebScraping_KODISFMRecord4

(*     
    AndroidManifest.xml: Do not forget to review and update it if necessary. 
*)

module App =

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
            RestartVisible : bool
            CloudVisible : bool
            LabelVisible : bool
            Label2Visible : bool            
            Cts : CancellationTokenSource  //Cancellation tokens for educational purposes only 
            Token : CancellationToken
        }

    type Msg =
        | Kodis  
        | Kodis4  
        | Dpo
        | Mdpo
        | Restart
        | Quit
        | QuitCountdown of string
        | CancellationToken2  //Cancellation tokens for educational purposes only 
        | NetConnMessage of string
        | IterationMessage of string    
        | UpdateStatus of float * float * bool
        | WorkIsComplete of string * bool    
                   
    let init () =  //Cancellation tokens for educational purposes only 
        
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
                                    async
                                        {
                                            match isConnected with
                                            | true  -> NetConnMessage >> dispatch <| yesNetConn    
                                            | false -> NetConnMessage >> dispatch <| noNetConnPlusPlus
                                        }    
                                    |> Async.StartImmediate  //muze byt aji Async.Start, pokud dany blok nemusi byt spusten hned s hlavnim blokem //Async.Start runs the task asynchronously on the thread pool, while Async.StartImmediate attempts to run the task immediately on the current thread. *)
                                )                                  
                                
                            do! Async.Sleep 5000    
                        }
                )
            |> Async.StartImmediate  //tady musim hned, nelze Async.Start
        
        let initialModel = //Cancellation tokens for educational purposes only 
            {                 
                ProgressMsg = String.Empty
                NetConnMsg = String.Empty
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = true
                DpoVisible = true
                MdpoVisible = true
                RestartVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
                Cts = new CancellationTokenSource() //s tim nic tady nenarobim, pokud null, zrejme to vyhodi exn pro Cts.Token
                Token = (new CancellationTokenSource()).Token
            } 

        let initialModelNoConn = //Cancellation tokens for learning purposes only 
            {                 
                ProgressMsg = String.Empty
                NetConnMsg = noNetConnInitial
                CloudProgressMsg = String.Empty
                ProgressIndicator = Idle
                Progress = 0.0 
                KodisVisible = false
                DpoVisible = false
                MdpoVisible = false
                RestartVisible = false
                CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                LabelVisible = true
                Label2Visible = true
                Cts = initialModel.Cts //s tim nic tady nenarobim, pokud null, zrejme to vyhodi exn pro Cts.Token
                Token = initialModel.Token
            }     
        
        try
            pyramidOfDoom
                {
                    let initialModelCtsToken =    //Cancellation tokens for learning purposes only 
                        try
                            Some <| initialModel.Cts.Token               
                        with
                        | _ -> None    

                    //initialModelNoConn.Token uz neni tra overovat, bo je zavisly na vyse uvedenem

                    let! initialModelToken = initialModelCtsToken, ({ initialModel with NetConnMsg = ctsMsg }, Cmd.none)
                    let! _ = connectivityListener () |> Option.ofBool, (initialModelNoConn, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch initialModelNoConn.Token))

                    return initialModel, Cmd.ofSub (fun dispatch -> monitorConnectivity dispatch initialModelToken)
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
                    RestartVisible = isVisible
            }, 
            Cmd.none

        | QuitCountdown message
            ->
            {
                m with                    
                    ProgressMsg = quitMsg2
                    NetConnMsg = message
                    ProgressIndicator = Idle
                    Progress = 0.0 
                    CloudProgressMsg = String.Empty                    
                    KodisVisible = false
                    DpoVisible = false
                    MdpoVisible = false
                    CloudVisible = false  //nechej to false, zatim nebudu pouzivat
                    LabelVisible = true
                    Label2Visible = true
            }, 
            Cmd.none  
       
        | CancellationToken2 //Template for cancellation tokens 
            ->             
            pyramidOfDoom
                {  
                    // Create a new CancellationTokenSource for future use //v danem pripade aji pro konkretni pouziti
                    let! newCts = new CancellationTokenSource() |> Option.ofNull, (m, Cmd.none) 

                    let newToken =    
                        try
                            Some <| m.Cts.Token                                                 
                        with
                        | _ -> None         

                    let! newToken = newToken, (m, Cmd.none) 

                    let ctsCancel () =    
                        try
                            try
                                Some <| newCts.Cancel() //This signal is irreversible once sent.
                               //newCts.CancelAfter(TimeSpan.FromSeconds(float timeOutInSeconds)) zvazit pouziti, neb requesting je az po danem case, ne ze zrobi cancel po danem case
                            finally                      
                                m.Cts.Dispose() //Any code that has already received or responded to the cancellation won’t be affected by the disposal.
                        with
                        | _ -> None

                    let!_ = ctsCancel (), (m, Cmd.none) 
                   
                    return  
                        { 
                            m with     
                                ProgressIndicator = InProgress (0.0, 0.0)
                                Progress = 0.0
                                KodisVisible = false
                                DpoVisible = false
                                MdpoVisible = false
                                RestartVisible = false
                                Cts = newCts  // Replace the old cts with a new one
                                Token = newToken
                        },
                        Cmd.none
                }    

        | IterationMessage message 
            ->
            { m with ProgressMsg = message }, Cmd.none   

        | Quit  
            -> 
            HardRestart.exitApp () 
            m, Cmd.none

        | Restart  
            -> 
            init ()
             
        | NetConnMessage message
            ->
            { m with NetConnMsg = message; LabelVisible = true; Label2Visible = true }, Cmd.none           
             
        | Kodis 
            ->
            match new CancellationTokenSource() |> Option.ofNull with  //Cancellation tokens for educational purposes only 
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
                                            UpdateStatus >> dispatch <| (progressValue, totalProgress, true)                                         

                                        return 
                                            stateReducerCmd1
                                            <| token
                                            <| path
                                            <| reportProgress
                                    }
                                |> Async.StartChild

                            let! result = hardWork 
                            do! Async.Sleep 1000
                            
                            match result with
                            | Ok result -> WorkIsComplete >> dispatch <| (result, false)  
                            | Error err -> WorkIsComplete >> dispatch <| (err, false) 
                        }  

                let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  

                    async 
                        {   
                            let! hardWork =                             
                                async 
                                    {   
                                        let reportProgress (progressValue, totalProgress) = 
                                            UpdateStatus >> dispatch <| (progressValue, totalProgress, true)  
                                       
                                        return
                                            stateReducerCmd2 
                                            <| token
                                            <| path
                                            <| fun message -> WorkIsComplete >> dispatch <| (message, false)
                                            <| fun message -> IterationMessage >> dispatch <| message 
                                            <| reportProgress            
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000
                          
                            WorkIsComplete >> dispatch <| (result, true)
                        }     

                let executeSequentially dispatch =

                    async 
                        {                                         
                            do! delayedCmd1 m.Token dispatch                                        
                            do! delayedCmd2 m.Token dispatch 
                        }
                    |> Async.StartImmediate

                match connectivityListener () |> Option.ofBool with
                | Some _
                    ->             
                    { 
                        m with                               
                            ProgressMsg = progressMsgKodis1 
                            //NetConnMsg = String.Empty
                            ProgressIndicator = InProgress (0.0, 0.0)
                            KodisVisible = false
                            DpoVisible = false
                            MdpoVisible = false
                            Cts = new CancellationTokenSource()  // Update the cts
                            Token = newCts.Token
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
                            Cts = new CancellationTokenSource()  // Update the cts
                            Token = newCts.Token
                    }, 
                    Cmd.none 
                                   
            | None      
                -> 
                m, Cmd.none           

        | Kodis4 
            -> 
            match new CancellationTokenSource() |> Option.ofNull with  //Cancellation tokens for educational purposes only 
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
                                            UpdateStatus >> dispatch <| (progressValue, totalProgress, true)        
                                       
                                        return
                                            stateReducerCmd4
                                            <| token
                                            <| path
                                            <| fun message -> WorkIsComplete >> dispatch <| (message, false)
                                            <| fun message -> IterationMessage >> dispatch <| message 
                                            <| reportProgress            
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000

                            let countDown () = 
                                [ 30 .. -1 .. 0 ]  //30 vterin // -1 for backward counting
                                |> List.toSeq                                             
                                |> AsyncSeq.ofSeq
                                |> AsyncSeq.iterAsync
                                    (fun remaining 
                                        ->                                                               
                                        QuitCountdown >> dispatch <| (quitMsg1 remaining)
                                        
                                        match remaining with
                                        | 0 -> async { return dispatch Quit } |> Async.executeOnMainThread
                                        | _ -> Async.Sleep 1000
                                    )  
                                |> Async.StartImmediate 

                            match result.Contains("timeout") with
                            | true  -> countDown ()
                            | false -> WorkIsComplete >> dispatch <| (result, true)
                        }     

                let executeSequentially dispatch =                    

                    async 
                        {  
                            do! delayedCmd2 m.Token dispatch   
                        }
                    |> Async.StartImmediate  

                match connectivityListener () |> Option.ofBool with
                | Some _
                    ->             
                    { 
                        m with                               
                            ProgressMsg = progressMsgKodis1 
                            //NetConnMsg = String.Empty
                            ProgressIndicator = InProgress (0.0, 0.0)
                            KodisVisible = false
                            DpoVisible = false
                            MdpoVisible = false
                            Cts = new CancellationTokenSource()  // Update the cts
                            Token = newCts.Token
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
                            Cts = new CancellationTokenSource()  // Update the cts
                            Token = newCts.Token
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
                                                          
                            let! hardWork =                            
                                async 
                                    {
                                        let reportProgress (progressValue, totalProgress) = 
                                            match token.IsCancellationRequested with
                                            | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                            | false -> UpdateStatus >> dispatch <| (progressValue, totalProgress, true)    

                                        match webscraping_DPO reportProgress token path with  
                                        | Ok _      -> return mauiDpoMsg
                                        | Error err -> return err
                                    }
                                |> Async.StartChild 
                               
                            let! result = hardWork 
                            do! Async.Sleep 1000
                           
                            match token.IsCancellationRequested with
                            | false -> WorkIsComplete >> dispatch <| (result, true)
                            | true  -> WorkIsComplete >> dispatch <| (netConnError, true)
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
                                match token.IsCancellationRequested with
                                | true  ->
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        | false -> do! delayedCmd token dispatch  
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
                            | false -> WorkIsComplete >> dispatch <| (result, true)
                            | true  -> WorkIsComplete >> dispatch <| (netConnError, true)
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
                                match token.IsCancellationRequested with
                                | true  ->
                                        UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                | false ->                                                               
                                        match token.IsCancellationRequested with
                                        | true  -> UpdateStatus >> dispatch <| (0.0, 1.0, false)
                                        | false -> do! delayedCmd token dispatch  
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
                            // Progress circle
                            GraphicsView(progressCircle m.Progress)
                                .height(130.)
                                .width(130.)      
    
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

                                                            Button("x", Restart)
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

                            Button(buttonRestart, Restart)
                                .semantics(hint = String.Empty)
                                .isVisible(m.RestartVisible)
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