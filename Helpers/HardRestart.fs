namespace Helpers

open type Fabulous.Maui.View
open Microsoft.Maui.Controls

module HardRestart =  

   let internal exitApp () =
        
       match Application.Current |> Option.ofNull with
       | Some app -> app.Quit()
       | None     -> ()