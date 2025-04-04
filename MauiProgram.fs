﻿namespace OdisTimetableDownloaderMAUI

open Fabulous.Maui
open Microsoft.Maui.Hosting

type MauiProgram =
    static member CreateMauiApp() =
        MauiApp
            .CreateBuilder()
            .UseFabulousApp(App.program)
            .ConfigureFonts(fun fonts ->
                fonts
                    .AddFont("OpenSans-Regular.ttf", "OpenSansRegular")
                    .AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold")
                |> ignore)
            .Build()   

// Approx. 4200 LoC as of Mar 22, 2025