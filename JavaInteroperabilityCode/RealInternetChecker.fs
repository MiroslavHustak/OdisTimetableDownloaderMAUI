namespace JavaInteroperabilityCode

open System

open FsToolkit.ErrorHandling

type IRealInternetChecker =
    abstract member HasRealInternet : unit -> bool
    abstract member IsCaptivePortalSuspected : unit -> bool
    abstract member ConnectivityChanged : IObservable<unit>   // Android: real events; other platforms: can be empty/never-firing

#if ANDROID   
open Android.Net
open Android.App
open Android.Content

//****************************** JAVA ***********************************

module private JavaInterOperabilityHelpers =

    let getConnectivityManager (context : Context) : ConnectivityManager =       
        match context.GetSystemService Context.ConnectivityService with
        | :? ConnectivityManager as cm 
            -> cm
        | _ -> failwith "Varování: Nepodařilo se inicializovat kontrolu sítě. Pokud se soubory nestahují, restartujte aplikaci."

type private ConnectivityCallback(trigger : unit -> unit) =
    inherit ConnectivityManager.NetworkCallback()
    override _.OnCapabilitiesChanged(_network, _caps) = trigger()
    override _.OnAvailable _network = trigger()
    override _.OnLost _network = trigger()
    override _.OnUnavailable() = trigger()

module StartupDiagnostics =

    let networkCheckerResult : Result<IRealInternetChecker * IDisposable, string> =

        try
            let connectivityChanged = Event<unit>()
            let cm = JavaInterOperabilityHelpers.getConnectivityManager Application.Context

            let callback = new ConnectivityCallback(connectivityChanged.Trigger)

            use request =
                (new NetworkRequest.Builder())
                    .AddCapability(NetCapability.Internet)
                    .Build()

            cm.RegisterNetworkCallback(request, callback)

            let checkCapabilities (predicate : NetworkCapabilities -> bool) =
                option 
                    {
                        let! (network: Network) = cm.ActiveNetwork |> Option.ofNull
                        let! (caps: NetworkCapabilities) = cm.GetNetworkCapabilities network |> Option.ofNull
                        return predicate caps
                    }
                |> Option.defaultValue false

            let checker =
                { 
                    new IRealInternetChecker with

                    member _.HasRealInternet() =
                        try
                            checkCapabilities
                                (fun caps 
                                    ->
                                    caps.HasCapability NetCapability.Internet
                                    && caps.HasCapability NetCapability.Validated
                                )
                        with
                        | _ -> false

                    member _.IsCaptivePortalSuspected() =
                        try
                            checkCapabilities 
                                (fun caps -> caps.HasCapability NetCapability.CaptivePortal)
                        with
                        | _ -> false

                    member _.ConnectivityChanged = connectivityChanged.Publish
                }

            let disposable =
                { 
                    new IDisposable with
                        member _.Dispose() =
                            try 
                                cm.UnregisterNetworkCallback callback
                            with
                            | _ -> ()
                }

            Ok (checker, disposable)

        with
        | ex -> Error ex.Message

[<RequireQualifiedAccess>]
module RealInternetChecker =

    let private neverObservable<'a> () : IObservable<'a> =
        {
            new IObservable<'a> with
                member _.Subscribe(_observer) =
                    {
                        new IDisposable with
                            member _.Dispose() = ()
                    }
        }

    // Stub returned when ConnectivityManager is unavailable
    let private unavailableStub () : IRealInternetChecker =
        { 
            new IRealInternetChecker with
                member _.HasRealInternet() = false
                member _.IsCaptivePortalSuspected() = false
                member _.ConnectivityChanged = neverObservable () 
        }

    let checkerForDI () : IRealInternetChecker =
        match StartupDiagnostics.networkCheckerResult with
        | Ok (checker, _) -> checker
        | Error _         -> unavailableStub ()
#endif