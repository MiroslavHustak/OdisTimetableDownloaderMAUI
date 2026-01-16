namespace Applicatives

open FsToolkit.ErrorHandling

module ResultApplicative = //genuine applicative functor

    let private pure' x = Ok x

    let private apply (f : Result<'a -> 'b, 'e>) (x : Result<'a, 'e>) : Result<'b, 'e> =
        match f, x with
        | Ok f,  Ok x -> Ok (f x)
        | Error e, _  -> Error e
        | _, Error e  -> Error e

    let internal (<!>) f x = apply (pure' f) x
    let internal (<*>) f x = apply f x

    // <*> or <!> is the applicative apply operator.  
    // <!> → it's a kind of "force plain function into applicative land"    
    // <*> → visually suggests: "take function from left context, apply to right context"

module CummulativeResultApplicative = //tohle neni strikne genuine applicative functor, ale neco jako applicative-style functor

    let private pureC' x = Ok x
    
    let private applyC (f : Result<'a -> 'b, 'e list>) (x : Result<'a, 'e list>) : Result<'b, 'e list> =
        match f, x with
        | Ok f, Ok x 
            -> Ok (f x)    
        | Error e1, Error e2
            -> Error (e1 @ e2)    
        | Error e, _ | _, Error e 
            -> Error e
    
    let internal (<!!!>) f x = applyC (pureC' f) x
    let internal (<***>) f x = applyC f x

    let internal liftErrorToList (r : Result<'a, 'e>) : Result<'a, 'e list> = Result.mapError List.singleton r
           
    (*
    // 1. Classic operator style 
    let createUser name email age =
        User.create
        <!> Name.validate  name
        <*> Email.validate email
        <*> Age.validate   age    
    
    // 2. Named mapN style 
    let createUser name email age =
        (Name.validate   name,
         Email.validate  email,
         Age.validate    age)
        |||> Result.map3 User.create    
    
    // 3. Applicative computation expression    
    let createUser name email age = 
        result
            {
                let! n = Name.validate name
                and! e = Email.validate email
                and! a = Age.validate age
        
                return User.create n  
            }     
    *)