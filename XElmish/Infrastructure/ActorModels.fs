namespace OdisTimetableDownloaderMAUI

open System
open System.Threading

open Types.Types
open Settings.Messages

module ActorModels =  

//********************** resumable App_New **************************************

    let internal debounceActor netConnMessage dispatch =
    
        MailboxProcessor.StartImmediate
            (fun inbox
                ->
                let rec loop lastState (lastChangeTime : DateTime) isFirstMessage =
                           
                    let NetConnMessage = netConnMessage
    
                    async
                        {
                            let! isConnected = inbox.Receive()
                            let now = DateTime.Now
                            let timeDiff = (now - lastChangeTime).TotalSeconds
                                           
                            match isFirstMessage, isConnected, lastState, timeDiff > 0.5 with
                            | true, false, _, _
                                ->
                                // First message: lost connection → dispatch immediately
                                NetConnMessage >> dispatch <| noNetConn
                                return! loop isConnected now false
                            | true, true, _, _
                                ->
                                // First message: have connection → dispatch immediately
                                dispatch (NetConnMessage yesNetConn)
                                return! loop isConnected now false
                            | false, false, true, _
                                ->
                                // Lost connection: react immediately (no debouncing)
                                NetConnMessage >> dispatch <| noNetConn
                                return! loop isConnected now false
                            | false, true, false, true
                                ->
                                // Gained connection: debounced (state stable for 0.5s)
                                dispatch (NetConnMessage yesNetConn)
                                return! loop isConnected now false
                            | false, _, _, _ when isConnected <> lastState
                                ->
                                // State changed but not ready to dispatch yet
                                dispatch (NetConnMessage "Čekám ...")
                                return! loop isConnected now false
                            | _
                                ->
                                // No state change or still waiting
                                dispatch (NetConnMessage "Stále čekám ...")
                                return! loop lastState lastChangeTime false
                        }
                loop true DateTime.MinValue true
            )

    let internal localCancellationActor () =
    
        MailboxProcessor<CancellationMessageLocal>
            .StartImmediate
                (fun inbox 
                    ->
                    let rec loop (cts : CancellationTokenSource) =
                        async 
                            {
                                match! inbox.Receive() with
                                | GetToken reply
                                    ->
                                    let tokenOpt =
                                        match cts.IsCancellationRequested with
                                        | true  -> None
                                        | false -> Some cts.Token
                                    reply.Reply tokenOpt 
                                    return! loop cts
    
                                | CancelToken
                                    ->
                                    cts.Cancel()
                                    return! loop cts
    
                                | Reset newCts 
                                    ->
                                    cts.Dispose()
                                    return! loop newCts
    
                                | StopLocal reply 
                                    ->
                                    cts.Cancel()
                                    cts.Dispose()
                                    reply.Reply()
                                    return! loop (new CancellationTokenSource()) //for App_New

                                | CancelAndReset reply
                                    ->
                                    cts.Cancel()
                                    cts.Dispose()
                                    let newCts = new CancellationTokenSource()
                                    reply.Reply()
                                    return! loop newCts
                            }
    
                    loop (new CancellationTokenSource())
                )

    let internal cancelLocalActor (actor : MailboxProcessor<CancellationMessageLocal>) =
        
        let newCts = new CancellationTokenSource()
    
        actor.Post CancelToken
        Reset >> actor.Post <| newCts

    let internal cancelLocalActor2 (actor : MailboxProcessor<CancellationMessageLocal>) =

        actor.PostAndReply(fun reply -> CancelAndReset reply)

        (*
        async 
            {
                do! Async.Sleep 10 // 10 ms is usually enough
                actor.Post (Reset newCts)
            }
        |> Async.StartImmediate
        *)

//********************** not resumable App **************************************

    let internal globalCancellationActor =  //tady nelze IO Monad (pak se actor nespusti tak, jak je treba)

           //If no timeout or cancellation token is applied or the mailbox is not disposed (all three cases are under my control),
           //the mailbox will not raise an exception on its own. 
                           
        MailboxProcessor<CancellationMessageGlobal>
            .StartImmediate
                <|
                fun inbox
                    ->
                    let rec loop (cancelRequested : bool) (cts : CancellationTokenSource) =
                       
                        async 
                            {
                                match! inbox.Receive() with
                                | UpdateStateGlobal (newState, newCts)
                                    ->
                                    match newState with
                                    | true  ->
                                            cts.Cancel()
                                            cts.Dispose()
                                    | false -> 
                                            cts.Dispose() 

                                    return! loop newState newCts
           
                                | CheckStateGlobal reply
                                    ->
                                    let tokenOpt = 
                                        match cts.IsCancellationRequested with
                                        | true  -> None
                                        | false -> Some cts.Token
                                       
                                    reply.Reply tokenOpt

                                    return! loop cancelRequested cts

                                | CancelCurrent //zatim nevyuzito
                                    ->
                                    cts.Cancel()
                                    // Do NOT dispose or replace — keep the same CTS
                                    // When user starts new work, init2 will replace it normally
                                    return! loop true cts  
           
                                | StopGlobal reply 
                                    ->
                                    match cancelRequested with
                                    | true  -> cts.Cancel()  
                                    | false -> () 
                                       
                                    cts.Dispose()
                                    reply.Reply()
                            }

                    loop false (new CancellationTokenSource())  