namespace OdisTimetableDownloaderMAUI

open System
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
        }

    type Msg =
        | Kodis  
        | Dpo
        | Mdpo
        | UpdateStatus of progress : float * float
        | WorkIsComplete of msgAndEnabled : string * bool 
        | IterationMessage of string      

    let init () =
        { 
            ProgressMsg = String.Empty
            ProgressIndicator = Idle
            Progress = 0.0 // Initialize progress
            KodisEnabled = true
            DpoEnabled = true
            MdpoEnabled = true
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

        | WorkIsComplete (result, enabled)
            ->
             {
                m with 
                    ProgressMsg = result
                    ProgressIndicator = Idle
                    Progress = 0.0
                    KodisEnabled = enabled
                    DpoEnabled = enabled
                    MdpoEnabled = enabled 
             }, 
             Cmd.none

        | IterationMessage message 
            ->
             { m with ProgressMsg = message }, Cmd.none         

        | Kodis 
            -> 
             let path = kodisPathTemp
                 
             let delayedCmd1 (dispatch : Msg -> unit) : Async<unit> =

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
                                                    stateReducerCmd1
                                                    <| path
                                                    <| fun _ -> ()
                                                    <| fun _ -> ()
                                                    <| reportProgress
                                            }
                                        |> Async.StartChild

                                   let! result = hardWork 
                                   do! Async.Sleep 1000
                                  
                                   dispatch (WorkIsComplete (result, false))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true))
                     }  

             let delayedCmd2 (dispatch : Msg -> unit) : Async<unit> =  

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
                                                   <| path
                                                   <| fun message -> dispatch (WorkIsComplete (message, false))
                                                   <| fun message -> dispatch (IterationMessage message) 
                                                   <| reportProgress            
                                           }
                                       |> Async.StartChild 
                               
                                   let! result = hardWork 
                                   do! Async.Sleep 1000

                                   dispatch (WorkIsComplete (result, true))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true))   
                     }     
                     
             let executeSequentially (dispatch : Msg -> unit) =

                 async 
                     {
                         do! delayedCmd1 dispatch 
                         do! Async.Sleep 2000
                         do! delayedCmd2 dispatch 
                     }
                 |> Async.StartImmediate
            
             { 
                 m with                               
                     ProgressMsg = progressMsgKodis 
                     ProgressIndicator = InProgress (0.0, 0.0)
                     KodisEnabled = false
                     DpoEnabled = false
                     MdpoEnabled = false
             }, 
             Cmd.ofSub executeSequentially        
          
        | Dpo 
            -> 
             let path = dpoPathTemp
                 
             let delayedCmd (dispatch : Msg -> unit) : Async<unit> =

                 async
                     {
                         match checkInternetConnectivity () with
                         | Some _ -> 
                                   let reportProgress (progressValue, totalProgress) =
                                       dispatch (UpdateStatus (progressValue, totalProgress)) 
                                
                                   let! hardWork =                            
                                       async 
                                           {
                                               match webscraping_DPO reportProgress path with
                                               | Ok _      -> return mauiDpoMsg 
                                               | Error err -> return err
                                           }
                                       |> Async.StartChild 
                               
                                   let! result = hardWork 
                                   do! Async.Sleep 1000

                                   dispatch (WorkIsComplete (result, true))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true))
                     }  
                     
             let execute dispatch = async { do! delayedCmd dispatch } |> Async.StartImmediate

             { 
                 m with                                  
                     ProgressMsg = progressMsgDpo 
                     ProgressIndicator = InProgress (0.0, 0.0)
                     KodisEnabled = false
                     DpoEnabled = false
                     MdpoEnabled = false
             },
             Cmd.ofSub execute     

        | Mdpo 
            -> 
             let path = mdpoPathTemp
            
             let delayedCmd (dispatch : Msg -> unit) : Async<unit> =

                 async
                     {
                         match checkInternetConnectivity () with
                         | Some _ -> 
                                   let reportProgress (progressValue, totalProgress) =
                                       dispatch (UpdateStatus (progressValue, totalProgress)) 
                                
                                   let! hardWork =                            
                                       async 
                                           {
                                               match webscraping_MDPO reportProgress path with
                                               | Ok _      -> return mauiMdpoMsg 
                                               | Error err -> return err
                                           }
                                       |> Async.StartChild 
                               
                                   let! result = hardWork 
                                   do! Async.Sleep 1000

                                   dispatch (WorkIsComplete (result, true))
                         | None   -> 
                                   dispatch (WorkIsComplete (noNetConn, true))
                     }     
                        
             let execute dispatch = async { do! delayedCmd dispatch } |> Async.StartImmediate

             { 
                 m with                                  
                     ProgressMsg = progressMsgMdpo 
                     ProgressIndicator = InProgress (0.0, 0.0)
                     KodisEnabled = false
                     DpoEnabled = false
                     MdpoEnabled = false
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
    
                        Button(buttonDpo, Dpo)
                            .semantics(hint = hintDpo)
                            .centerHorizontal()
                            .isEnabled(m.DpoEnabled)
    
                        Button(buttonMdpo, Mdpo)
                            .semantics(hint = hintMdpo)
                            .centerHorizontal()
                            .isEnabled(m.MdpoEnabled)
                    })
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
    
    let program = 
        Program.statefulWithCmd init update view