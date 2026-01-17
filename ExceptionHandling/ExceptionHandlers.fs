namespace Helpers

open System
open System.IO
open System.Threading
open System.Threading.Tasks

open System.Net.Http

open System.Net.Sockets
open System.Security.Authentication

//***********************************
open Api.Logging
open Helpers.Builders  

open Types.ErrorTypes
open Types.Haskell_IO_Monad_Simulation

module ExceptionHelpers =

    let internal isCancellationGeneric (letItBe : 'c) (stopDownloading : 'c) (timeoutError : 'c) (fileDownloadError : 'c) (token : CancellationToken) (ex : exn) =
           
        let rec loop (stack : exn list) (acc : 'c list) : 'c list =
            match stack with
            | [] 
                -> 
                acc
            | current :: rest 
                ->
                match current with
                | null 
                    ->
                    loop rest (fileDownloadError :: acc)
                
                // Handle TaskCanceledException before OperationCanceledException
                | :? TaskCanceledException as tcex 
                    ->
                    let result =
                        match token.IsCancellationRequested with
                        | true  -> 
                                stopDownloading     // User cancelled with my token
                        | false 
                            when tcex.CancellationToken.IsCancellationRequested
                                -> 
                                timeoutError        // HttpClient timeout
                        | false ->
                                runIO (postToLog <| string ex.Message <| "#0001-ExceptionHandlers")  
                                fileDownloadError   // Unknown cancellation
                    loop rest (result :: acc)
                
                // This will now only catch OperationCanceledException that aren't TaskCanceledException
                | :? OperationCanceledException 
                    ->
                    let result =
                        match token.IsCancellationRequested with
                        | true  -> stopDownloading
                        | false -> timeoutError
                    loop rest (result :: acc)

                // Network-related exceptions
                | :? System.Net.Http.HttpRequestException
                    ->
                    runIO (postToLog <| string ex.Message <| "#0002-ExceptionHandlers")
                    loop rest (fileDownloadError :: acc)
                
                | :? System.Security.Authentication.AuthenticationException
                    ->
                    runIO (postToLog <| string ex.Message <| "#0003-ExceptionHandlers")
                    loop rest (fileDownloadError :: acc)
                
                | :? System.Net.Sockets.SocketException
                    ->
                    runIO (postToLog <| string ex.Message <| "#0004-ExceptionHandlers")
                    loop rest (fileDownloadError :: acc) 
                
                | :? AggregateException as agg 
                    ->
                    let flattened = agg.Flatten().InnerExceptions |> Seq.toList
                    loop (flattened @ rest) acc
                
                | _ when isNull current.InnerException 
                    ->
                    loop rest (fileDownloadError :: acc)
                
                | _ ->
                    runIO (postToLog <| string ex.Message <| "#0005-ExceptionHandlers")
                    loop (current.InnerException :: rest) acc
       
        // Start with the initial exception
        let results = loop [ex] []

        pyramidOfHell
            {   
                let!_ = not (results |> List.exists ((=) stopDownloading)), fun _ -> stopDownloading
                let!_ = not (results |> List.exists ((=) timeoutError)), fun _ -> timeoutError
                       
                return fileDownloadError        
            } 

    let internal comprehensiveTryWith (letItBe : 'c) (stopDownloading : 'c) (timeoutError : 'c) (fileDownloadError : 'c) (tlsHandShakeError : 'c) (token : CancellationToken) (ex : exn) =  
    
        let priority =
            function
                | TlsError2     -> 3
                | TimeoutError2 -> 2
                | NetworkError2 -> 1
                | UnknownError2 -> 0
    
        let rec collectExceptions (ex : Exception) =
            match ex with
            | :? AggregateException as aex
                ->
                // Flatten AggregateException from Task.WhenAll, Async.Parallel, etc.
                aex.InnerExceptions 
                |> Seq.collect collectExceptions 
                |> List.ofSeq
            | ex
                ->
                match ex.InnerException |> Option.ofNull with
                | Some inner 
                    -> 
                    runIO (postToLog <| string inner.Message <| "#0006-ExceptionHandlers")
                    ex :: collectExceptions inner
                | None
                    -> 
                    runIO (postToLog <| string ex.Message <| "#0007-ExceptionHandlers")
                    [ ex ]
    
        let classifyException (e : Exception) : ExceptionClassification option =
            match e with
            // TLS/SSL errors
            | :? AuthenticationException
                -> 
                Some TlsError2
        
            | :? TaskCanceledException
                ->
                match token.IsCancellationRequested with
                | true  -> None  // Our cancellation, not a timeout
                | false -> Some TimeoutError2  // HTTP client timeout
        
            | :? SocketException as se 
                ->
                runIO (postToLog <| string se.Message <| "#0008-ExceptionHandlers")

                match se.SocketErrorCode with
                | SocketError.TimedOut 
                    -> 
                    Some TimeoutError2
                | SocketError.NetworkUnreachable
                | SocketError.HostUnreachable
                | SocketError.ConnectionRefused
                | SocketError.ConnectionAborted
                | SocketError.NetworkDown
                | SocketError.HostNotFound
                | SocketError.ConnectionReset
                    -> 
                    Some NetworkError2
                | _ -> 
                    Some NetworkError2
        
            | :? IOException as ex 
                -> 
                runIO (postToLog <| string ex.Message <| "#0009-ExceptionHandlers")

                match ex with
                | :? DirectoryNotFoundException
                | :? FileNotFoundException
                | :? DriveNotFoundException 
                    ->
                    None   
                | _ ->
                    Some NetworkError2

            | :? HttpRequestException 
                -> 
                runIO (postToLog <| string ex.Message <| "#0010-ExceptionHandlers")
                Some NetworkError2
        
            // Android platform-specific message patterns (last resort)
            | e ->
                let msg = string e.Message

                runIO (postToLog <| msg <| "#0011-ExceptionHandlers")

                match msg with
                // TLS - specific terms only, not broad "SSL"/"TLS"
                | msg when msg.Contains("handshake", StringComparison.Ordinal) 
                        || msg.Contains("certificate", StringComparison.Ordinal)
                        || msg.Contains("trust anchor", StringComparison.Ordinal) 
                    ->
                    Some TlsError2
            
                // Timeout - specific patterns
                | msg when msg.Contains("timed out", StringComparison.Ordinal)
                    ->
                    Some TimeoutError2
            
                // Network - specific patterns
                | msg when msg.Contains("network unreachable", StringComparison.Ordinal)
                        || msg.Contains("connection refused", StringComparison.Ordinal)
                        || msg.Contains("no route to host", StringComparison.Ordinal)
                    ->
                    Some NetworkError2
            
                | _ -> 
                    None

        let classifyChain (chain : Exception list) : ExceptionClassification * Exception option =
            match chain |> List.choose classifyException with
            | [] 
                -> 
                (UnknownError2, chain |> List.tryHead)
            | classifications 
                -> 
                let highest = classifications |> List.maxBy priority
                (highest, None)  // Known classification, no need to log original
    
        let mapClassificationToError = 
            function
                | TlsError2     -> tlsHandShakeError
                | TimeoutError2 -> timeoutError
                | NetworkError2 -> letItBe
                | UnknownError2 -> fileDownloadError
    
        match isCancellationGeneric letItBe stopDownloading timeoutError fileDownloadError token ex with
        | err 
            when err = stopDownloading
            -> 
            Error stopDownloading
        
        | err
            when err = timeoutError 
            -> 
            Error timeoutError
        
        | err
            when err = letItBe 
            ->
            let classification, unknownEx = 
                ex 
                |> collectExceptions 
                |> classifyChain
        
            // Logging side-effect at the boundary only (point 3)
            match classification, unknownEx with
            | UnknownError2, Some originalEx
                ->
                //runIO (postToLog (sprintf "Unknown exception: %s - %s" (originalEx.GetType().Name) originalEx.Message) #UNKNOWN-ERROR")
                ()
            | _ ->
                ()
        
            classification
            |> mapClassificationToError
            |> Error
    
        | _ -> 
            runIO (postToLog <| string ex.Message <| "#0012-ExceptionHandlers")
            Error letItBe

        //temporary code for stress testing  
        |> function
            | err 
                when err = Error letItBe
                -> Error letItBe  
            | err
                -> err

    let internal comprehensiveTryWithMHD (letItBe : 'c) (stopDownloading : 'c) (timeoutError : 'c) (fileDownloadError : 'c) (tlsHandShakeError : 'c) (token : CancellationToken) (ex : exn) =  
       
           let priority =
               function
                   | TlsError2     -> 3
                   | TimeoutError2 -> 2
                   | NetworkError2 -> 1
                   | UnknownError2 -> 0
       
           let rec collectExceptions (ex : Exception) =
               match ex with
               | :? AggregateException as aex
                   ->
                   // Flatten AggregateException from Task.WhenAll, Async.Parallel, etc.
                   aex.InnerExceptions 
                   |> Seq.collect collectExceptions 
                   |> List.ofSeq
               | ex
                   ->
                   match ex.InnerException |> Option.ofNull with
                   | Some inner 
                       -> 
                       runIO (postToLog <| string inner.Message <| "#0013-ExceptionHandlers")
                       ex :: collectExceptions inner
                   | None
                       ->
                       runIO (postToLog <| string ex.Message <| "#0014-ExceptionHandlers")
                       [ ex ]
                 
           let classifyException (e : Exception) : ExceptionClassification option =
               match e with
               // TLS/SSL errors
               | :? AuthenticationException
                   -> 
                   Some TlsError2
           
               | :? TaskCanceledException
                   ->
                   match token.IsCancellationRequested with
                   | true  -> None  // Our cancellation, not a timeout
                   | false -> Some TimeoutError2  // HTTP client timeout
           
               | :? SocketException as se 
                   ->
                   runIO (postToLog <| string se.Message <| "#0015-ExceptionHandlers")

                   match se.SocketErrorCode with
                   | SocketError.TimedOut 
                       -> 
                       Some TimeoutError2
                   | SocketError.NetworkUnreachable
                   | SocketError.HostUnreachable
                   | SocketError.ConnectionRefused
                   | SocketError.ConnectionAborted
                   | SocketError.NetworkDown
                   | SocketError.HostNotFound
                   | SocketError.ConnectionReset
                       -> 
                       Some NetworkError2
                   | _ -> 
                       Some NetworkError2
           
               | :? IOException as ex 
                   -> 
                   runIO (postToLog <| string ex.Message <| "#0016-ExceptionHandlers")

                   match ex with
                   | :? DirectoryNotFoundException
                   | :? FileNotFoundException
                   | :? DriveNotFoundException 
                       ->
                       None   
                   | _ ->
                       Some NetworkError2

               | :? HttpRequestException 
                   -> 
                   Some NetworkError2
           
               // Android platform-specific message patterns (last resort)
               | e ->
                   let msg = string e.Message

                   runIO (postToLog <| msg <| "#0017-ExceptionHandlers")

                   match msg with
                   // TLS - specific terms only, not broad "SSL"/"TLS"
                   | msg when msg.Contains("handshake", StringComparison.Ordinal) 
                           || msg.Contains("certificate", StringComparison.Ordinal)
                           || msg.Contains("trust anchor", StringComparison.Ordinal) 
                       ->
                       Some TlsError2
               
                   // Timeout - specific patterns
                   | msg when msg.Contains("timed out", StringComparison.Ordinal)
                       ->
                       Some TimeoutError2
               
                   // Network - specific patterns
                   | msg when msg.Contains("network unreachable", StringComparison.Ordinal)
                           || msg.Contains("connection refused", StringComparison.Ordinal)
                           || msg.Contains("no route to host", StringComparison.Ordinal)
                       ->
                       Some NetworkError2
               
                   | _ -> 
                       None

           let classifyChain (chain : Exception list) : ExceptionClassification * Exception option =
               match chain |> List.choose classifyException with
               | [] 
                   -> 
                   (UnknownError2, chain |> List.tryHead)
               | classifications 
                   -> 
                   let highest = classifications |> List.maxBy priority
                   (highest, None)  // Known classification, no need to log original
       
           let mapClassificationToError = 
               function
                   | TlsError2     -> tlsHandShakeError
                   | TimeoutError2 -> timeoutError
                   | NetworkError2 -> letItBe
                   | UnknownError2 -> fileDownloadError
       
           match isCancellationGeneric letItBe stopDownloading timeoutError fileDownloadError token ex with
           | err 
               when err = stopDownloading
               -> 
               Error stopDownloading
           
           | err
               when err = timeoutError 
               -> 
               Error timeoutError
           
           | err
               when err = fileDownloadError 
               ->
               let classification, unknownEx = 
                   ex 
                   |> collectExceptions 
                   |> classifyChain
           
               // Logging side-effect at the boundary only (point 3)
               match classification, unknownEx with
               | UnknownError2, Some originalEx
                   ->
                   //runIO (postToLog (sprintf "Unknown exception: %s - %s" (originalEx.GetType().Name) originalEx.Message) #UNKNOWN-ERROR")
                   ()
               | _ ->
                   ()
           
               classification
               |> mapClassificationToError
               |> Error
       
           | _ -> 
               runIO (postToLog <| string ex.Message <| "#0018-ExceptionHandlers")
               Error fileDownloadError
          
           //temporary code for stress testing  
           |> function
               | err 
                   when err = Error letItBe
                   -> Error letItBe 
               | err
                   -> err