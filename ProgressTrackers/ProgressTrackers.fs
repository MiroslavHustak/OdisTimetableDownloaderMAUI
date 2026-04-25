namespace Helpers

open Types
open Types.Types

module ProgressValues = 

    let internal counterAndProgressBar l token checkCancel reportProgress =

        MailboxProcessor<MsgIncrement>.StartImmediate
            (fun inbox 
                ->
                let rec loop n =
                    async 
                        {
                            try
                                checkCancel token      
                                let! msg = inbox.Receive()  
                                    
                                match msg with
                                | Inc i 
                                    -> 
                                    reportProgress (float n, float l)
                                    return! loop (n + i)
                                | Stop
                                    ->
                                    return () // exit loop → agent terminates
                                | StopAndReply reply 
                                    ->
                                    reply.Reply()
                                    return ()
                            with
                            | ex -> () //runIO (postToLog2 <| string ex.Message <| "#0001-KBLJson")
                        }
                loop 0
            )
                
    let internal counterAndProgressBar2 l token checkCancel reportProgress =

        MailboxProcessor<MsgIncrement2>.StartImmediate 
            <|
            fun inbox 
                ->
                let rec loop n =
                    async
                        {
                            try
                                checkCancel token
                                let! msg = inbox.Receive()

                                match msg with
                                | Inc2 i 
                                    ->
                                    reportProgress (float n, float l)
                                    return! loop (n + i)

                                | GetCount2 replyChannel //not used anymore, kept for educational purposes
                                    ->
                                    replyChannel.Reply n
                                    return! loop n

                                | Stop2AndReply reply
                                    ->
                                    reply.Reply()
                                    return ()
                                
                                | Stop2  
                                    ->
                                    return ()
                            with
                            | ex -> () //runIO (postToLog2 <| string ex.Message <| "#0013-KBL")
                        }
                loop 0