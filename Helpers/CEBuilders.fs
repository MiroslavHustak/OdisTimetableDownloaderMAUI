namespace Helpers
       
open System       
open System.Data

module Builders =
           
    [<Struct>]
    type internal MyBuilder3 = MyBuilder3 with       
         member _.Bind(resultExpr, nextFunc) = 
             match fst resultExpr with
             | Ok value -> nextFunc value 
             | Error err  -> (snd resultExpr) err
         member _.Return x = x  
     
    let internal pyramidOfInferno = MyBuilder3 
    
    //**************************************************************************************

    [<Struct>]
    type internal MyBuilder = MyBuilder with    
         member _.Bind(condition, nextFunc) =
             match fst condition with
             | false -> snd condition
             | true  -> nextFunc()  
         member _.Return x = x
         member _.Using x = x

    let internal pyramidOfHell = MyBuilder

    //**************************************************************************************

    [<Struct>]
    type internal Builder2 = Builder2 with    
        member _.Bind((optionExpr, err), nextFunc) =
            match optionExpr with
            | Some value -> nextFunc value 
            | _          -> err  
        member _.Return x : 'a = x   
        member _.ReturnFrom x : 'a = x 
        member _.TryFinally(body, compensation) =
            try body()
            finally compensation()
        member _.Zero () = ()
        member _.Using(resource, binder) =
            use r = resource
            binder r
    
    let internal pyramidOfDoom = Builder2
    
    //**************************************************************************************

    type internal Reader<'e, 'a> = 'e -> 'a
    
    [<Struct>] 
    type internal ReaderBuilder = ReaderBuilder with
        member __.Bind(m, f) = fun env -> f (m env) env      
        member __.Return x = fun _ -> x
        member __.ReturnFrom x = x
        //member __.Zero x = x

    let internal reader = ReaderBuilder 