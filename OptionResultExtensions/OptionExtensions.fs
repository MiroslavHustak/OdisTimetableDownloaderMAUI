namespace Helpers

open System

//***********************************

open Helpers.Builders      
            
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