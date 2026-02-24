namespace JavaInteroperabilityCode

open System
open FsToolkit.ErrorHandling

#if ANDROID
open Android.Net
open Android.App
open Android.Content

module RealInternetChecker =

    [<Literal>]
    let private InitializationFailedMsg =
        "Varování: Nepodařilo se inicializovat kontrolu sítě. Pokud se soubory nestahují, restartuj aplikaci."

    let private getConnectivityManager (ctx : Context) =

        match ctx.GetSystemService Context.ConnectivityService with
        | :? ConnectivityManager as cm -> Ok cm
        | _ -> Error InitializationFailedMsg

    type private ConnectivityCallback(trigger : unit -> unit) =

        inherit ConnectivityManager.NetworkCallback()
        override _.OnCapabilitiesChanged(_, _) = trigger()
        override _.OnAvailable _ = trigger()
        override _.OnLost _ = trigger()
        override _.OnUnavailable() = trigger()

    let internal tryChecker () =

        result 
            {
                let! cm = getConnectivityManager Application.Context

                let connectivityChanged = Event<unit>()
                let callback = new ConnectivityCallback(connectivityChanged.Trigger)

                let request =
                    (new NetworkRequest.Builder())
                        .AddCapability(NetCapability.Internet)
                        .Build()

                let!_ = 
                    try 
                        Ok <| cm.RegisterNetworkCallback(request, callback)
                    with
                    | _ -> Error InitializationFailedMsg

                let check predicate =
                    option 
                        {
                            let! (network : Network) = cm.ActiveNetwork |> Option.ofNull
                            let! (caps : NetworkCapabilities) = cm.GetNetworkCapabilities network |> Option.ofNull
                            return predicate caps
                        }
                    |> Option.defaultValue false 

                let hasRealInternet =
                    check (fun c -> c.HasCapability NetCapability.Internet && c.HasCapability NetCapability.Validated)

                let isCaptivePortalSuspected =
                    check (fun c -> c.HasCapability NetCapability.CaptivePortal)

                let disposable =
                    { 
                        new IDisposable with
                            member _.Dispose() =
                                try 
                                    cm.UnregisterNetworkCallback callback
                                with
                                | _ -> ()
                    }

                disposable.Dispose() 

                return hasRealInternet, isCaptivePortalSuspected, connectivityChanged.Publish //parametry zatim nepotrebne, ale co kdyby ... 
            }

#endif