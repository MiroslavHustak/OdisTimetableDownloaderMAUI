namespace OdisTimetableDownloaderMAUI

open System
open System.Threading
open System.Net.NetworkInformation

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Storage
open Microsoft.Maui.Graphics
open Microsoft.Maui.Primitives
open Microsoft.Maui.Accessibility

open type Fabulous.Maui.View

//**********************************

open ProgressCircle

open Settings.Messages
open Settings.SettingsGeneral

open Helpers.CheckNetConnection

open MainFunctions.WebScraping_DPO
open MainFunctions.WebScraping_MDPO
open MainFunctions.WebScraping_KODISFMRecord

module App =

    type ProgressIndicator = 
        | Idle 
        | InProgress of percent : float * float

    type Model = 
        {
            ProgressMsg : string
            ProgressIndicator : ProgressIndicator
            Progress : float 
            KodisEnabled : bool
            DpoEnabled : bool
            MdpoEnabled : bool
            CancelEnabled : bool
            KodisVisible : bool
            DpoVisible : bool
            MdpoVisible : bool
            CancelVisible : bool
            Cts : CancellationTokenSource
        }

    type Msg =
        | Kodis  
        | Dpo
        | Mdpo
        | Cancel
        | UpdateStatus of progress : float * float
        | WorkIsComplete of msgAndEnabled : string * bool * bool 
        | IterationMessage of string    
       
    let init () =

        { 
            ProgressMsg = String.Empty
            ProgressIndicator = Idle
            Progress = 0.0 // Initialize progress
            KodisEnabled = true
            DpoEnabled = true
            MdpoEnabled = true
            CancelEnabled = false
            KodisVisible = true
            DpoVisible = true
            MdpoVisible = true
            CancelVisible = false
            Cts = new CancellationTokenSource() 
        },
        Cmd.none
    
    let update msg m =

        match msg with      
        | UpdateStatus (progressValue, totalProgress)
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
             }, 
             Cmd.none

        | WorkIsComplete (result, enabled1, enabled2)
            ->
             {
                m with 
                    ProgressMsg = result
                    ProgressIndicator = Idle
                    Progress = 0.0
                    KodisEnabled = enabled1
                    DpoEnabled = enabled1
                    MdpoEnabled = enabled1 
                    CancelEnabled = enabled2
                    KodisVisible = enabled1
                    DpoVisible = enabled1
                    MdpoVisible = enabled1
                    CancelVisible = enabled2
             }, 
             Cmd.none

        | IterationMessage message 
            ->
             { m with ProgressMsg = message }, Cmd.none          

        | Kodis 
            ->          
             let newCts = new CancellationTokenSource()
             
             let path = kodisPathTemp

             let delayedCmd0 (dispatch : Msg -> unit) : Async<unit> =

                 async
                     {       
                         let! result = SubmainFunctions.KODIS_SubmainRecords.downloadAndSaveJsonTest ()  
                         do! Async.Sleep 1000                                  
                         dispatch (WorkIsComplete (result, false, true))                         
                     }  
                 
             let delayedCmd1 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                 async
                     {
                         //async tady ignoruje async code obsahujici Request.sendAsync request/result (FsHttp)
                         match checkInternetConnectivity () with
                         | Some _ -> 
                                   let reportProgress (progressValue, totalProgress) =
                                       dispatch (UpdateStatus (progressValue, totalProgress)) 
                             
                                   let! hardWork =                                                              
                                        async 
                                            {
                                                return 
                                                    stateReducerCmd1
                                                    <| token
                                                    <| path
                                                    <| fun _ -> ()
                                                    <| fun _ -> ()
                                                    <| reportProgress
                                            }
                                        |> Async.StartChild

                                   let! result = hardWork 
                                   do! Async.Sleep 1000
                                  
                                   dispatch (WorkIsComplete (result, false, true))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true, false))
                     }  

             let delayedCmd2 (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  

                 async 
                     {   
                         match checkInternetConnectivity () with
                         | Some _ -> 
                                   let reportProgress (progressValue, totalProgress) =
                                       dispatch (UpdateStatus (progressValue, totalProgress))  

                                   let! hardWork =                             
                                       async 
                                           {   
                                               return
                                                   stateReducerCmd2
                                                   <| token
                                                   <| path
                                                   <| fun message -> dispatch (WorkIsComplete (message, false, true))
                                                   <| fun message -> dispatch (IterationMessage message) 
                                                   <| reportProgress            
                                           }
                                       |> Async.StartChild 
                               
                                   let! result = hardWork 
                                   do! Async.Sleep 1000

                                   dispatch (WorkIsComplete (result, true, false))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true, false))   
                     }     

             let delayedCmdCancelMessage (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  
             
                 async 
                     {   
                         dispatch (WorkIsComplete (cancelMsg2, true, false))
                     }                    
                     
             let executeSequentially (dispatch : Msg -> unit) =

                 async 
                     {
                         //do! delayedCmd0 dispatch                     
                         let token = newCts.Token
                         
                         do! delayedCmd1 token dispatch                          
                        
                         match token.IsCancellationRequested with
                         | true  ->
                                  do! delayedCmdCancelMessage token dispatch                                   
                         | false ->
                                  do! Async.Sleep 2000
                                  do! delayedCmd2 token dispatch    
                                  match token.IsCancellationRequested with
                                  | true  ->
                                           do! delayedCmdCancelMessage token dispatch                                   
                                  | false ->
                                           ()    
                     }
                 |> Async.StartImmediate
            
             { 
                 m with                               
                     ProgressMsg = progressMsgKodis 
                     ProgressIndicator = InProgress (0.0, 0.0)
                     KodisEnabled = false
                     DpoEnabled = false
                     MdpoEnabled = false
                     CancelEnabled = true
                     KodisVisible = false
                     DpoVisible = false
                     MdpoVisible = false
                     CancelVisible = true
                     Cts = newCts  // Update the cts
             }, 
             Cmd.ofSub executeSequentially        
          
        | Dpo 
            -> 
             let newCts = new CancellationTokenSource()
             
             let path = dpoPathTemp
                 
             let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                 async
                     {
                         match checkInternetConnectivity () with
                         | Some _ -> 
                                   let reportProgress (progressValue, totalProgress) =
                                       dispatch (UpdateStatus (progressValue, totalProgress)) 
                                
                                   let! hardWork =                            
                                       async 
                                           {
                                               match webscraping_DPO reportProgress token path with
                                               | Ok _      -> return mauiDpoMsg 
                                               | Error err -> return err
                                           }
                                       |> Async.StartChild 
                               
                                   let! result = hardWork 
                                   do! Async.Sleep 1000

                                   dispatch (WorkIsComplete (result, true, false))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true, false))
                     }  
                     
             let execute dispatch = 
                 async 
                     { 
                         let token = newCts.Token
                         do! delayedCmd newCts.Token dispatch
                     } 
                 |> Async.StartImmediate

             { 
                 m with                                  
                     ProgressMsg = progressMsgDpo 
                     ProgressIndicator = InProgress (0.0, 0.0)
                     KodisEnabled = false
                     DpoEnabled = false
                     MdpoEnabled = false
                     CancelEnabled = true
                     KodisVisible = false
                     DpoVisible = false
                     MdpoVisible = false
                     CancelVisible = true
                     Cts = newCts
             },
             Cmd.ofSub execute     

        | Mdpo 
            -> 
             let newCts = new CancellationTokenSource()
             
             let path = mdpoPathTemp
            
             let delayedCmd (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =

                 async
                     {
                         match checkInternetConnectivity () with
                         | Some _ -> 
                                   let reportProgress (progressValue, totalProgress) =
                                       dispatch (UpdateStatus (progressValue, totalProgress)) 
                                
                                   let! hardWork =                            
                                       async 
                                           {
                                               match webscraping_MDPO reportProgress token path with
                                               | Ok _      -> return mauiMdpoMsg 
                                               | Error err -> return err
                                           }
                                       |> Async.StartChild 
                               
                                   let! result = hardWork 
                                   do! Async.Sleep 1000

                                   dispatch (WorkIsComplete (result, true, false))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true, false))
                     }     
                        
             //let execute dispatch = async { do! delayedCmd dispatch } |> Async.StartImmediate
             let execute dispatch = 
                 async 
                     { 
                         let token = newCts.Token
                         do! delayedCmd newCts.Token dispatch
                     } 
                 |> Async.StartImmediate

             { 
                 m with                                  
                     ProgressMsg = progressMsgMdpo 
                     ProgressIndicator = InProgress (0.0, 0.0)
                     KodisEnabled = false
                     DpoEnabled = false
                     MdpoEnabled = false
                     CancelEnabled = true
                     KodisVisible = false
                     DpoVisible = false
                     MdpoVisible = false
                     CancelVisible = true
                     Cts = newCts
             }, 
             Cmd.ofSub execute   
             
        | Cancel 
            ->    
             m.Cts.Cancel()
             
             // Create a new CancellationTokenSource for future use
             let newCts = new CancellationTokenSource()

             let delayedCmdCancel (token : CancellationToken) (dispatch : Msg -> unit) : Async<unit> =  
                 async 
                     {   
                         dispatch (WorkIsComplete (cancelMsg1, false, false))
                     }                    

             let execute dispatch = async { do! delayedCmdCancel newCts.Token dispatch } |> Async.StartImmediate  
             
             { 
                 m with
                     ProgressMsg = String.Empty
                     ProgressIndicator = Idle
                     Progress = 0.0
                     KodisEnabled = true
                     DpoEnabled = true
                     MdpoEnabled = true
                     CancelEnabled = false
                     KodisVisible = true
                     DpoVisible = true
                     MdpoVisible = true
                     CancelVisible = false
                     Cts = newCts  // Replace the old cts with a new one
             },
             Cmd.ofSub execute

    let view (m : Model) =

        let progressDrawable = progressCircle m.Progress
    
        Application(
            ContentPage(
                ScrollView(
                    (VStack(spacing = 25.) {
                        // Progress circle
                        GraphicsView(progressDrawable)
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
    
                        Button(buttonKodis, Kodis)
                            .semantics(hint = hintOdis)
                            .centerHorizontal()
                            .isEnabled(m.KodisEnabled)
                            .isVisible(m.KodisVisible)
    
                        Button(buttonDpo, Dpo)
                            .semantics(hint = hintDpo)
                            .centerHorizontal()
                            .isEnabled(m.DpoEnabled)
                            .isVisible(m.DpoVisible)
    
                        Button(buttonMdpo, Mdpo)
                            .semantics(hint = hintMdpo)
                            .centerHorizontal()
                            .isEnabled(m.MdpoEnabled)
                            .isVisible(m.MdpoVisible)

                        Button("Cancel", Cancel)
                            .semantics(hint = hintCancel)
                            .centerHorizontal()
                            .isEnabled(m.CancelEnabled)
                            .isVisible(m.CancelVisible)
                    })
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
    
    let program = 
        Program.statefulWithCmd init update view