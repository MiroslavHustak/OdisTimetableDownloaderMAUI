module OdisTimetableDownloaderMAUI.Engines.KodisCanopy

open System.Threading

open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Api.Logging
open Helpers.ExceptionHelpers
open ApplicationDesign4_R.WebScraping_KODIS4

open Settings.SettingsGeneral

type KodisCanopyMsg =
    | Progress of float * float
    | IterationMsg of string  
    | Completed of string
    | ErrorKodis of string
    | NavigateHome
    | NoInternet 
    
let execute dispatch (token : CancellationToken) =
    
    let delayedCmd (token2 : CancellationToken) dispatch =

        async
            {
                try
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
      
                            let! result = 
                                async
                                    { 
                                        return
                                            stateReducer
                                                <| token2
                                                <| kodisPathTemp4
                                                <| fun msg -> IterationMsg >> dispatch <| msg
                                                <| reportProgress
                                            |> runIO
                                    }

                            match token2.IsCancellationRequested with
                            | true  -> return dispatch NavigateHome
                            | false -> return Completed >> dispatch <| result
                with
                | ex ->
                    match runIO <| isCancellationGeneric LetItBe StopDownloading TimeoutError FileDownloadError token2 ex with
                    | err 
                        when err = StopDownloading
                        ->
                        runIO (postToLog2 <| string ex.Message <| " StopDownloading #9999 Kodis Canopy")
                        match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                        | false -> return dispatch NoInternet
                        | true  -> return dispatch NavigateHome
                    | _ ->
                        runIO (postToLog2 <| string ex.Message <| " #XElmish_Kodis4_Critical_Error")
                        return dispatch NoInternet
            }  

    async 
        {          
            use cts = CancellationTokenSource.CreateLinkedTokenSource token
            umMiliSecondsToInt32 >> cts.CancelAfter <| timeoutMs

            return! delayedCmd cts.Token dispatch                           
        }

let execute2 dispatch (token : CancellationToken) =

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
                       
                        let! result = 
                            async
                                { 
                                    return
                                        stateReducer
                                            <| token2
                                            <| kodisPathTemp4
                                            <| fun msg -> IterationMsg >> dispatch <| msg
                                            <| reportProgress
                                        |> runIO
                                }

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
                    runIO (postToLog2 <| string ex.Message <| " StopDownloading #9999 Kodis Canopy")
                    match Helpers.ConnectivityWithDebouncing.isNowConnected () with
                    | false -> return dispatch NoInternet
                    | true  -> return dispatch NavigateHome
                | _ ->
                    runIO (postToLog2 <| string ex.Message <| " #XElmish_Kodis4_Critical_Error")
                    return dispatch NoInternet
        }