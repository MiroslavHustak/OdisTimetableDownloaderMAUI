namespace OdisTimetableDownloaderMAUI.WinUI

open System

module Program =
    [<EntryPoint; STAThread>]
    let main args =
        do FSharp.Maui.WinUICompat.Program.Main(args, typeof<OdisTimetableDownloaderMAUI.WinUI.App>)
        0
