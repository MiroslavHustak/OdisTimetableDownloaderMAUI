﻿namespace Helpers

module Connectivity =  

    open System
    open Microsoft.Maui.Networking
    open System.Net.NetworkInformation    
    
    //**********************************
        
    open Types.Types    

    let private actor = 

        //If no timeout or cancellation token is applied or the mailbox is not disposed (all three cases are under my control),
        //the mailbox will not raise an exception on its own. 

        MailboxProcessor<ConnectivityMessage>
            .StartImmediate
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

    let internal connectivityListener () = //vysledek je bool
    
        let initialConnected = (=) Connectivity.NetworkAccess NetworkAccess.Internet
            in
            actor.Post <| UpdateState initialConnected // prvotni inicializace mailboxu
    
        let connectivityChangedHandler (args : ConnectivityChangedEventArgs) =
             
            try  
                let isConnected = (=) args.NetworkAccess NetworkAccess.Internet  
                    in
                    actor.Post <| UpdateState isConnected
            with
            | _ -> ()  //TODO logfile
    
        Connectivity.ConnectivityChanged.Add connectivityChangedHandler 
            
        actor.PostAndReply (fun replyChannel -> CheckState replyChannel)

    let internal connectivityListener2 onConnectivityChange = //vysledek je unit
        
        let connectivityChangedHandler (args : ConnectivityChangedEventArgs) =

            try  
                let isConnected = (=) args.NetworkAccess NetworkAccess.Internet
                    in
                    onConnectivityChange isConnected
            with
            | _ -> ()  //TODO logfile          
            
        Connectivity.ConnectivityChanged.Add connectivityChangedHandler

module CheckNetConnection =  
    
    open FSharp.Control

    open Microsoft.Maui.Networking
    open Microsoft.Maui.ApplicationModel

    open System.Net.NetworkInformation

    //******************** Zatim se nepouziva ********************************************     
           
    let internal checkInternetConnectivityWM () = //Only for Windows Machine
        
        NetworkInterface.GetIsNetworkAvailable () |> Option.ofBool 
    

    //******************** Zatim se nepouziva ******************************************** 
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
        |> Async.RunSynchronously  //API is async-only
    #endif