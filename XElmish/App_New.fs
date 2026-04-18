namespace OdisTimetableDownloaderMAUI

open System
open System.Threading

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

open ProgressCircle

open Settings.Messages
open Settings.SettingsGeneral

open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open ActorModels
open IO_Operations.IO_Operations

open Helpers.ConnectivityWithDebouncing

#if ANDROID
open Helpers.Builders
open AndroidUIHelpers 
open JavaInteroperabilityCode.RealInternetChecker
#endif

open OdisTimetableDownloaderMAUI.Engines.KodisTP
open OdisTimetableDownloaderMAUI.Engines.KodisCanopy

open OdisTimetableDownloaderMAUI.Engines.Dpo
open OdisTimetableDownloaderMAUI.Engines.Mdpo

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
        | Preparing   
        | InProgress of current : float * total : float

    type Screen =
        | Home
        | Utilities
        | ClearingConfirm
        | Downloading of DownloadType * ProgressState
        | Completed of string
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
            Permission : PermissionState
            Connectivity : Connectivity
            Screen : Screen
            Status : string
            ActiveButton : ButtonType option
            KodisCTS : CancellationTokenSource option
        }

    type Msg =
        | KodisTPMsg of KodisTPMsg
        | KodisCanopyMsg of KodisCanopyMsg
        | DpoMsg of DpoMsg
        | MdpoMsg of MdpoMsg
        | NetConnMessage of string
        | SetScreen of Screen
        | Navigate of Screen
        | Click of ButtonType
        | StartDownload of DownloadType
        | CancelDownload
        | RequestPermission
        | RunFileLauncher
        | Dummy
        | Quit

    let connectivityDebouncerSubscription (_model : Model) =
    
        let sub (dispatch : Msg -> unit) =       
            
            let debounceActor = debounceActor NetConnMessage dispatch              
            //250 = debounceMs, (now - lastChangeTime).TotalSeconds > 0.5 resp. 0.2 - s temito hodnotami si pohrat
            runIO <| startConnectivityMonitoring 250 (fun isConnected -> debounceActor.Post isConnected)     
                
        Cmd.ofSub sub
          
    // =============================================
    // INIT
    // =============================================

    let private connectivity msg = 
        match isNowConnected () with    
        | true  -> Connected yesNetConn
        | false -> Disconnected msg

    let init () : Model * Cmd<Msg> =

        #if ANDROID
        let permissionGranted = permissionCheck >> runIO >> Async.RunSynchronously <| ()
        #else
        let permissionGranted = true
        #endif
    
        let permission = match permissionGranted with true -> Granted | false -> NotGranted        
    
        let connectivity = connectivity noNetConn2
    
        let initialScreen = 
            match permission, connectivity with
            | _, Disconnected _    -> NoConnection  // ← disconnected always wins
            | NotGranted, _        -> NoPermission
            | Granted, Connected _ -> Home
    
        let baseModel =
            {
                Permission = permission
                Connectivity = connectivity
                Screen = initialScreen
                Status = match permission with Granted -> String.Empty | NotGranted -> appInfoInvoker
                ActiveButton = None
                KodisCTS = None
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

    let update (msg : Msg) (m : Model) : Model * Cmd<Msg> =

        let connectivity = connectivity noNetConn2
         
        match msg with   
        | Dummy 
            -> 
            m, Cmd.none

        | SetScreen s 
            ->
            { m with Screen = s; Status = String.Empty }, Cmd.none
   
        | Navigate screen
            ->
            { m with Screen = screen; ActiveButton = None; Status = String.Empty }, Cmd.none  
   
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
                                           async { return deleteOld >> runIO <| () }
                                           async { return deleteOld4 >> runIO <| () }
                                       ]
                                       |> Async.Parallel
                                       |> Async.Catch

                                   let message =
                                       match results |> Result.ofChoice with
                                       | Ok [| _; _ |] -> deleteOldTimetablesMsg2
                                       | _             -> deleteOldTimetablesMsg3

                                   dispatch (Navigate (Completed message))
                                   do! Async.Sleep 1000

                                   return ()
                               with
                               | ex ->
                                   runIO (postToLog2 (string ex.Message) "#XElmish_ClearData")
                                   dispatch (Navigate (Completed deleteOldTimetablesMsg3))
                                   return ()
                           }
                       |> Async.Start
                   )

           { m with ActiveButton = Some ClearYes; Status = deleteOldTimetablesMsg1 }, clearDataCmd

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
          
            let msg = HardRestart.exitApp >> runIO <| () 
            { m with Status = msg }, cmd ()
            #endif

            #if ANDROID
            runIO <| KeepScreenOnManager.keepScreenOn false  

            let msg = HardRestart.exitApp >> runIO <| () 

            { 
                m with Status = msg
            },
            Cmd.none
            #endif
   
        | CancelDownload 
            ->           
            match m.KodisCTS with
            | Some cts 
                ->
                cts.Cancel()
                cts.Dispose()
            | None
                ->
                ()
        
            {
                m with
                    Screen = Home
                    Status = cancelMsg42
                    KodisCTS = None
            }, Cmd.none 
   
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
            let cts = new CancellationTokenSource()
   
            let cmd =
                Cmd.ofSub
                    (fun dispatch
                        ->
                        Engines.KodisTP.executeJson
                        <| (fun m -> KodisTPMsg >> dispatch <| m)
                        <| cts.Token
                    )
   
            { 
                m with
                    Screen = Downloading (KodisJsonTP, Idle)
                    Status = progressMsgKodis
                    Connectivity = connectivity 
                    KodisCTS = Some cts
            }, cmd

        | StartDownload KodisPdfTP 
            ->      
            let cts = new CancellationTokenSource()
   
            let cmd =
                Cmd.ofSub 
                    (fun dispatch
                        ->
                        Engines.KodisTP.executePdf
                        <| (fun m -> KodisTPMsg >> dispatch <| m)
                        <| cts.Token
                    )
   
            { 
                m with
                    Screen = Downloading (KodisPdfTP, Idle)
                    Status = dispatchMsg2
                    Connectivity = connectivity 
                    KodisCTS = Some cts
            }, cmd               

        | StartDownload KodisCanopy4 
            ->
            let cts = new CancellationTokenSource()
   
            let cmd =
                Cmd.ofSub 
                    (fun dispatch
                        ->
                        Engines.KodisCanopy.execute
                        <| (fun m -> KodisCanopyMsg >> dispatch <| m)
                        <| cts.Token
                    )
   
            { 
                m with
                    Screen = Downloading (KodisCanopy4, Idle)
                    Status = String.Empty 
                    Connectivity = connectivity 
                    KodisCTS = Some cts
            }, cmd 

        | StartDownload Dpo
            -> 
            let cts = new CancellationTokenSource()
             
            let cmd =
                Cmd.ofSub 
                    (fun dispatch
                        ->
                        Engines.Dpo.executeDpo
                        <| (fun m -> DpoMsg >> dispatch <| m)
                        <| cts.Token
                    )
   
            { 
                m with
                    Screen = Downloading (Dpo, Idle)
                    Status = String.Empty  //TODO
                    Connectivity = connectivity 
                    KodisCTS = Some cts
            }, cmd 
        
        | StartDownload Mdpo
            -> 
            let cts = new CancellationTokenSource()
         
            let cmd =
                Cmd.ofSub 
                    (fun dispatch
                        ->
                        Engines.Mdpo.executeMdpo
                        <| (fun m -> MdpoMsg >> dispatch <| m)
                        <| cts.Token
                    )
   
            { 
                m with
                    Screen = Downloading (Mdpo, Idle)
                    Status = String.Empty  
                    Connectivity = connectivity 
                    KodisCTS = Some cts
            }, cmd 

        | KodisTPMsg msg
            ->
            match msg with
            | Engines.KodisTP.Progress (c, t)
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> { m with Screen = Downloading (dt, InProgress (c, t)) }, Cmd.none
                | _ -> m, Cmd.none       
            
            | Engines.KodisTP.IterationMsg text 
                ->
                { m with Status = text }, Cmd.none
        
            | Engines.KodisTP.Completed result
                ->            
                match m.Screen with            
                | Downloading (KodisJsonTP, _)
                    ->            
                    { m with Status = dispatchMsg2; KodisCTS = None },
                        StartDownload >> Cmd.ofMsg <| KodisPdfTP           
               
                | Downloading (KodisPdfTP, _) 
                    ->            
                    {
                        m with
                            Screen = Completed result
                            Status = String.Empty // result
                            KodisCTS = None
                    }, Cmd.none
            
                | _ ->
                    m, Cmd.none
        
            | Engines.KodisTP.ErrorKodis err
                -> { m with Screen = ErrorScreen err; KodisCTS = None }, Cmd.none
        
            | Engines.KodisTP.NavigateHome 
                -> { m with Screen = Home; KodisCTS = None }, Cmd.none            
                
        | KodisCanopyMsg msg
            ->
            match msg with
            | Engines.KodisCanopy.Progress (c, t)
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> { m with Screen = Downloading (dt, InProgress (c, t)) }, Cmd.none
                | _ -> m, Cmd.none       
        
            | Engines.KodisCanopy.IterationMsg text 
                ->
                { m with Status = text }, Cmd.none
        
            | Engines.KodisCanopy.Completed result
                ->            
                match m.Screen with               
                | Downloading (KodisCanopy4 , _) 
                    ->            
                    {
                        m with
                            Screen = Completed result
                            Status = String.Empty //result
                            KodisCTS = None
                    }, Cmd.none
        
                | _ ->
                    m, Cmd.none
        
            | Engines.KodisCanopy.ErrorKodis err
                -> { m with Screen = ErrorScreen err; KodisCTS = None }, Cmd.none
        
            | Engines.KodisCanopy.NavigateHome 
                -> { m with Screen = Home; KodisCTS = None }, Cmd.none   
                
            | Engines.KodisCanopy.Preparing
                ->
                match m.Screen with
                | Downloading (dt, _) 
                    -> { m with Screen = Downloading (dt, ProgressState.Preparing) }, Cmd.none
                | _ -> m, Cmd.none
                
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
                | Downloading (Dpo , _) 
                    ->            
                    {
                        m with
                            Screen = Completed result
                            Status = String.Empty //result
                            KodisCTS = None
                    }, Cmd.none
        
                | _ ->
                    m, Cmd.none
        
            | Engines.Dpo.ErrorDpo err
                -> { m with Screen = ErrorScreen err; KodisCTS = None }, Cmd.none
        
            | Engines.Dpo.NavigateHome 
                -> { m with Screen = Home; KodisCTS = None }, Cmd.none  
                
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
                | Downloading (Mdpo , _) 
                    ->            
                    {
                        m with
                            Screen = Completed result
                            Status = String.Empty 
                            KodisCTS = None
                    }, Cmd.none
               
                | _ ->
                    m, Cmd.none
               
            | Engines.Mdpo.ErrorMdpo err
                -> { m with Screen = ErrorScreen err; KodisCTS = None }, Cmd.none
               
            | Engines.Mdpo.NavigateHome  
                -> { m with Screen = Home; KodisCTS = None }, Cmd.none  

    let view (m: Model) : WidgetBuilder<Msg, IFabApplication> =
        
        // =============================================
        // VIEW HELPERS 
        // =============================================

        let animate buttonType =
            match m.ActiveButton with
            | Some b 
                when b = buttonType
                -> 1.2
            | _ -> 1.0
    
        let progressValue =
            match m.Screen with
            | Downloading (_, InProgress (curr, total)) 
                when total > 0.0
                ->
                let v = (1.0 / total) * curr
                match v >= 1.0 with 
                | true  -> 1.0 
                | false -> v
            | _    
                ->
                0.0
    
        let progressCircleVisible =
            match m.Screen with
            | Downloading (_, InProgress _) 
                -> true
            | _ -> false
    
        let homeView =
            VStack(spacing = 25.) {
                Button(buttonKodis, StartDownload KodisJsonTP)
                    .semantics(hint = hintOdis)
                    .centerHorizontal()
    
                Button(buttonKodis4, StartDownload KodisCanopy4)
                    .semantics(hint = hintOdis)
                    .centerHorizontal()
    
                Button(buttonDpo, StartDownload Dpo)
                    .semantics(hint = hintDpo)
                    .centerHorizontal()
    
                Button(buttonMdpo, StartDownload Mdpo)
                    .semantics(hint = hintMdpo)
                    .centerHorizontal()
    
                Button("Nástroje", Navigate Utilities)
                    .semantics(hint = String.Empty)
                    .centerHorizontal()
            }
    
        let utilitiesView =
            VStack(spacing = 25.) {
                Button(buttonLauncher, RunFileLauncher)
                    .semantics(hint = String.Empty)
                    .centerHorizontal()
                    .background(SolidColorBrush(Colors.YellowGreen))
    
                Button(buttonClearing, Click Clear)
                    .semantics(hint = hintClearing)
                    .centerHorizontal()
                    .background(SolidColorBrush(Colors.YellowGreen))
                    //.scaleX(animate Clear)
                    //.scaleY(animate Clear)
    
                Button(buttonHome, Navigate Home)
                    .semantics(hint = String.Empty)
                    .centerHorizontal()
                    .background(SolidColorBrush(Colors.LightGreen))
            }
    
        let clearingConfirmView =
            (VStack(spacing = 15.) {
                Border(
                    ContentView(
                        HStack(spacing = 12.) {
                            Button(buttonClearingConfirmation, Click ClearYes)
                                .font(size = 14., attributes = FontAttributes.None)
                                .padding(2.5, -5.5, 2.5, 2.5)
                                .cornerRadius(2)
                                .height(25.)
                                .background(SolidColorBrush(Colors.DarkRed))
                                //.scaleX(animate ClearYes)
                                //.scaleY(animate ClearYes)
    
                            Button(buttonClearingCancel, Click ClearNot)
                                .font(size = 14., attributes = FontAttributes.None)
                                .padding(2.5, -5.5, 2.5, 2.5)
                                .cornerRadius(2)
                                .height(25.)
                                .background(SolidColorBrush(Colors.DarkRed))
                                //.scaleX(animate ClearNot)
                                //.scaleY(animate ClearNot)
                        }
                    )
                )
                    .stroke(SolidColorBrush(Colors.Gray))
                    .strokeShape(RoundRectangle(cornerRadius = 5.))
                    .background(SolidColorBrush(Colors.White))
                    .strokeThickness(0.5)
                    .padding(5.)
            })
                .centerHorizontal()
                .centerVertical()
    
        let downloadingView (dt : DownloadType) (ps : ProgressState) =
            VStack(spacing = 25.) {
                Label(
                    match dt with
                    | KodisJsonTP  -> "Varianta stahování: Kodis TP"
                    | KodisPdfTP   -> "Varianta stahování: Kodis TP"
                    | KodisCanopy4 -> "Varianta stahování: Kodis Canopy"
                    | Dpo          -> progressMsgDpo
                    | Mdpo         -> progressMsgMdpo   
                )
                    .font(size = 14.)
                    .centerTextHorizontal()
    
                Button(buttonCancel, CancelDownload)
                    .semantics(hint = String.Empty)
                    .centerHorizontal()
                    .isVisible(                    // ← only show when actually downloading
                        match ps with
                        | InProgress _ -> true
                        | _            -> false
                    )
            }
    
        let completedView msg =
            VStack(spacing = 25.) {
                Label(msg)
                    .font(size = 14.)
                    .centerTextHorizontal()
    
                Button(buttonHome, Navigate Home)
                    .semantics(hint = String.Empty)
                    .background(SolidColorBrush(Colors.LightGreen))
                    .centerHorizontal()
                    .scaleX(animate Restart)
                    .scaleY(animate Restart)
            }
    
        let noConnectionView =
            VStack(spacing = 25.) {
                //Label(noNetConnInitial)
                Label(String.Empty)
                    .font(size = 14.)
                    .centerTextHorizontal()
            }

        let noPermissionView =
            VStack(spacing = 25.) {
                #if ANDROID
                Button(buttonRequestPermission, RequestPermission)
                    .semantics(hint = "Grant permission to access storage")
                    .centerHorizontal()
                #endif
            }
    
        let errorView msg =
            VStack(spacing = 25.) {
                Label(msg)
                    .font(size = 14.)
                    .centerTextHorizontal()
            }
    
        // =============================================
        // UI/UX - actual view
        // =============================================
    
        Application(
            ContentPage(
                ScrollView(
                    (VStack(spacing = 25.) {
    
                        GraphicsView(runIO <| progressCircle progressValue)
                            .height(130.)
                            .width(130.)
                            .centerHorizontal()
                            .isVisible(progressCircleVisible)
    
                        Label(labelOdis)
                            .semantics(SemanticHeadingLevel.Level1)
                            .font(size = 24.)
                            .centerTextHorizontal()
    
                        Label(m.Status)
                            .semantics(SemanticHeadingLevel.Level2, String.Empty)
                            .font(size = 14.)
                            .centerTextHorizontal()

                        Label(m.Connectivity 
                                 |> function 
                                     | Connected msg    -> msg
                                     | Disconnected msg -> msg
                             )
                            .semantics(SemanticHeadingLevel.Level3, String.Empty)
                            .font(size = 14.)
                            .centerTextHorizontal() 
    
                        match m.Screen with
                        | Home                 -> homeView
                        | Utilities            -> utilitiesView
                        | ClearingConfirm      -> clearingConfirmView                           
                        | Completed msg        -> completedView msg
                        | Downloading (dt, ps) -> downloadingView dt ps
                        | NoConnection         -> noConnectionView
                        | NoPermission         -> noPermissionView
                        | ErrorScreen err      -> errorView err
    
                        Button(buttonQuit, Quit)
                            .semantics(hint = String.Empty)
                            .centerHorizontal()
                            .background(SolidColorBrush(Colors.Green))
                    })
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
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
        |> Program.withSubscription (connectivityDebouncerSubscription)
        |> Program.withSubscription captureDispatchSub