namespace Helpers

open System

open type Fabulous.Maui.View
open Microsoft.Maui.Controls

open Settings.Messages

module HardRestart =  

   let internal exitApp () =
        
       match Application.Current |> Option.ofNull with
       | Some app 
           -> 
           try
               app.Quit()
               String.Empty
           with
           | ex -> string ex.Message
               
       | None 
           -> quitError