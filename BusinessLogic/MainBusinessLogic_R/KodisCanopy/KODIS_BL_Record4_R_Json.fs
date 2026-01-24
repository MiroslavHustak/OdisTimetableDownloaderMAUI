namespace BusinessLogic_R

open System.Threading

//*******************

open FSharp.Control
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Applicatives.CummulativeResultApplicative

open Api.Logging
open Api.FutureValidityRestApi 

open Settings.SettingsGeneral
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4_Json =   

    let private normaliseAsyncResult (token : CancellationToken) (a : Async<Result<'a, ParsingAndDownloadingErrors>>) =

        async 
            {
                try
                    token.ThrowIfCancellationRequested()
                    let! r = a
                    return r |> Result.mapError List.singleton
                with
                | ex                                 
                    ->
                    token.ThrowIfCancellationRequested()
                    runIO (postToLog2 <| string ex.Message <| "#0001-K4BL")
                    return Error [ JsonParsingError2 JsonDataFilteringError ]
            }
    
    let private process1 (token : CancellationToken) variant dir =

        async
            {
                token.ThrowIfCancellationRequested()

                match! runIO <| getFutureLinksFromRestApi token urlApi with
                | Ok value  
                    -> 
                    return runIO <| filterTimetableLinks variant dir (Ok value)
                | Error err 
                    -> 
                    token.ThrowIfCancellationRequested()
                    runIO (postToLog2 <| string err <| "#0002-K4BL")
                    return Error <| PdfDownloadError2 err
            }
    
    let private process2 (token : CancellationToken) variant dir =

        async
            {
                token.ThrowIfCancellationRequested()

                match variant with
                | FutureValidity 
                    ->
                    match! runIO <| getFutureLinksFromRestApi token urlJson with
                    | Ok value  
                        -> 
                        return runIO <| filterTimetableLinks variant dir (Ok value)
                    | Error err
                        -> 
                        runIO (postToLog2 <| string err <| "#0003-K4BL")
                        return Error <| PdfDownloadError2 err
                | _ 
                    ->
                    return Ok [] //zadna dalsi varianta uz tady neni, Ok[] je dummy
            }

    // Non-resumable variant
    let internal operationOnDataFromJson2 (token : CancellationToken) variant dir = 

        IO (fun () 
                ->
                async
                    {
                        token.ThrowIfCancellationRequested() 

                        let! results =
                            [|
                                normaliseAsyncResult token (process1 token variant dir)
                                normaliseAsyncResult token (process2 token variant dir)
                            |]
                            |> Async.Parallel
    
                        let result1 = Array.head results
                        let result2 = Array.last results
    
                        return
                            (fun l1 l2 -> l1 @ l2)
                            <!!!> result1
                            <***> result2

                            |> Result.map List.distinct
                    }

                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        )    

    //"Resumable block" variant for small payload
    let internal operationOnDataFromJson4 (token : CancellationToken) variant dir =

        let maxRetries = maxRetries4
        let delay = delayMs

        let inline checkCancel (token : CancellationToken) =
            token.ThrowIfCancellationRequested()
            ()

        let rec retryParallel maxRetries (delay : int) =  //cely blok, neni tra to robit jak u downloads, bo payload je jen 100-200 KB
            
            async 
                {
                    checkCancel token
    
                    try
                        let! results =
                            [|
                                normaliseAsyncResult token (process1 token variant dir)
                                normaliseAsyncResult token (process2 token variant dir)
                            |]
                            |> Async.Parallel
    
                        let result1 = Array.head results
                        let result2 = Array.last results

                        let combined =
                            validation
                                {
                                    let! links1 = result1
                                    and! links2 = result2
                                    return links1 @ links2 |> List.distinct
                                }
                        
                        match combined with
                        | Validation.Ok _
                            ->
                            return combined
                                                                        
                        | Validation.Error errs
                            ->
                            return Validation.Error errs
                        (*
                        tento kod samo o sobe nechyti vsechny triggers nutnych pro spusteni resume
                        return
                            validation
                                {
                                    let! links1 = result1
                                    and! links2 = result2
                                    return links1 @ links2 |> List.distinct
                                }
                        *)    
                    with
                    | ex 
                        when maxRetries > 0
                            ->
                            //runIO (postToLog2 <| string ex.Message <| "#0044-K4BL")
                            do! Async.Sleep delay

                            return! retryParallel (maxRetries - 1) (delay * 2)
                    | ex
                        ->
                        checkCancel token
                        runIO (postToLog2 <| string ex.Message <| "#0004-K4BL")
                          
                        return Validation.error <| PdfDownloadError2 FileDownloadError
                }
    
        IO (fun () 
                ->
                retryParallel maxRetries (umMiliSecondsToInt32 delay)
                |> (fun a -> Async.RunSynchronously(a, cancellationToken = token))
        )  
            
    // Resumable variant
    let internal operationOnDataFromJson_resumable (token : CancellationToken) variant dir =

        IO (fun ()
                ->
                let maxRetries = maxRetries4
                let initialDelayMs = delayMs

                let inline checkCancel () =
                    token.ThrowIfCancellationRequested ()

                let shouldRetry (errs : ParsingAndDownloadingErrors list) =
                    errs
                    |> List.exists
                        (
                            function
                                | PdfDownloadError2 TimeoutError           -> true
                                | PdfDownloadError2 ApiResponseError       -> true
                                | JsonParsingError2 JsonDataFilteringError -> true
                                | _                                        -> false
                        )

                let rec attempt retryCount (delayMs : int) =

                    async 
                        {
                            checkCancel ()

                            let! results =
                                [|
                                    normaliseAsyncResult token (process1 token variant dir)
                                    normaliseAsyncResult token (process2 token variant dir)
                                |]
                                |> Async.Parallel

                            let result1 = Array.head results
                            let result2 = Array.last results

                            let combined =
                                validation
                                    {
                                        let! links1 = result1
                                        and! links2 = result2
                                        return links1 @ links2 |> List.distinct
                                    }
                        
                            match combined with
                            | Validation.Ok _ 
                                ->
                                return combined

                            | Validation.Error errs
                                when retryCount < maxRetries && shouldRetry errs
                                ->
                                do! Async.Sleep delayMs
                                return! attempt (retryCount + 1) (delayMs * 2)

                            | Validation.Error errs 
                                ->
                                return Validation.Error errs
                        }
                        
                attempt 0 (umMiliSecondsToInt32 initialDelayMs)
        )