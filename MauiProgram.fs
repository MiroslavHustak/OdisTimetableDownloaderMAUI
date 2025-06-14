namespace OdisTimetableDownloaderMAUI

open System.Net
open Fabulous.Maui
open Microsoft.Maui.Hosting

type MauiProgram =

    static member CreateMauiApp() =

        ServicePointManager.SecurityProtocol <- SecurityProtocolType.Tls12 ||| SecurityProtocolType.Tls13 //quli Android 7.1

        MauiApp
            .CreateBuilder()
            .UseFabulousApp(App.program)
            .ConfigureFonts(
                fun fonts
                    ->
                    fonts
                        .AddFont("OpenSans-Regular.ttf", "OpenSansRegular")
                        .AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold")
                   
                    |> ignore<IFontCollection>
                )
            .Build()   

// Approx. 4300 LoC as of Apr 24, 2025
// REST API approx. 400 LoC as of Apr 24, 2025