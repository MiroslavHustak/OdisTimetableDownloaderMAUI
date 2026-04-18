namespace OdisTimetableDownloaderMAUI

open Fabulous.Maui
open FSharp.Control

//********************************

open Settings.Messages
open Settings.SettingsGeneral

open Api.Logging

open Types.Types
open Types.Haskell_IO_Monad_Simulation
open Helpers.ConnectivityWithDebouncing

module Counters =  

    // Not used yet
    let internal countDownNew countDownMessage netConnMessage quit dispatch =
              
        IO (fun () 
                ->  
                //abych se zbavil varovani ohledne capital letters v parametrech
                let CountDownMessage = countDownMessage 
                let NetConnMessage = netConnMessage
                let Quit = quit

                //This "loop" fn anotated with [<TailCall>] tested as a module fn in another F# project; no warnings encountered
                let rec loop remaining =

                    async
                        {
                            CountDownMessage >> dispatch <| (quitMsg remaining)
          
                            match isNowConnected () with
                            | false 
                                when remaining = 0
                                    ->
                                    do! 
                                        async { return dispatch Quit }
                                        |> Async.executeOnMainThread  
                            | false 
                                when remaining <> 0 
                                    ->
                                    do! Async.Sleep 1000
                                 
                                    return! loop (remaining - 1) // Recurring with the next remaining value
                            | _ 
                                    ->
                                    do! runIO (postToLog2Async <| "End of counter loop" <| "#0001-Counters")                                    
                                    return!                                        
                                        async { return NetConnMessage >> dispatch <| continueDownload } 
                                        |> Async.executeOnMainThread 
                        }
          
                //Async.StartImmediate (loop waitingForNetConn) //Async.StartImmediate -> common cause of ANRs (Application Not Responding) on Android.
                Async.Start (umSecondsToInt32 >> loop <| waitingForNetConn) 
        )

    // Not used yet 
    let internal countDown2 isConnected quitCountdown restartVisible netConnMessage quit dispatch =
           
        IO (fun () 
                ->  
                //abych se zbavil varovani ohledne capital letters v parametrech
                let QuitCountdown = quitCountdown 
                let NetConnMessage = netConnMessage
                let Quit = quit
                let RestartVisible = restartVisible

                //This "loop" fn anotated with [<TailCall>] tested as a module fn in another F# project; no warnings encountered
                let rec loop remaining =

                    async
                        {
                            QuitCountdown >> dispatch <| (quitMsg remaining)
       
                            match isConnected with
                            | false 
                                when remaining = 0
                                    ->
                                    RestartVisible >> dispatch <| false

                                    do! 
                                        async { return dispatch Quit }
                                        |> Async.executeOnMainThread  
                            | false 
                                when remaining <> 0 
                                    ->
                                    do! Async.Sleep 1000
                                    RestartVisible >> dispatch <| false
                              
                                    return! loop (remaining - 1) // Recurring with the next remaining value
                            | _ 
                                    ->
                                    do! runIO (postToLog2Async <| "End of counter loop" <| "#0001-Counters")                                    
                                    return!                                        
                                        async { return NetConnMessage >> dispatch <| continueDownload } 
                                        |> Async.executeOnMainThread 
                        }
       
                //Async.StartImmediate (loop waitingForNetConn) //Async.StartImmediate -> common cause of ANRs (Application Not Responding) on Android.
                Async.Start (umSecondsToInt32 >> loop <| waitingForNetConn) 
        )

    // Tato varianta odpocitadla v pozadi jede dal az do 0, aji kdyz predcasne ukoncime
    // Not used yet
    let internal countDown isConnected quitCountdown netConnMessage quit dispatch = 

        IO (fun () 
                ->  
                //abych se zbavil varovani ohledne capital letters v parametrech
                let QuitCountdown = quitCountdown 
                let NetConnMessage = netConnMessage
                let Quit = quit

                [ umSecondsToInt32 waitingForNetConn .. -1 .. 0 ]  // -1 for backward counting
                |> List.toSeq                                             
                |> AsyncSeq.ofSeq
                |> AsyncSeq.iterAsync
                    (fun remaining 
                        ->      
                        QuitCountdown >> dispatch <| (quitMsg remaining)
                
                        match isConnected with
                        | false 
                            when remaining = 0  
                                ->
                                async { return dispatch Quit } |> Async.executeOnMainThread    
                        | false
                            when remaining <> 0
                                -> 
                                Async.Sleep 1000 //po vterine to odpocitava
                        | _
                                -> 
                                async
                                    { 
                                        //tato varianta v pozadi jede dal
                                        return NetConnMessage >> dispatch <| continueDownload
                                    } 
                                |> Async.executeOnMainThread                           
                    ) 
        )

    // Pouzivana varianta odpocitadla 
   