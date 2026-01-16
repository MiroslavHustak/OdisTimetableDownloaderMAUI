namespace OdisTimetableDownloaderMAUI

open System.Threading

//*************************************

open Types.Types

module ActorModels =  

//********************** resumable App_R **************************************

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
    
                                | Stop reply 
                                    ->
                                    cts.Cancel()
                                    cts.Dispose()
                                    reply.Reply ()
                            }
    
                    loop (new CancellationTokenSource())
                )

    let internal cancelLocalActor (actor : MailboxProcessor<CancellationMessageLocal>) =
        
        let newCts = new CancellationTokenSource()
    
        actor.Post CancelToken
        actor.Post (Reset newCts)

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
                                | UpdateState2 (newState, newCts)
                                    ->
                                    match newState with
                                    | true  ->
                                            cts.Cancel()
                                            cts.Dispose()
                                    | false -> 
                                            cts.Dispose() 

                                    return! loop newState newCts
           
                                | CheckState2 reply
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
           
                                | Stop2 reply 
                                    ->
                                    match cancelRequested with
                                    | true  -> cts.Cancel()  
                                    | false -> () 
                                       
                                    cts.Dispose()
                                    reply.Reply ()
                            }

                    loop false (new CancellationTokenSource())  