namespace Helpers

module StateMonad = 
 
    type internal State<'S, 'T> = State of ('S -> 'T * 'S)

    let internal runState (State f) initialState = f initialState
    
    let internal returnState x = State (fun s -> (x, s))

    let private bindState (m : State<'S, 'T>) (f : 'T -> State<'S, 'U>) : State<'S, 'U> =

        State
            (fun s
                ->
                let (v, s1) = runState m s
                runState (f v) s1
            )

   // Computation expression builder for the State monad
    
    type internal StateBuilder = StateBuilder with
        member _.Return(x) = returnState x
        member _.Bind(m, f) = bindState m f
        member _.ReturnFrom(m) = m
   
    let internal state = StateBuilder 