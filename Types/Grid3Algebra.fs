namespace Types

module Grid3Algebra =

    //CARDINALITY AND ISOMORPHISM

    type [<Struct>] internal Index = | I1 | I2 | I3   //3 x 3 = 9   
   
    type [<Struct>] internal GridFunction<'a> = { board : Index -> Index -> 'a }   
   
    type [<Struct>] internal ODIS = { board : GridFunction<string> }  

    let internal defaultGridFunction (defaultValue : 'a) : GridFunction<'a> =
        {
            board = fun _ _ -> defaultValue
        } 