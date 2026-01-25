namespace DotNetInteroperabilityCode

open Microsoft.Maui.Networking

module internal ConnectivityMonitorManager =

    let handlers =
        System.Collections.Generic.List<System.EventHandler<ConnectivityChangedEventArgs>>()

    let lockObj = obj()

    let addHandler handler =
        lock lockObj 
            (fun ()
                ->
                handlers.Add handler
                Connectivity.ConnectivityChanged.AddHandler handler
            )

    let removeAllHandlers () =
        lock lockObj
            (fun ()
                ->
                handlers
                |> Seq.iter Connectivity.ConnectivityChanged.RemoveHandler

                handlers.Clear()
            )