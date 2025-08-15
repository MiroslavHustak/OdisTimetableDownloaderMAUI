namespace Helpers

open System

open Microsoft.Maui.Controls

//**************************************

open Settings.Messages
open Types.Haskell_IO_Monad_Simulation

module HardRestart =  

   let internal exitApp () =

       IO (fun () 
               -> 
               match Application.Current |> Option.ofNull with
               | Some app 
                   -> 
                   try
                       app.Quit()
                       String.Empty
                   with
                   | ex -> string ex.Message
               
               | None 
                   -> 
                   quitError
       )