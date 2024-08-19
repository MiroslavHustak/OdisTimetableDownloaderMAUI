namespace Types

module Types =

    [<Struct>]   //vhodne pro 16 bytes => 4096 characters
    type internal ODIS =  
        {        
            OdisDir1 : string
            OdisDir2 : string
            //OdisDir3 : string
            OdisDir4 : string
            OdisDir5 : string
            OdisDir6 : string
        }  
        
    [<Struct>] 
    type Context<'a, 'b, 'c> = 
        {
            listMappingFunction : ('a -> 'b -> 'c) -> 'a list -> 'b list -> 'c list
            reportProgress : (float * float) -> unit
            dir : string
            list : (string * string) list
        }