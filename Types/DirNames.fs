namespace Types

module DirNames =

    [<Struct>]   //vhodne pro 16 bytes => 4096 characters
    type internal ODIS =  
        {        
            odisDir1 : string
            odisDir2 : string
            odisDir3 : string
            odisDir4 : string
            odisDir5 : string
            odisDir6 : string
        }   