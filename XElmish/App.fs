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
        | Kodis2  
        | Kodis  
        | Dpo
        | Mdpo
        | UpdateStatus of progress: float*float
        | WorkIsComplete of string //TODO predelat na Result

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
             let progress = (1.0 / totalProgress) * progressValue
             { m with ProgressIndicator = InProgress (progressValue, totalProgress); Progress = progress }, Cmd.none
    
        | WorkIsComplete result 
            ->
             { m with ResultMsg = result; ProgressIndicator = Idle; Progress = 0.0 }, Cmd.none
    
        | Kodis2 
            -> 
             m, Cmd.none  

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
                                          return KODIS_SubmainDataTable.downloadAndSaveJson
                                              <| (jsonLinkList @ jsonLinkList2) 
                                              <| (pathToJsonList @ pathToJsonList2) 
                                              <| reportProgress
                                      }
                                  )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete result)
                     }      
                     
             let delayedCmd2 (dispatch: Msg -> unit): Async<unit> =  
                 async
                     {
                         let! hardWork = 
                             Async.StartChild 
                                 (async
                                      {
                                          KODIS_SubmainDataTable.deleteAllODISDirectories path                                                      
                                          return "Chv\u00EDli strpen\u00ED pros\u00EDm, za\u010Dalo stahov\u00E1n\u00ED J\u0158 a bude to trvat n\u011Bkolik minut ..." 
                                      }
                                  )
                         let! result = hardWork 
                         do! Async.Sleep 1000

                         dispatch (WorkIsComplete result)
                     }  

             let delayedCmd3 (dispatch: Msg -> unit): Async<unit> =  
                 async 
                     {
                         let reportProgress (progressValue, totalProgress) =
                             dispatch (UpdateStatus (progressValue, totalProgress))  
                             
                         let! hardWork = 
                             Async.StartChild 
                                 (async 
                                     {
                                         let dt = DataTable.CreateDt.dt()   
                                         let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4

                                         KODIS_SubmainDataTable.createFolders dirList   
                                         
                                         ([ CurrentValidity; FutureValidity; WithoutReplacementService ], dirList)
                                         ||> List.iter2 
                                             (fun variant dir ->
                                                 KODIS_SubmainDataTable.operationOnDataFromJson dt variant dir 
                                                 |> KODIS_SubmainDataTable.downloadAndSave reportProgress dir)

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
                         do! delayedCmd2 dispatch 
                         do! delayedCmd3 dispatch
                     }
                 |> Async.StartImmediate
             { m with ResultMsg = "Chv\u00EDli strpen\u00ED pros\u00EDm, za\u010Dalo stahov\u00E1n\u00ED JSON soubor\u016F pot\u0159ebn\u00FDch pro stahov\u00E1n\u00ED J\u0158 a bude to trvat n\u011Bkolik minut ..."; ProgressIndicator = InProgress (0.0, 0.0) }, Cmd.ofSub executeSequentially                
        
        | Dpo 
            ->                      
             let result =                 
                 try
                     let path = @"/storage/emulated/0/FabulousTimetables/"
                     webscraping_DPO path
                     "J\u0158 DPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny."                           
                 with
                 | ex -> sprintf "Error: %s" ex.Message      
             { m with ResultMsg = result }, Cmd.none    
        
        | Mdpo 
            ->                 
             let result =                 
                 try
                     let path = @"/storage/emulated/0/FabulousTimetables/"
                     webscraping_MDPO path
                     "Zast\u00E1vkov\u00E9 J\u0158 MDPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny."
                 with
                 | ex -> sprintf "Error: %s" ex.Message      
             { m with ResultMsg = result }, Cmd.none 

    let view (m : Model) =

        let progressDrawable = createProgressCircle(m.Progress)

        Application(
            ContentPage(
                ScrollView(
                    (VStack(spacing = 25.) {
                        // Progress circle
                        GraphicsView(progressDrawable)
                            .height(150.)
                            .width(150.)
    
                        Label("Timetable Downloader")
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