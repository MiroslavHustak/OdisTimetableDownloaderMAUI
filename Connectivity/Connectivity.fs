namespace Helpers

open System
open System.Net.NetworkInformation

open Microsoft.Maui.Networking
open Microsoft.Maui.ApplicationModel

//**********************************

open FSharp.Control

//**********************************
               
open Types.Types 
open Types.Haskell_IO_Monad_Simulation

open DotNetInteroperabilityCode.ConnectivityMonitorManager

module Connectivity =  

    let private actor = //tady nelze IO Monad (pak se actor nespusti tak, jak je treba)

        //If no timeout or cancellation token is applied or the mailbox is not disposed (all three cases are under my control),
        //the mailbox will not raise an exception on its own. 

        MailboxProcessor<ConnectivityMessage>.StartImmediate
            <|
            fun inbox
                ->
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

    // nepouzivano                   
    let internal connectivityListener () = //impure

        IO (fun () 
                -> 
                let initialConnected = (=) Connectivity.NetworkAccess NetworkAccess.Internet     
                
                actor.Post <| UpdateState initialConnected // prvotni inicializace mailboxu
    
                let connectivityChangedHandler (args : ConnectivityChangedEventArgs) =
             
                    try  
                        let isConnected = (=) args.NetworkAccess NetworkAccess.Internet                              
                        actor.Post <| UpdateState isConnected
                    with
                    | _ -> ()  //Proste at to tise pokracuje
            
                Connectivity.ConnectivityChanged.Add connectivityChangedHandler 
            
                actor.PostAndReply (fun replyChannel -> CheckState replyChannel)
        )        

    // nepouzivano   
    let internal connectivityListener2 onConnectivityChange = //impure

        IO (fun () 
                ->         
                let connectivityChangedHandler (args : ConnectivityChangedEventArgs) =

                    try  
                        let isConnected = (=) args.NetworkAccess NetworkAccess.Internet                            
                        onConnectivityChange isConnected
                    with
                    | _ -> ()  //Proste at to tise pokracuje         
            
                Connectivity.ConnectivityChanged.Add connectivityChangedHandler
        )

module ConnectivityWithDebouncing =
    
    let internal startConnectivityMonitoring (debounceMs : int) (onConnectivityChange : bool -> unit) =
        
        IO (fun ()
                ->
                // Debouncing actor
                let monitorActor =
                    MailboxProcessor<ConnectivityMonitorMsg>
                        .StartImmediate
                            (fun inbox 
                                ->
                                let rec loop lastState = 
                        
                                    async
                                        {
                                            let! msg = inbox.Receive()

                                            match msg with
                                            | StopConnectivityMonitoring
                                                ->
                                                return ()
                                            | StateChanged newState 
                                                when newState <> lastState 
                                                ->
                                                // Wait for the debounce interval
                                                let! maybeNextMsg = inbox.TryReceive debounceMs
                                                match maybeNextMsg with
                                                | Some (StateChanged newerState)
                                                    ->
                                                    // Another state change occurred → restart waiting
                                                    return! loop newerState
                                                | Some StopConnectivityMonitoring 
                                                    ->
                                                    return ()
                                                | _ 
                                                    ->
                                                    // State is stable → notify
                                                    onConnectivityChange newState
                                                    return! loop newState
                                            | StateChanged _
                                                ->
                                                // Same state → ignore
                                                return! loop lastState
                                        }
                                // Initial state
                                loop (Connectivity.NetworkAccess = NetworkAccess.Internet)
                            )
            
                // Event handler
                let handler = 
                    System.EventHandler<ConnectivityChangedEventArgs>
                        (fun _ args
                            ->
                            try  
                                let isConnected = (=) args.NetworkAccess NetworkAccess.Internet
                                monitorActor.Post (StateChanged isConnected)
                            with
                            | _ -> ()
                        )
            
                addHandler handler
                
                let initialState = (=) Connectivity.NetworkAccess NetworkAccess.Internet
                monitorActor.Post (StateChanged initialState)
        )
    
    //Pro in production nevyuzito, mozno vyuzit v prubehu stress testing
    let internal stopAllConnectivityMonitoring () = IO (fun () -> removeAllHandlers())

        (*
        OS event
        └─ actor receives
            └─ cancel old timer
                └─ wait debounceMs
                    └─ if uninterrupted → notify app

        Layer 1

        OS connectivity events (raw, noisy)
        Wi-Fi ───┐    ┌─── none ──┐ ┌── Wi-Fi
                 │    │            │ │
                 │    │            │ │
        Time →   ─────┼────────────┼──────────────

        OS event ──► startConnectivityMonitoring ──(200 ms debounce)─► stable events
        Wi-Fi ───┐
                 │
        none ────┘
        Wi-Fi ─────────────────────────────► Stable "Connected" event

        Layer 2

        Stable event ──► debounceActor ──► app reaction
        true ─────────────────────► Dispatch "Connected"
        false ───────────────────► Dispatch "No Connection" + start countdown


        Combined flow diagram (simplified)

        OS Connectivity Events (Wi-Fi / None / Wi-Fi / None)
                    │
                    ▼
        startConnectivityMonitoring (200 ms debounce) //OS-level debounce: 200 ms → filters micro-flickers. Must be much shorter than all operational timeouts.
                    │
                    ▼
        Stable Connectivity Events (true / false)
                    │
                    ▼
        debounceActor (0.2s app logic debounce)  Must be much shorter than all operational timeouts.
                    │
                    ▼
        App reacts:
            - Dispatch UI messages
            - Start/stop quit/restart countdown

        Key notes from this diagram
        
        Two separate debounce layers:
        
        Layer 1: removes OS noise.
        
        Layer 2: ensures app logic isn’t spammed.
        
        Layer 2 never sees raw OS flicker, only already debounced signals.

        Layer 2 uses non-blocking timeout-based debounce (MailboxProcessor.TryReceive) → always responsive, no frozen mailbox during waiting period    
        
        Timers cancel safely in both layers, avoiding race conditions.
        
        Makes the UI stable and countdowns reliable.  

        Note: Combined debounce (OS + App) = 0.4 s → < 2% of 30 s countdown → fast and safe.
        Debounce times should be < 5% of your shortest operational timeout.
        *)

module CheckNetConnection =      

    //******************** Zatim se nepouziva TODO IO Monad ********************************************     
           
    let internal checkInternetConnectivityWM () = //Only for Windows Machine        
        NetworkInterface.GetIsNetworkAvailable () |> Option.ofBool 
        

    //******************** Zatim se nepouziva TODO IO Monad ******************************************** 
    #if ANDROID
    let internal checkInternetConnectivityAE () =

        let requestLocationPermission () =
                
            async
                {
                    let! status = 
                        Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() |> Async.AwaitTask

                    match status <> PermissionStatus.Granted with
                    | true  ->
                            let! result = 
                                Permissions.RequestAsync<Permissions.LocationWhenInUse>() |> Async.AwaitTask

                            return (=) result PermissionStatus.Granted
                    | false ->
                            return true
                }

        async
            {
                try                          
                    match Connectivity.Current.ConnectionProfiles |> Option.ofNull with            
                    | Some profiles
                        ->
                        match profiles |> List.ofSeq with
                        | [] 
                            -> 
                            return None                                         
                        | _  
                            ->
                            let! hasPermission = requestLocationPermission ()

                            let cond1 = profiles |> Seq.contains ConnectionProfile.WiFi
                            let cond2 = (=) Connectivity.NetworkAccess NetworkAccess.Internet

                            match cond1 && cond2 && hasPermission with
                            | true  -> return Some ()
                            | false -> return None  

                    | None 
                        ->
                        return None
                with
                | _ -> return None                
            }  
    #endif