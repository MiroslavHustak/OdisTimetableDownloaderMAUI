namespace BusinessLogic_R

open System
open System.IO
open System.Net
open System.Threading

//*******************

open FsHttp
open FSharp.Control
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

open Applicatives.CummulativeResultApplicative

open Api.Logging
open Api.FutureLinks

open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

open Settings.SettingsGeneral
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 Nic neni trvalejsiho, nez neco docasneho ...
    // 31-12-2025 ... kdo by to byl rekl, ze se nic nezmeni   

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

    // Not resumable varriant
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

        let maxRetries = 4
        let delay = 1000

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
                            validation {
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
    
        IO (fun () -> retryParallel maxRetries delay |> (fun a -> Async.RunSynchronously(a, cancellationToken = token)))  
            
    // Resumable variant
    let internal operationOnDataFromJson_resumable (token : CancellationToken) variant dir =

        let maxRetries = 4
        let initialDelayMs = 1000

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

        IO (fun () ->
            attempt 0 initialDelayMs
            |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        )

//******************************************************************************

    let internal downloadAndSave (token : CancellationToken) context =  
   
        IO (fun ()
                ->
                let inline checkCancel (token : CancellationToken) =
                    token.ThrowIfCancellationRequested()
                    ()

                let downloadWithResume (uri : string) (pathToFile : string) (token : CancellationToken) : Async<Result<unit, PdfDownloadErrors>> = 
               
                    async 
                        {
                            let maxRetries = 500                            
                            let initialBackoffMs = 1000

                            checkCancel token
   
                            let rec attempt retryCount (backoffMs : int) = 

                                async 
                                    {
                                        checkCancel token
   
                                        let existingFileLength =                               
                                            runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                            |> Option.map (fun _ -> (FileInfo pathToFile).Length)
                                            |> Option.defaultValue 0L
                                      
                                        let headerContent1 = "Range" 
                                        let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                                      
                                        let request =
                                            match existingFileLength > 0L with
                                            | true  -> 
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds timeOutInSeconds2 
                                                            config_cancellationToken token 
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                            header headerContent1 headerContent2
                                                        }
                                            | false ->
                                                    http
                                                        {
                                                            GET uri
                                                            config_timeoutInSeconds timeOutInSeconds2 
                                                            config_cancellationToken token 
                                                            header "User-Agent" "FsHttp/Android7.1"
                                                        }     

                                        match! Request.sendAsync >> Async.Catch <| request with
                                        | Choice1Of2 response
                                            ->
                                            use response = response

                                            match response.statusCode with
                                            | HttpStatusCode.OK
                                            | HttpStatusCode.PartialContent 
                                                ->
                                                try
                                                    use! stream = response.content.ReadAsStreamAsync() |> Async.AwaitTask
                                                    use fileStream = new FileStream(pathToFile, FileMode.Append, FileAccess.Write, FileShare.None)
                                                    do! stream.CopyToAsync(fileStream, token) |> Async.AwaitTask
                                                    return Ok ()
                                                with                                               
                                                | ex 
                                                    -> 
                                                    checkCancel token
                                                    // runIO (postToLog2 <| string ex.Message <| "#0004-K4BL")  //in order not to log cancellation
                                                    return 
                                                        comprehensiveTryWith 
                                                            LetItBeKodis4 StopDownloading TimeoutError 
                                                            FileDownloadError TlsHandshakeError token ex
   
                                            | HttpStatusCode.Forbidden
                                                ->
                                                runIO <| postToLog2 () (sprintf "%s Forbidden 403 #0005-K4BL" uri) 
                                                return Error FileDownloadError
   
                                            | status
                                                ->
                                                runIO <| postToLog2 (string status) "#0006-K4BL" 
                                                return Error FileDownloadError
   
                                        | Choice2Of2 ex 
                                            ->
                                            match isCancellationGeneric LetItBeKodis4 StopDownloading TimeoutError FileDownloadError token ex with
                                            | err 
                                                when err = StopDownloading
                                                ->
                                                //runIO (postToLog2 <| string ex.Message <| "#0007-K4BL")  //in order not to log cancellation
                                                return Error StopDownloading
                                            | _ 
                                                ->
                                                match retryCount < maxRetries with
                                                | true  ->
                                                        do! Async.Sleep backoffMs
                                                        return! attempt (retryCount + 1) (backoffMs * 2)
                                                | false ->
                                                        runIO <| postToLog2 (string ex.Message) (sprintf "#0008-K4BL (retry %d)" retryCount) 
                                                        return 
                                                            comprehensiveTryWith
                                                                LetItBeKodis4 StopDownloading TimeoutError
                                                                FileDownloadError TlsHandshakeError token ex    
                                    }
   
                            return! attempt 0 initialBackoffMs
                    }
   
                let downloadAndSaveTimetables (token : CancellationToken) context =                                         
         
                    let l = context.list |> List.length

                    //mel jsem 2x stejnou linku s jinym jsGeneratedString, takze uri bylo unikatni, ale cesta k souboru 2x stejna
                    let removeDuplicatePathPairs uri pathToFile =
                        (uri, pathToFile)
                        ||> List.zip 
                        |> List.distinctBy snd
                         
                    let uri, pathToFile =
                        context.list
                        |> List.distinct
                        |> List.unzip
                        |> fun (uri, pathToFile) -> removeDuplicatePathPairs uri pathToFile
                        |> List.unzip           
                           
                    let counterAndProgressBar =
                        MailboxProcessor<MsgIncrement>
                            .StartImmediate
                                <|
                                fun inbox 
                                    ->   
                                    let rec loop n = 
                                        async
                                            {
                                                try
                                                    let! Inc i = inbox.Receive()                                                  
                                                    context.reportProgress (float n, float l)
                                                    return! loop (n + i)
                                                with
                                                | _ -> () 
                                            }
                                    loop 0                               
                    try
                        checkCancel token

                        (token, uri, pathToFile)
                        |||> List.Parallel.map2_IO_AW_Token_Async                                    
                            (fun uri (pathToFile : string) 
                                -> 
                                async
                                    {
                                        try
                                            checkCancel token 
                                            counterAndProgressBar.Post <| Inc 1
   
                                            let pathToFileExistFirstCheck =
                                                runIO <| checkFileCondition pathToFile (fun fi -> fi.Exists)
                                            
                                            match pathToFileExistFirstCheck with
                                            | Some _ 
                                                ->
                                                // File already exists → skip download entirely
                                                return Ok ()
   
                                            | None 
                                                ->
                                                // File does not exist (or was deleted) → download (with resume if partial)
                                                let! result = downloadWithResume uri pathToFile token 
                                                
                                                return
                                                    result
                                                    |> Result.mapError(
                                                        function
                                                            | err 
                                                                when err = StopDownloading
                                                                ->
                                                                //runIO (postToLog2 <| string err <| "#0009-K4BL") //in order not to log cancellation
                                                                PdfDownloadError2 StopDownloading
                                                            | err 
                                                                ->
                                                                runIO (postToLog2 <| string err <| "#0010-K4BL")
                                                                PdfDownloadError2  err 
                                                        )                                               
                                        with                                        
                                        | ex
                                            -> 
                                            checkCancel token
                                            //runIO (postToLog2 <| string ex.Message <| "#0011-K4BL")  //in order not to log cancellation
                                            return 
                                                comprehensiveTryWith
                                                    (PdfDownloadError2 LetItBeKodis4)
                                                    (PdfDownloadError2 StopDownloading)
                                                    (PdfDownloadError2 TimeoutError) 
                                                    (PdfDownloadError2 FileDownloadError) 
                                                    (PdfDownloadError2 TlsHandshakeError)
                                                    token ex
                                    }                                 
                            )

                        |> fun a -> Async.RunSynchronously(a, cancellationToken = token) 

                    with
                    | ex 
                        -> 
                        checkCancel token
                        //runIO (postToLog2 <| string ex.Message <| "#0012-K4BL")  //in order not to log cancellation
                        [
                            comprehensiveTryWith 
                                (PdfDownloadError2 LetItBeKodis4)
                                (PdfDownloadError2 StopDownloading)
                                (PdfDownloadError2 TimeoutError)
                                (PdfDownloadError2 FileDownloadError)
                                (PdfDownloadError2 TlsHandshakeError)
                                token ex 
                        ]
                                                              
                    |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                    |> Option.defaultValue (Ok ())  
                        
                pyramidOfDamnation //TOCTOU race condition by tady nemel byt problem       
                    {
                        let! _ = context.dir |> Directory.Exists, Error (PdfDownloadError2 NoFolderError)
                        let! _ = context.list <> List.Empty, Ok String.Empty
                    
                        return
                            downloadAndSaveTimetables token context
                            |> Result.map (fun _ -> String.Empty)       
                    }              
        )