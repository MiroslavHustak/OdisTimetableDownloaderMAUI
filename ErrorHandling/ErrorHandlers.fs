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
            // If it’s not user cancellation or timeout, continue with Android-specific analysis
            let rec findRoot (ex : Exception) =
                match ex.InnerException |> Option.ofNull with
                | Some inner -> findRoot inner
                | None  -> ex
           
            let root = findRoot ex
           
            match root with
            | :? SocketException as sockEx 
                when sockEx.SocketErrorCode = SocketError.NetworkUnreachable
                ->
                Error letItBe
           
            | :? SocketException as sockEx 
                when sockEx.SocketErrorCode = SocketError.TimedOut 
                ->
                Error timeoutError
           
            // HttpClient timeout (TaskCanceledException)   
            | :? TaskCanceledException as tcex 
                when not tcex.CancellationToken.IsCancellationRequested
                ->
                Error timeoutError

            // TLS handshake
            | :? AuthenticationException as authEx 
                ->
                Error tlsHandShakeError
           
            | :? IOException as ioEx
                ->
                Error letItBe
           
            | :? SocketException as sockEx 
                ->
                Error letItBe
           
            | :? HttpRequestException
                ->
                Error letItBe 

            | _ ->
                Error letItBe  

        | _ ->
            Error letItBe 