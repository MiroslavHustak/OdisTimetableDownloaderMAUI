namespace Helpers

open System
open System.IO
open System.Threading
open System.Threading.Tasks

open System.Net.Sockets
open System.Security.Authentication

//***********************************

open Helpers.Builders      
open System.Net.Http
            
module Result =    
            
    let internal sequence aListOfResults = //gets the first error - see the book Domain Modelling Made Functional
        let prepend firstR restR =
            match firstR, restR with
            | Ok first, Ok rest   -> Ok (first :: rest) | Error err1, Ok _ -> Error err1
            | Ok _, Error err2    -> Error err2
            | Error err1, Error _ -> Error err1

        let initialValue = Ok [] 
        List.foldBack prepend aListOfResults initialValue  

    let internal fromOption = 
        function   
        | Some value -> Ok value
        | None       -> Error String.Empty  

    let internal toOption = 
        function   
        | Ok value -> Some value 
        | Error _  -> None  

    let internal fromBool ok err =                               
        function   
        | true  -> Ok ok  
        | false -> Error err

    (*
    let defaultWith defaultFn res =
        match res with
        | Ok value  -> value
        | Error err -> defaultFn err 
        
    let defaultValue default res =
        match res with
        | Ok value -> value
        | Error _  -> default
        
    let map f res =
        match res with
        | Ok value  -> Ok (f value)
        | Error err -> Error err

    let mapError f res =
        match res with
        | Ok value  -> Ok value
        | Error err -> Error (f err)

    let bind f res =
        match res with
        | Ok value  -> f value
        | Error err -> Error err
    *)
  
module Option =

    let internal ofBool =                           
        function   
        | true  -> Some ()  
        | false -> None

    let internal toBool = 
        function   
        | Some _ -> true
        | None   -> false

    let internal fromBool value =                               
        function   
        | true  -> Some value  
        | false -> None

    //Technically impure because of System.Object.ReferenceEquals
    //Pragmatically pure as there are no side effects        
    let internal ofNull (value : 'nullableValue) =
        match System.Object.ReferenceEquals(value, null) with //The "value" type can be even non-nullable, and ReferenceEquals will still work.
        | true  -> None
        | false -> Some value     

    let internal ofPtrOrNull (value : 'nullableValue) =  
        match System.Object.ReferenceEquals(value, null) with 
        | true  ->
                None
        | false -> 
                match box value with
                | null 
                    -> None
                | :? IntPtr as ptr 
                    when ptr = IntPtr.Zero
                    -> None
                | _   
                    -> Some value          
    
    let internal ofNullEmpty (value : 'nullableValue) : string option = //NullOrEmpty
        pyramidOfDoom 
            {
                let!_ = (not <| System.Object.ReferenceEquals(value, null)) |> fromBool value, None 
                let value = string value 
                let! _ = (not <| String.IsNullOrEmpty value) |> fromBool value, None //IsNullOrEmpty is not for nullable types

                return Some value
            }

    let internal ofNullEmpty2 (value : 'nullableValue) : string option =
        option2 
            {
                let!_ = (not <| System.Object.ReferenceEquals(value, null)) |> fromBool value                            
                let value : string = string value
                let!_ = (not <| String.IsNullOrEmpty value) |> fromBool value

                return Some value
            }

    (*
    let internal ofNullEmpty2 (value : string) : string option =
        option 
            {
                do! (not <| System.Object.ReferenceEquals(value, null)) |> fromBool value                            
                let value : string = string value
                do! (not <| String.IsNullOrEmpty value) |> fromBool value

                return value
            } 
    *)

    (*
    let defaultValue default opt =
        match opt with
        | Some value -> value
        | None       -> default
        
    let map f opt =
        match opt with
        | Some value -> Some (f value)
        | None       -> None

    let bind f opt =
        match opt with
        | Some value -> f value
        | None       -> None

    let orElseWith (f: unit -> 'T option) (option: 'T option) : 'T option =
        match option with
        | Some x -> Some x
        | None   -> f()

    *) 

    (*
        monadic composition (>>=) in Haskell

        import Control.Monad (guard)

        validate :: Maybe String -> Maybe String
        validate value = 
        value >>= \v ->                      -- Check if value is Just
        guard (not (null v)) >> Just v       -- Check if value is not empty, return Just v
        
        //*****************************************
        
        do notation

        import Control.Monad (guard)
    
        validate :: Maybe String -> Maybe String
        validate value = do
            v <- value                    -- Check if value is Just
            guard (not (null v))          -- Equivalent to `let! _ = not <| String.IsNullOrEmpty(value), None`
            return v 
    
    *)

    let internal ofNullEmptySpace (value : 'nullableValue) = //NullOrEmpty, NullOrWhiteSpace
        pyramidOfDoom //nelze option {}
            {
                let!_ = (not <| System.Object.ReferenceEquals(value, null)) |> fromBool Some, None 
                let value = string value 
                let! _ = (not <| String.IsNullOrWhiteSpace(value)) |> fromBool Some, None
    
                return Some value
            }

    let internal toResult err = 
        function   
        | Some value -> Ok value 
        | None       -> Error err     

    (*
    //FsToolkit
    let internal toResult (error : 'error) (opt : 'value option) : Result<'value, 'error> =

        match opt with
        | Some value -> Result.Ok value
        | None       -> Result.Error error    
    *)

module ExceptionHelpers =

    type ExceptionClassification =
        | TlsError2
        | TimeoutError2  
        | NetworkError2
        | UnknownError2   

    let internal isCancellationGeneric (stopDownloading : 'c) (timeoutError : 'c) (fileDownloadError : 'c) (token : CancellationToken) (ex : exn) =
           
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
                
                // Handle TaskCanceledException FIRST (before OperationCanceledException)
                | :? TaskCanceledException as tcex 
                    ->
                    let result =
                        match token.IsCancellationRequested with
                        | true  -> stopDownloading     // User cancelled with YOUR token
                        | false 
                            when tcex.CancellationToken.IsCancellationRequested
                                -> timeoutError        // HttpClient timeout
                        | false -> fileDownloadError   // Unknown cancellation
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
                    loop rest (fileDownloadError :: acc)
                
                | :? System.Security.Authentication.AuthenticationException
                    ->
                    loop rest (fileDownloadError :: acc)
                
                | :? System.Net.Sockets.SocketException
                    ->
                    loop rest (fileDownloadError :: acc) 
                
                | :? AggregateException as agg 
                    ->
                    let flattened = agg.Flatten().InnerExceptions |> Seq.toList
                    loop (flattened @ rest) acc
                
                | _ when isNull current.InnerException 
                    ->
                    loop rest (fileDownloadError :: acc)
                
                | _ ->
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
            | :? AggregateException as aex ->
                // Flatten AggregateException from Task.WhenAll, Async.Parallel, etc.
                aex.InnerExceptions 
                |> Seq.collect collectExceptions 
                |> List.ofSeq
            | ex ->
                match ex.InnerException |> Option.ofNull with
                | Some inner 
                    -> ex :: collectExceptions inner
                | None
                    -> [ ex ]
    
        let classifyException (e : Exception) : ExceptionClassification option =
            match e with
            // TLS/SSL errors
            | :? AuthenticationException
                -> 
                Some TlsError2
        
            // TaskCanceledException - correct handling
            | :? TaskCanceledException
                ->
                match token.IsCancellationRequested with
                | true  -> None  // Our cancellation, not a timeout
                | false -> Some TimeoutError2  // HTTP client timeout
        
            // Socket errors
            | :? SocketException as se ->
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
        
            // IO and HTTP errors
            | :? IOException 
                -> 
                Some NetworkError2
            | :? HttpRequestException 
                -> 
                Some NetworkError2
        
            // Android platform-specific message patterns (last resort)
            | e ->
                let msg = e.Message
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
                | NetworkError2 -> fileDownloadError
                | UnknownError2 -> fileDownloadError
    
        match isCancellationGeneric stopDownloading timeoutError fileDownloadError token ex with
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
            Error fileDownloadError