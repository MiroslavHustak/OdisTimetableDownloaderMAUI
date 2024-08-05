namespace OdisTimetableDownloaderMAUI

open System
open System.IO

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Storage
open Microsoft.Maui.Graphics
open Microsoft.Maui.Primitives
open Microsoft.Maui.Accessibility

open type Fabulous.Maui.View

open Types

open ProgressCircle
open SubmainFunctions

open Settings.SettingsKODIS
open Settings.SettingsGeneral

open MainFunctions.WebScraping_DPO
open MainFunctions.WebScraping_MDPO
open SubmainFunctions.DPO_Submain
open System.Net.Http
open Helpers

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
        | UpdateStatus of progress: float*float
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
                                 (async 
                                      {
                                          //TODO result type 
                                          return KODIS_SubmainDataTable.downloadAndSaveJson
                                              <| (jsonLinkList @ jsonLinkList2) 
                                              <| (pathToJsonList @ pathToJsonList2) 
                                              <| reportProgress
                                      }
                                  )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete "Dokon\u010deno stahov\u00e1n\u00ed JSON soubor\u016f.") //"Dokonèeno stahování JSON souborù. Chvíli strpení, prosím ..."
                     }  

             let delayedCmd2 (dispatch: Msg -> unit): Async<unit> =  
                 async 
                     {
                         let reportProgress (progressValue, totalProgress) =
                             dispatch (UpdateStatus (progressValue, totalProgress))  

                         //TODO result type     
                         let! hardWork = 
                             Async.StartChild 
                                 (async 
                                     {
                                         let dt = DataTable.CreateDt.dt() 
                                         
                                         KODIS_SubmainDataTable.deleteAllODISDirectories path
                                         
                                         let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4
                                         let variantList = [ CurrentValidity; FutureValidity; WithoutReplacementService ]
                                         let msgList =
                                             [
                                                 "Stahuj\u00ED se aktu\u00E1ln\u011B platn\u00E9 J\u0158 ODIS"
                                                 "Stahuj\u00ED se J\u0158 ODIS platn\u00E9 v budoucnosti"
                                                 "Stahuj\u00ED se teoreticky dlouhodob\u011B platn\u00E9 J\u0158 ODIS"
                                             ]

                                         KODIS_SubmainDataTable.createFolders dirList   
                                         
                                         (variantList, dirList, msgList)
                                         |||> List.map3
                                             (fun variant dir message 
                                                 ->
                                                  dispatch (WorkIsComplete "Chv\u00edli strpen\u00ed, pros\u00edm ...")
                                                  
                                                  let activity = KODIS_SubmainDataTable.operationOnDataFromJson dt variant dir 

                                                  dispatch (IterationMessage message)

                                                  activity |> KODIS_SubmainDataTable.downloadAndSave reportProgress dir                                                                                                    
                                             )
                                         |> ignore

                                         //Unicode escape sequences
                                         return "Kompletn\u00ED J\u0158 ODIS \u00FAsp\u011B\u0161n\u011B sta\u017Eeny." 
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
                         do! Async.Sleep 3000
                         do! delayedCmd2 dispatch 
                     }
                 |> Async.StartImmediate
            
             { 
                 m with       //TODO predelat                           
                     ResultMsg = "Stahuj\u00ed se JSON soubory pot\u0159ebn\u00e9 pro stahov\u00e1n\u00ed J\u0158 ODIS" 
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
                                (async 
                                        {
                                            dispatch (IterationMessage "Stahuj\u00ED se aktu\u00E1ln\u011B platn\u00E9 J\u0158 DPO")

                                            webscraping_DPO reportProgress path

                                            return "J\u0158 DPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny." //TODO result type
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
                    ResultMsg = "J\u0158 DPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny."
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
                                (async 
                                        {
                                            dispatch (IterationMessage "Stahuj\u00ED se aktu\u00E1ln\u011B platn\u00E9 J\u0158 MDPO")

                                            webscraping_MDPO reportProgress path

                                            return "Zast\u00E1vkov\u00E9 J\u0158 MDPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny." //TODO result type
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
                    ResultMsg = "Zast\u00E1vkov\u00E9 J\u0158 MDPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny."
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
    
                        Label("ODIS Timetable Downloader")
                            .semantics(SemanticHeadingLevel.Level1)
                            .font(size = 26.)
                            .centerTextHorizontal()
    
                        Label(m.ResultMsg)
                            .semantics(SemanticHeadingLevel.Level2, "Welcome to dot net Multi platform App U I powered by Fabulous")
                            .font(size = 14.)
                            .centerTextHorizontal()
    
                        Button("Complete ODIS Timetables", Kodis)
                            .semantics(hint = "Download complete ODIS timetables")
                            .centerHorizontal()
    
                        Button("DPO Timetables", Dpo)
                            .semantics(hint = "Download DPO timetables")
                            .centerHorizontal()
    
                        Button("MDPO Timetables", Mdpo)
                            .semantics(hint = "Download MDPO timetables")
                            .centerHorizontal()
                    })
                        .padding(30., 0., 30., 0.)
                        .centerVertical()
                )
            )
        )
    
    let program = 
        Program.statefulWithCmd init update view

     (*     
     For lowercase characters:
     
     ì: \u011B
     š: \u0161
     è: \u010D
     ø: \u0159
     ž: \u017E
     ý: \u00FD
     á: \u00E1
     í: \u00ED
     é: \u00E9
     ó: \u00F3
     ú: \u00FA

     For uppercase characters:
     
     Ì: \u011A
     Š: \u0160
     È: \u010C
     Ø: \u0158
     Ž: \u017D
     Ý: \u00DD
     Á: \u00C1
     Í: \u00CD
     É: \u00C9
     Ó: \u00D3
     Ú: \u00DA
     Ù: \u016E  
     *)   