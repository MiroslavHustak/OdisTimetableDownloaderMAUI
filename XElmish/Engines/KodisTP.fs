module OdisTimetableDownloaderMAUI.Engines.KodisTP

open System.Threading

open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open Helpers.ExceptionHelpers
open ApplicationDesign_R.WebScraping_KODIS

open Settings.SettingsGeneral

type KodisTPMsg =
    | Progress of float * float
    | IterationMsg of string  
    | Completed of string
    | ErrorKodis of string
    | NavigateHome
    | NoInternet 

let internal executeJson dispatch (token : CancellationToken) =
   
    async
        {
            use cts = CancellationTokenSource.CreateLinkedTokenSource token  

            try               
                let token2 = cts.Token

                let inline reportProgress (progressValue : float, totalProgress : float) =
                    match token2.IsCancellationRequested with
                    | true  -> dispatch (Progress (0.0, 1.0))
                    | false -> dispatch (Progress (progressValue, totalProgress))
                
                match token2.IsCancellationRequested with
                | true 
                    ->
                    dispatch NavigateHome
                | false 
                    ->
                    match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                    | false
                        ->
                        return dispatch NoInternet
                    | true
                        ->
                        do! Async.SwitchToThreadPool()                       
                        umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs

                        let! result = async { return runIO (stateReducerCmd1 token2 stateDefault reportProgress) }

                        match token2.IsCancellationRequested with
                        | true 
                            ->
                            return dispatch NavigateHome
                        | false 
                            ->
                            match result with
                            | Ok msg    -> return Completed >> dispatch <| msg
                            | Error err -> return ErrorKodis >> dispatch <| err
            with
            | ex ->
                match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError cts.Token ex with
                | err 
                    when err = StopDownloading
                    ->
                    runIO (postToLog2 <| string ex.Message <| " StopDownloading #9999 Kodis TP")

                    match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                    | false -> return dispatch NoInternet
                    | true  -> return dispatch NavigateHome
                | _ ->
                    runIO (postToLog2 <| string ex.Message <| " #XElmish_Kodis_Critical_Error_Json")
                    return dispatch NoInternet
        }
    
let internal executePdf dispatch (token : CancellationToken) =

    async
        {
            try
                use cts = CancellationTokenSource.CreateLinkedTokenSource token

                let token2 = cts.Token

                let inline reportProgress (progressValue : float, totalProgress : float) =
                    match token2.IsCancellationRequested with
                    | true  -> dispatch (Progress (0.0, 1.0))
                    | false -> dispatch (Progress (progressValue, totalProgress))

                match token2.IsCancellationRequested with
                | true 
                    ->
                    return dispatch NavigateHome
                | false 
                    ->
                    match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                    | false 
                        ->
                        return dispatch NoInternet
                    | true 
                        ->
                        do! Async.SwitchToThreadPool() 
                        umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs                       
                        
                        let computation =
                            stateReducerCmd2
                            <| token2
                            <| kodisPathTemp
                            <| fun _   -> ()
                            <| fun msg -> IterationMsg >> dispatch <| msg
                            <| reportProgress
                        
                        let! result = async { return runIO computation }
                        
                        match token2.IsCancellationRequested with
                        | true  -> return dispatch NavigateHome
                        | false -> return Completed >> dispatch <| result
            with
            | ex ->
                use cts = CancellationTokenSource.CreateLinkedTokenSource token

                match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError cts.Token ex with
                | err 
                    when err = StopDownloading 
                    ->                    
                    match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                    | false -> return dispatch NoInternet
                    | true  -> return dispatch NavigateHome
                | _ ->
                    runIO (postToLog2 <| string ex.Message <| " #XElmish_Kodis_Critical_Error")
                    return dispatch NoInternet
        }