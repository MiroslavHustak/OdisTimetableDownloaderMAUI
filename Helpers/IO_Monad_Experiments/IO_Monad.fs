namespace Helpers

open Builders

module IO_Monad =

    // Primitive: actually perform a side effect and pass the world through
    let internal primIO (action: unit -> 'a) : IO_Monad<'a> =

        IO_Monad
            (fun world 
                ->
                let result = action()
                world, result
            )  // world is just threaded through unchanged
   
    let internal runIOMonad (IO_Monad f) =

        // We create a dummy token and discard it — the real sequencing is enforced by the type
        let dummyWorld = RealWorldToken
        let _, result = f dummyWorld
        result

    //runIOMonad program