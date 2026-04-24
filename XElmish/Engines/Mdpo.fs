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

    async
        {
            try
                use cts = CancellationTokenSource.CreateLinkedTokenSource token

                let token2 = cts.Token
    
                let reportProgress (progressValue : float, totalProgress : float) =
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
                        
                        let! result = async { return runIO (webscraping_MDPO reportProgress token2 mdpoPathTemp) }

                        match token2.IsCancellationRequested with
                        | true 
                            ->
                            return dispatch NavigateHome
                        | false
                            ->
                            match result with
                            | Ok _    -> return dispatch (Completed mauiMdpoMsg)
                            | Error e -> return dispatch (ErrorMdpo e)
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
                    runIO (postToLog2 <| string ex.Message <| " #XElmish_Mdpo_Critical_Error")
                    return dispatch NoInternet
        }