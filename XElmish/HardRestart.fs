namespace OdisTimetableDownloaderMAUI

open System
open Microsoft.Maui.Controls

//**************************************
open Helpers
open Settings.Messages
open Types.Haskell_IO_Monad_Simulation

module HardRestart =  

   let internal exitApp () =

       IO (fun () 
               -> 
               Application.Current
               |> Option.ofNull
               |> Option.map 
                   (fun app
                       ->
                       try
                           app.Quit()
                           String.Empty
                       with
                       | ex -> string ex.Message
                   )
               |> Option.defaultValue quitError               
       )