namespace Helpers

open System
open System.Net.NetworkInformation

open Microsoft.Maui.Networking

#if ANDROID
open Microsoft.Maui.ApplicationModel
#endif 

//**********************************

open FSharp.Control

//**********************************

open Types.Types 
open Types.Haskell_IO_Monad_Simulation

open Settings.SettingsGeneral
open DotNetInteroperabilityCode.ConnectivityMonitorManager


#if ANDROID
open Helpers.Builders   
open JavaInteroperabilityCode.RealInternetChecker
#endif 

module PingTest =

    let private pingHost (host: string) = 
        try
            let pingTimeoutMs = umMiliSecondsToInt32 pingTimeoutMs
            use ping = new Ping()
            let reply = ping.Send(host, pingTimeoutMs)
        
            match reply.Status with
            | IPStatus.Success 
                -> Some reply.RoundtripTime
            | _ -> None
        with
        | _ -> None
    
    let rec private tryPingHosts hosts =
        match hosts with
        | [] -> 
            None  
        | host :: remainingHosts 
            ->
            match pingHost host with
            | Some _ -> Some host     
            | None -> tryPingHosts remainingHosts   

    let internal pingConnectionChecker () =
    
        let hosts =
            [
                "8.8.8.8"          // Google DNS
                "1.1.1.1"          // Cloudflare DNS
                //"google.com"
                //"cloudflare.com"
            ]
    
        tryPingHosts hosts       

module ConnectivityWithDebouncing =    
    
    #if ANDROID
    open PingTest
    #endif

    #if ANDROID
    let internal networkError() =       
        pyramidOfInferno 
            {
                let! _ = testRealInternetConnectivity (), (fun _ -> "No internet connection A") 
                let!__ = PingTest.pingConnectionChecker () |> Result.fromOption, (fun _ -> "No internet connection P") 
                return String.Empty
            }
    #endif 

    let internal isNowConnected () = 
        #if ANDROID                    
            optionBool
                {
                    let! _ = testRealInternetConnectivity () |> Result.toBool 
                    return! PingTest.pingConnectionChecker () |> Option.toBool 
                }
        #else
            (=) Connectivity.NetworkAccess NetworkAccess.Internet
        #endif 
    
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

                                #if ANDROID
                                let isConnected = isNowConnected ()                                   
                                #else
                                let isConnected = (=) Connectivity.NetworkAccess NetworkAccess.Internet
                                #endif

                                // Initial state
                                loop isConnected
                            )
            
                let handler = 
                    System.EventHandler<ConnectivityChangedEventArgs>
                        (fun _ args
                            ->
                            try  
                                #if ANDROID
                                let isConnected = 
                                    optionBool
                                        {
                                            let! _ = testRealInternetConnectivity () |> Result.toBool 
                                            let! _ = pingConnectionChecker () |> Option.toBool                                                                                      
                                            return! (=) args.NetworkAccess NetworkAccess.Internet  
                                        }
                                monitorActor.Post (StateChanged isConnected)

                                #else
                                let isConnected = (=) args.NetworkAccess NetworkAccess.Internet
                                monitorActor.Post (StateChanged isConnected)
                                #endif

                            with
                            | _ -> ()
                        )
            
                addHandler handler
                
                #if ANDROID
                let isConnected = isNowConnected()
                #else
                let isConnected = (=) Connectivity.NetworkAccess NetworkAccess.Internet
                #endif
                
                let initialState = isConnected
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