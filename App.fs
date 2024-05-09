namespace OdisTimetableDownloaderMAUI

open System
open System.IO

open Fabulous
open Fabulous.Maui
open Microsoft.Maui
open Microsoft.Maui.Graphics
open Microsoft.Maui.Accessibility
open Microsoft.Maui.Primitives

open type Fabulous.Maui.View

open MainFunctions.WebScraping_KODISFMDataTable

open Types
open Microsoft.Maui.Storage

open MainFunctions.WebScraping_MDPO
open MainFunctions.WebScraping_DPO

open SubmainFunctions
open Settings.SettingsKODIS
open Settings.SettingsGeneral


module App =

    type ProgressIndicator = 
        | Idle 
        | InProgress of percent: float*float

    type Model = 
        {
            ResultMsg: string
            ErrorMsg: string
            ProgressIndicator: ProgressIndicator    
        }

    type Msg =
        | Complete  
        | Dpo
        | Mdpo
        | UpdateStatus of progress: float*float
        | WorkIsComplete of string //TODO predelat na Result

    let init () =
        { 
            ResultMsg = String.Empty
            ErrorMsg = String.Empty
            ProgressIndicator = Idle
        }, Cmd.none

    let update msg m =
        match msg with
        | UpdateStatus (progressValue, totalProgress) -> { m with ProgressIndicator = InProgress (progressValue, totalProgress) }, Cmd.none 
        | WorkIsComplete result -> { m with ResultMsg = result; ProgressIndicator = Idle }, Cmd.none 
        | Complete ->   
                    //let path = @"/storage/emulated/0/FabulousTimetables/"
                    let path = @"c:\Users\User\Music\"

                    let delayedCmd1 (dispatch: Msg -> unit): Async<unit> =
                        
                        async
                            {
                                let reportProgress (progressValue, totalProgress) = dispatch (UpdateStatus (progressValue, totalProgress))   
                                    
                                let! hardWork = 
                                    Async.StartChild 
                                        (async 
                                            {
                                                return
                                                    KODIS_SubmainDataTable.downloadAndSaveJson (jsonLinkList @ jsonLinkList2) (pathToJsonList @ pathToJsonList2) reportProgress
                                            }
                                        ) 
                                let! result = hardWork 
                                do! Async.Sleep 1000

                                dispatch (WorkIsComplete result)
                            }        

                    let delayedCmd2 (dispatch: Msg -> unit): Async<unit> =  
                        
                        async
                            {
                                let reportProgress (progressValue, totalProgress) = dispatch (UpdateStatus (progressValue, totalProgress))   
                                    
                                let! hardWork = 
                                    Async.StartChild 
                                        (async 
                                            {  
                                                KODIS_SubmainDataTable.deleteAllODISDirectories path                                                              
         
                                                let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4
                                                            
                                                KODIS_SubmainDataTable.createFolders dirList      
                                                ([ CurrentValidity; FutureValidity; ReplacementService; WithoutReplacementService ], dirList)
                                                ||> List.iter2 
                                                    (fun variant dir 
                                                        ->               
                                                          KODIS_SubmainDataTable.operationOnDataFromJson variant dir 
                                                          |> KODIS_SubmainDataTable.downloadAndSave reportProgress dir   
                                                    )                                                            
                                                
                                                return "Pdf downloading finished :-)" 
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
                                // Wait for delayedCmd1 to complete before starting delayedCmd2
                                do! delayedCmd2 dispatch                          
                            } |> Async.StartImmediate
                             
                    { m with ResultMsg = "Downloading in progress ..."; ErrorMsg = ""; ProgressIndicator = InProgress (0.0, 0.0) }, 
                    Cmd.ofSub executeSequentially                

        | Dpo      ->                      
                    let result =                 
                        try
                           let path = @"/storage/emulated/0/FabulousTimetables/"
                           webscraping_DPO path 

                           "DPO timetables downloaded :-)"                           
                        with
                        | ex -> sprintf "Error: %s" ex.Message      
                    { m with ResultMsg = result; ErrorMsg = result }, Cmd.none    
                    
        | Mdpo    ->                 
                   let result =                 
                       try
                           let path = @"/storage/emulated/0/FabulousTimetables/"
                           webscraping_MDPO path

                           "MDPO timetables downloaded :-)"
                       with
                       | ex -> sprintf "Error: %s" ex.Message      
                   { m with ResultMsg = result; ErrorMsg = result }, Cmd.none    

    let view (m : Model) =

        Application(
            ContentPage(
                ScrollView(
                    (VStack(spacing = 25.) {
                        
                        ProgressBar(
                            match m.ProgressIndicator with
                            | Idle         
                                -> 
                                 0.0
                            | InProgress (currentProgress, totalProgress) 
                                -> 
                                 let barFill = (1.0 / totalProgress) * currentProgress
                                 barFill                             
                            )
                            .progressColor(Color.FromArgb("FF0000FF"))
                            .height(50.)
                            .width(200.)
                            .semantics(description = "Progress Bar")
                                                                              
                        Label("Timetable Downloader")
                            .semantics(SemanticHeadingLevel.Level1)
                            .font(size = 32.)
                            .centerTextHorizontal()                   

                        Label(m.ResultMsg)
                            .semantics(SemanticHeadingLevel.Level2, "Welcome to dot net Multi platform App U I powered by Fabulous")
                            .font(size = 14.)
                            .centerTextHorizontal()
               
                        Button("Complete ODIS Timetables", Complete)
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