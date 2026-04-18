module OdisTimetableDownloaderMAUI.Engines.Mdpo

open System.Threading

open Types.Types
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open IO_Operations.IO_Operations
open Helpers.ConnectivityWithDebouncing

open ApplicationDesign_R.WebScraping_MDPO

open Settings.SettingsGeneral

open OdisTimetableDownloaderMAUI.ActorModels
open FsToolkit.ErrorHandling
open Settings.Messages

type MdpoMsg  =
    | Progress of float * float
    | IterationMsg of string  
    | Completed of string
    | ErrorMdpo of string
    | NavigateHome
    
let executeMdpo dispatch (token: CancellationToken) =

    let reportProgress (progressValue: float, totalProgress: float) =
        match token.IsCancellationRequested with
        | true  -> dispatch (Progress (0.0, 1.0))
        | false -> dispatch (Progress (progressValue, totalProgress))

    let cmd (token: CancellationToken) =
        async {
            try
                do! Async.SwitchToThreadPool()

                let! result =
                    async { return runIO (webscraping_MDPO reportProgress token mdpoPathTemp) }

                match token.IsCancellationRequested with
                | true  ->
                    dispatch NavigateHome
                | false ->
                    match result with
                    | Ok _    -> dispatch (Completed mauiDpoMsg)
                    | Error e -> dispatch (ErrorMdpo e)

            with ex ->
                runIO (postToLog2 ex.Message "Mdpo-CMD")
                dispatch (ErrorMdpo ex.Message)
        }

    async {
        use cts = CancellationTokenSource.CreateLinkedTokenSource token
        umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs
        
        match cts.Token.IsCancellationRequested with
        | true  -> dispatch NavigateHome
        | false -> return! cmd cts.Token
    }
    |> Async.Start