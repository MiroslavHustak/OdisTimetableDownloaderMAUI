module OdisTimetableDownloaderMAUI.Engines.Dpo

open System
open System.Threading

open FsToolkit.ErrorHandling

open Types.Types
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open Types.ErrorTypes
open ApplicationDesign_R.WebScraping_DPO

open Settings.Messages
open Settings.SettingsGeneral

open Helpers.ExceptionHelpers
open IO_Operations.IO_Operations


type DpoMsg =
    | Progress of float * float
    | IterationMsg of string  
    | CompletedFilter of (string * string) list
    | CompletedDownload of string
    | ErrorDpo of string
    | NavigateHome
    | NoInternet 
    
let internal executeFilter dispatch (token : CancellationToken) =

    IO (fun ()
            ->
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

                                let! result =
                                    async
                                        {
                                            return
                                                runIO
                                                (
                                                    webscraping_DPO_Filter
                                                    <| reportProgress
                                                    <| token2
                                                    <| dpoPathTemp
                                                )
                                        }

                                match token2.IsCancellationRequested with
                                | true
                                    -> 
                                    return dispatch NavigateHome
                                | false
                                    ->
                                    match result with
                                    | Ok result 
                                        ->
                                        return dispatch (CompletedFilter result)  // chains to DpoDownload
                                    | Error err
                                        ->
                                        return errMsg >> ErrorDpo >> dispatch <| err
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
                        | _
                            ->
                            runIO (postToLog2 <| string ex.Message <| " #XElmish_Dpo_Critical_Error_Filter")
                            return dispatch NoInternet
                }
    )

let internal executeDownload dispatch (token : CancellationToken) filterResult =

    IO (fun ()
            ->
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

                                let! result =
                                    async
                                        {
                                            return
                                                runIO
                                                ( 
                                                    webscraping_DPO_Download
                                                    <| reportProgress
                                                    <| token2
                                                    <| filterResult
                                                )
                                        }

                                match token2.IsCancellationRequested with
                                | true  
                                    -> 
                                    return dispatch NavigateHome
                                | false
                                    ->
                                    match result with
                                    | Ok _    
                                        -> 
                                        return dispatch (CompletedDownload mauiDpoMsg)
                                    | Error err 
                                        ->                                         
                                        return errMsg >> ErrorDpo >> dispatch <| err
                    with
                    | ex
                        ->
                        use cts = CancellationTokenSource.CreateLinkedTokenSource token
                        match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError cts.Token ex with
                        | err 
                            when err = StopDownloading
                            ->
                            match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                            | false -> return dispatch NoInternet
                            | true  -> return dispatch NavigateHome
                        | _ 
                            ->
                            runIO (postToLog2 <| string ex.Message <| " #XElmish_Dpo_Critical_Error_Download")
                            return dispatch NoInternet
                }
    )