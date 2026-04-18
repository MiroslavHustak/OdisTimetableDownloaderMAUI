module OdisTimetableDownloaderMAUI.Engines.KodisCanopy

open System.Threading

open Types.Types
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open IO_Operations.IO_Operations
open Helpers.ConnectivityWithDebouncing

open ApplicationDesign4_R.WebScraping_KODIS4

open Settings.SettingsGeneral

open OdisTimetableDownloaderMAUI.ActorModels

type KodisCanopyMsg =
    | Progress of float * float
    | IterationMsg of string  
    | Completed of string
    | ErrorKodis of string
    | NavigateHome
    
let execute dispatch (token: CancellationToken) =

    let reportProgress (progressValue: float, totalProgress: float) =
        match token.IsCancellationRequested with
        | true  -> dispatch (Progress (0.0, 1.0))
        | false -> dispatch (Progress (progressValue, totalProgress))

    let cmd4 (token : CancellationToken) dispatch =
        async 
            {
                try
                    do! Async.SwitchToThreadPool()    
   
                    let computation =
                        stateReducerCmd4
                            token
                            kodisPathTemp4
                            (fun msg -> dispatch (IterationMsg msg))   
                            reportProgress
    
                    let! result = async { return runIO computation }
    
                    match token.IsCancellationRequested with 
                    | true  -> return dispatch NavigateHome
                    | false -> return dispatch (Completed result)
    
                with ex ->
                    runIO (postToLog2 ex.Message "Kodis-CMD2")
                    return dispatch (ErrorKodis ex.Message)
            }

    let cmd5 dispatch =           
        async 
            {    
                try
                    match! stateReducerCmd5 >> runIO <| () with  //cannot block the UI thread for long enough to be visible, Async.SwitchToThreadPool() not needed
                    | Ok _      -> return () 
                    | Error err -> return ErrorKodis >> dispatch <| err
                        
                with 
                | ex
                    ->
                    runIO (postToLog2 <| string ex.Message <| " #XElmish_Kodis4_Critical_Error")
                    return  dispatch (ErrorKodis ex.Message)
            }  
       
    let executeSequentially dispatch = 
        async 
            {          
                use cts = CancellationTokenSource.CreateLinkedTokenSource token
                umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs
                do! cmd4 cts.Token dispatch

                return! cmd5 dispatch                           
            }
        |> Async.Start  
    
    async 
        {   
            use cts = CancellationTokenSource.CreateLinkedTokenSource token
            umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs
                
            match cts.Token.IsCancellationRequested with 
            | true  -> return dispatch NavigateHome
            | false -> return executeSequentially dispatch
        }
    |> Async.Start                    

