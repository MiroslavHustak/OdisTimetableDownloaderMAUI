namespace OdisTimetableDownloaderMAUI

open System

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Storage
open Microsoft.Maui.Graphics
open Microsoft.Maui.Primitives
open Microsoft.Maui.Accessibility

open type Fabulous.Maui.View

open Types
open Types.Types

open ProgressCircle

open MainFunctions.WebScraping_DPO
open MainFunctions.WebScraping_MDPO
open MainFunctions.WebScraping_KODISFMDataTable

module App =

    type ProgressIndicator = 
        | Idle 
        | InProgress of percent: float*float

    type Model = 
        {
            ResultMsg: string
            ProgressIndicator: ProgressIndicator
            Progress: float // New property to hold the progress value
        }

    type Msg =
        | Kodis  
        | Dpo
        | Mdpo
        | UpdateStatus of progress: float * float
        | WorkIsComplete of string //TODO predelat na Result
        | IterationMessage of string

    let init () =
        { 
            ResultMsg = String.Empty
            ProgressIndicator = Idle
            Progress = 0.0 // Initialize progress
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
             { m with ProgressIndicator = InProgress (progressValue, totalProgress); Progress = progress }, Cmd.none

        | WorkIsComplete result 
            ->
             { m with ResultMsg = result; ProgressIndicator = Idle; Progress = 0.0 }, Cmd.none

        | IterationMessage message 
            ->
             { m with ResultMsg = message }, Cmd.none         

        | Kodis 
            -> 
             let path =
                 //@"/storage/emulated/0/FabulousTimetables/"
                 @"c:\Users\User\Data\"

             let delayedCmd1 (dispatch: Msg -> unit): Async<unit> =
                 async
                     {
                         let reportProgress (progressValue, totalProgress) =
                             dispatch (UpdateStatus (progressValue, totalProgress)) 
                             
                         let! hardWork = 
                             Async.StartChild 
                                 (
                                    async 
                                        {
                                            return 
                                                stateReducerCmd1
                                                <| path
                                                <| fun _ -> ()
                                                <| fun _ -> ()
                                                <| reportProgress
                                        }
                                 )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete "Dokončeno stahování JSON souborů.") //"Dokončeno stahování JSON souborů. Chvíli strpení, prosím ..."
                     }  

             let delayedCmd2 (dispatch: Msg -> unit): Async<unit> =  
                 async 
                     {
                         let reportProgress (progressValue, totalProgress) =
                             dispatch (UpdateStatus (progressValue, totalProgress))  

                         //TODO result type     
                         let! hardWork = 
                             Async.StartChild 
                                 (
                                     async 
                                         {                                    
                                             stateReducerCmd2
                                             <| path
                                             <| fun message -> dispatch (WorkIsComplete message)
                                             <| fun message -> dispatch (IterationMessage message) 
                                             <| reportProgress

                                             return "Kompletní JŘ ODIS úspěšně staženy." 
                                         }
                                 )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete result)
                     }     
                     
             let executeSequentially (dispatch: Msg -> unit) =
                 async 
                     {
                         do! delayedCmd1 dispatch 
                         do! delayedCmd2 dispatch 
                     }
                 |> Async.StartImmediate
            
             { 
                 m with       //TODO predelat                           
                     ResultMsg = "Stahují se JSON soubory potřebné pro stahování JŘ ODIS" 
                     ProgressIndicator = InProgress (0.0, 0.0)
             }, Cmd.ofSub executeSequentially        
          
        | Dpo 
            -> 
             let path =
                 //@"/storage/emulated/0/FabulousTimetables/"
                 @"c:\Users\User\Data\"

             let delayedCmd (dispatch: Msg -> unit): Async<unit> =
                 async
                     {
                         let reportProgress (progressValue, totalProgress) =
                             dispatch (UpdateStatus (progressValue, totalProgress)) 
                                
                         let! hardWork = 
                             Async.StartChild 
                                 (
                                     async 
                                         {
                                             dispatch (IterationMessage "Stahují se aktuálně platné JŘ DPO")

                                             webscraping_DPO reportProgress path

                                             return "JŘ DPO úspěšně staženy." //TODO result type
                                         }
                                 )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete result)
                     }   
                    
             let execute (dispatch: Msg -> unit) =
                 async { do! delayedCmd dispatch } |> Async.StartImmediate

             { 
                 m with                                  
                     ResultMsg = "JŘ DPO úspěšně staženy."
                     ProgressIndicator = InProgress (0.0, 0.0)
             }, Cmd.ofSub execute     

        | Mdpo 
            -> 
             let path =
                 //@"/storage/emulated/0/FabulousTimetables/"
                 @"c:\Users\User\Data\"

             let delayedCmd (dispatch: Msg -> unit): Async<unit> =
                 async
                     {
                         let reportProgress (progressValue, totalProgress) =
                             dispatch (UpdateStatus (progressValue, totalProgress)) 
                                    
                         let! hardWork = 
                             Async.StartChild 
                                 (
                                     async 
                                         {
                                             dispatch (IterationMessage "Stahují se zastávkové JŘ MDPO ...") 

                                             webscraping_MDPO reportProgress path

                                             return "Zastávkové JŘ MDPO úspěšně staženy." //TODO result type
                                         }
                                 )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete result)
                     }   
                        
             let execute (dispatch: Msg -> unit) =
                 async { do! delayedCmd dispatch } |> Async.StartImmediate

             { 
                 m with                                  
                     ResultMsg = "Zastávkové JŘ MDPO úspěšně staženy."
                     ProgressIndicator = InProgress (0.0, 0.0)
             }, Cmd.ofSub execute                     

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
    
                        Label("Stahování JŘ ODIS")
                            .semantics(SemanticHeadingLevel.Level1)
                            .font(size = 26.)
                            .centerTextHorizontal()
    
                        Label(m.ResultMsg)
                            .semantics(SemanticHeadingLevel.Level2, "Welcome to dot net Multi platform App U I powered by Fabulous")
                            .font(size = 14.)
                            .centerTextHorizontal()
    
                        Button("Stahuj kompletní balík JŘ ODIS", Kodis)
                            .semantics(hint = "Stahování kompletních JŘ ODIS všech dopravců")
                            .centerHorizontal()
    
                        Button("Stahuj JŘ dopravce DPO", Dpo)
                            .semantics(hint = "Stahování aktuálních JŘ dopravce DPO")
                            .centerHorizontal()
    
                        Button("Stahuj JŘ dopravce MDPO", Mdpo)
                            .semantics(hint = "Stahování zastávkových JŘ dopravce MDPO")
                            .centerHorizontal()
                    })
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
    
    let program = 
        Program.statefulWithCmd init update view
