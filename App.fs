namespace OdisTimetableDownloaderMAUI

open System
open System.IO

open Fabulous
open Fabulous.Maui

open Microsoft.Maui
open Microsoft.Maui.Graphics
open Microsoft.Maui.Primitives
open Microsoft.Maui.Accessibility

open type Fabulous.Maui.View

open Types
open Microsoft.Maui.Storage

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
        },
        Cmd.none

    let update msg m =
        match msg with
        | UpdateStatus (progressValue, totalProgress) 
            -> 
             { m with ProgressIndicator = InProgress (progressValue, totalProgress) }, Cmd.none 
        | WorkIsComplete result 
            -> 
             { m with ResultMsg = result; ProgressIndicator = Idle }, Cmd.none 

        | Kodis2 
            -> m, Cmd.none  
             (*
             let path = @"/storage/emulated/0/FabulousTimetables/"
             //let path = @"c:\Users\User\Data\"
                          
             let delayedCmd (dispatch: Msg -> unit): unit =  
                 let delayedDispatch: Async<unit> =   
                     async
                         {
                             let reportProgress (progressValue, totalProgress) = dispatch (UpdateStatus (progressValue, totalProgress))   
                                       
                             let! hardWork = 
                                 Async.StartChild 
                                     (
                                        async 
                                            {  

                                                let jsonDownload () = 
                                                    KODIS_SubmainDataTable.downloadAndSaveJson
                                                    <| (jsonLinkList @ jsonLinkList2) 
                                                    <| (pathToJsonList @ pathToJsonList2) 
                                                    <| reportProgress
                                                  
                                                jsonDownload () |> ignore
                                                 
                                                KODIS_SubmainDataTable.deleteAllODISDirectories path

                                                let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4
                                                               
                                                KODIS_SubmainDataTable.createFolders dirList      
                                                ([ CurrentValidity; FutureValidity; WithoutReplacementService ], dirList) //lze aji po jednom,pokud to bude nutne
                                                ||> List.iter2 
                                                    (fun variant dir 
                                                        ->               
                                                         KODIS_SubmainDataTable.operationOnDataFromJson variant dir 
                                                         |> KODIS_SubmainDataTable.downloadAndSave reportProgress dir   
                                                    )                                                            
                                                   
                                                return "Kompletn\u00ED J\u0158 ODIS \u00FAsp\u011B\u0161n\u011B sta\u017Eeny." 
                                            }
                                     ) 
                             let! result = hardWork 
                             do! Async.Sleep 1000

                             dispatch (WorkIsComplete result)
                         }     
                 Async.StartImmediate delayedDispatch                                                    
                                    
             { m with ResultMsg = "Wait....."; ProgressIndicator = InProgress (0.0, 0.0) }, Cmd.ofSub delayedCmd                
             *)       
             
        | Kodis 
            ->   
             //let path = @"/storage/emulated/0/FabulousTimetables/"
             let path = @"c:\Users\User\Data\"

             let delayedCmd1 (dispatch: Msg -> unit): Async<unit> =
                        
                 async
                     {
                         let reportProgress (progressValue, totalProgress) = dispatch (UpdateStatus (progressValue, totalProgress))   
                                    
                         let! hardWork = 
                             Async.StartChild 
                                 (async 
                                     {
                                         return
                                             KODIS_SubmainDataTable.downloadAndSaveJson
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
                         let reportProgress (progressValue, totalProgress) = dispatch (UpdateStatus (progressValue, totalProgress))   
                                
                         let! hardWork = 
                             Async.StartChild 
                                 (async 
                                     {  
                                         let dirList = KODIS_SubmainDataTable.createNewDirectories path listODISDefault4
                                                        
                                         KODIS_SubmainDataTable.createFolders dirList      
                                         ([ CurrentValidity; FutureValidity; WithoutReplacementService ], dirList) //lze aji po jednom,pokud to bude nutne
                                         ||> List.iter2 
                                             (fun variant dir 
                                                 ->               
                                                  KODIS_SubmainDataTable.operationOnDataFromJson variant dir 
                                                  |> KODIS_SubmainDataTable.downloadAndSave reportProgress dir   
                                             )                                                            
                                            
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
               
        | Dpo  ->                      
                let result =                 
                    try
                        let path = @"/storage/emulated/0/FabulousTimetables/"
                        webscraping_DPO path 

                        "J\u0158 DPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny."                           
                    with
                    | ex -> sprintf "Error: %s" ex.Message      
                { m with ResultMsg = result }, Cmd.none    
                    
        | Mdpo ->                 
                let result =                 
                    try
                        let path = @"/storage/emulated/0/FabulousTimetables/"
                        webscraping_MDPO path

                        "Zast\u00E1vkov\u00E9 J\u0158 MDPO \u00FAsp\u011B\u0161n\u011B sta\u017Eeny."
                    with
                    | ex -> sprintf "Error: %s" ex.Message      
                { m with ResultMsg = result }, Cmd.none    

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
                            .height(20.)
                            .width(200.)
                            .semantics(description = "Progress Bar")
                                                                              
                        Label("Timetable Downloader")
                            .semantics(SemanticHeadingLevel.Level1)
                            .font(size = 26.)
                            .centerTextHorizontal()                   

                        Label(m.ResultMsg)
                            .semantics(SemanticHeadingLevel.Level2, "Welcome to dot net Multi platform App U I powered by Fabulous")
                            .font(size = 14.)
                            .centerTextHorizontal()
                        (*
                        Button("Complete ODIS Timetables 2", Kodis2)
                            .semantics(hint = "Download complete ODIS timetables 2")
                            .centerHorizontal()
                        *)
                        
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