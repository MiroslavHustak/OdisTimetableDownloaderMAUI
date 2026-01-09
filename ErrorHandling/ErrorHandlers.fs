namespace Helpers

open System
open System.Threading

open FsToolkit.ErrorHandling

//***********************************

open Types.ErrorTypes
open Helpers.Builders

module ExceptionHelpers =

    let private containsCancellationGeneric (stopDownloading : 'c) (timeoutError : 'c) (fileDownloadError : 'c) (token : CancellationToken) (ex : exn) =
        
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

                | :? OperationCanceledException 
                    ->
                    let result =
                        match token.IsCancellationRequested with
                        | true  -> stopDownloading
                        | false -> timeoutError
                    loop rest (result :: acc)

                | :? AggregateException as agg 
                    ->
                    // Flatten inner exceptions and push them onto the stack
                    let flattened = agg.Flatten().InnerExceptions |> Seq.toList
                    loop (flattened @ rest) acc

                | _ when isNull current.InnerException 
                    ->
                    loop rest (fileDownloadError :: acc)
                | _ 
                    ->
                    // Push inner exception onto stack
                    loop (current.InnerException :: rest) acc
    
        // Start with the initial exception
        let results = loop [ex] []

        pyramidOfHell
            {   
                let!_ = not (results |> List.exists ((=) stopDownloading)), fun _ -> stopDownloading
                let!_ = not (results |> List.exists ((=) timeoutError)), fun _ -> timeoutError
                    
                return fileDownloadError        
            } 
    
    let private containsCancellation (token : CancellationToken) (ex : exn) : PdfDownloadErrors =
        
        let rec loop (stack : exn list) (acc : PdfDownloadErrors list) : PdfDownloadErrors list =

            match stack with
            | [] 
                -> 
                acc

            | current :: rest 
                ->
                match current with
                | null 
                    ->
                    loop rest (FileDownloadError :: acc)

                | :? OperationCanceledException 
                    ->
                    let result =
                        match token.IsCancellationRequested with
                        | true  -> StopDownloading
                        | false -> TimeoutError
                    loop rest (result :: acc)

                | :? AggregateException as agg 
                    ->
                    // Flatten inner exceptions and push them onto the stack
                    let flattened = agg.Flatten().InnerExceptions |> Seq.toList
                    loop (flattened @ rest) acc

                | _ when isNull current.InnerException 
                    ->
                    loop rest (FileDownloadError :: acc)
                | _ 
                    ->
                    // Push inner exception onto stack
                    loop (current.InnerException :: rest) acc
    
        // Start with the initial exception
        let results = loop [ex] []

        pyramidOfHell
            {   
                let!_ = not (results |> List.exists ((=) StopDownloading)), fun _ -> StopDownloading
                let!_ = not (results |> List.exists ((=) TimeoutError)), fun _ -> TimeoutError
                    
                return FileDownloadError        
            } 
            
    let internal isCancellation (token : CancellationToken) (ex : exn) = containsCancellation token ex
    let internal isCancellationGeneric (stopDownloading : 'c) (timeoutError : 'c) (fileDownloadError : 'c) (token : CancellationToken) (ex : exn) = 
        containsCancellationGeneric stopDownloading timeoutError fileDownloadError token ex 
            
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