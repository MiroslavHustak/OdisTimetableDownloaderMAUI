module OdisTimetableDownloaderMAUI.Engines.Mdpo

open System.Threading

open Types.Types
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open Types.ErrorTypes
open ApplicationDesign_R.WebScraping_MDPO

open Settings.Messages
open Settings.SettingsGeneral

open Helpers.ExceptionHelpers

type MdpoMsg =
    | Progress of float * float
    | IterationMsg of string  
    | Completed of string
    | ErrorMdpo of string
    | NavigateHome
    | NoInternet 
    
let executeMdpo dispatch (token : CancellationToken) =

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
                        
                        let! result = async { return runIO (webscraping_MDPO reportProgress cts.Token mdpoPathTemp) }

                        match cts.Token.IsCancellationRequested with
                        | true 
                            ->
                            dispatch NavigateHome
                        | false
                            ->
                            match result with
                            | Ok _    -> dispatch (Completed mauiMdpoMsg)
                            | Error e -> dispatch (ErrorMdpo e)
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
                    runIO (postToLog2 <| string ex.Message <| " #XElmish_Mdpo_Critical_Error")
                    return dispatch NoInternet
        }
    |> fun a -> Async.Start(a, token)