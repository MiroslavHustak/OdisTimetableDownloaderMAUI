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

    let reportProgress (progressValue : float, totalProgress : float) =
        match token.IsCancellationRequested with
        | true  -> dispatch (Progress (0.0, 1.0))
        | false -> dispatch (Progress (progressValue, totalProgress))

    async
        {
            try
                match token.IsCancellationRequested with
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
                        use cts = CancellationTokenSource.CreateLinkedTokenSource token
                        umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs

                        do! Async.SwitchToThreadPool()

                        let! result = async { return runIO (stateReducerCmd1 cts.Token stateDefault reportProgress) }

                        match cts.Token.IsCancellationRequested with
                        | true 
                            ->
                            dispatch NavigateHome
                        | false 
                            ->
                            match result with
                            | Ok msg    -> Completed >> dispatch <| msg
                            | Error err -> ErrorKodis >> dispatch <| err
            with
            | ex ->
                match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError token ex with
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
    |> fun a -> Async.Start(a, token)
    
let internal executePdf dispatch (token : CancellationToken) =

    let reportProgress (progressValue : float, totalProgress: float) =

        match token.IsCancellationRequested with
        | true  -> dispatch (Progress (0.0, 1.0))
        | false -> dispatch (Progress (progressValue, totalProgress))

    async
        {
            try
                match token.IsCancellationRequested with
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
                        use cts = CancellationTokenSource.CreateLinkedTokenSource token
                        umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs
                        
                        do! Async.SwitchToThreadPool()
                        
                        let computation =
                            stateReducerCmd2
                            <| cts.Token
                            <| kodisPathTemp
                            <| fun _   -> ()
                            <| fun msg -> IterationMsg >> dispatch <| msg
                            <| reportProgress
                        
                        let! result = async { return runIO computation }
                        
                        match cts.Token.IsCancellationRequested with
                        | true  -> return dispatch NavigateHome
                        | false -> return Completed >> dispatch <| result
            with
            | ex ->
                match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError token ex with
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
    |> fun a -> Async.Start(a, token)