namespace Helpers

open Microsoft.Maui.ApplicationModel

module FileInfoHelper = 

    open System
    open System.IO

    //*************************
    
    open Types.ErrorTypes
    open Helpers.Builders
    open Settings.Messages
    
    let private jsonEmpty = """[ {} ]"""

    let internal readAllText path = 

        pyramidOfDoom
            {
                //path je sice casto pod kontrolou a filepath nebude null, nicmene pro jistotu...  
                let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError
                                                
                let fInfoDat = FileInfo filepath
                let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                return Ok <| File.ReadAllText filepath                                           
            }  
            
        |> function
            | Ok value -> value                      
            | Error _  -> jsonEmpty //TODO logfile, nestoji to za to vytahovat Result nahoru                                 
                    
    let internal readAllTextAsync path = 

        pyramidOfDoom
            {   
                //path je sice casto pod kontrolou a filepath nebude null, nicmene pro jistotu...  
                let! filepath = Path.GetFullPath path |> Option.ofNullEmpty, Error JsonDownloadError

                let fInfoDat = FileInfo filepath
                let! _ = fInfoDat.Exists |> Option.ofBool, Error JsonDownloadError

                return Ok (File.ReadAllTextAsync filepath |> Async.AwaitTask)                                          
            }  
            
        |> function
            | Ok value -> value                      
            | Error _  -> async { return jsonEmpty } //TODO logfile, nestoji to za to vytahovat Result nahoru
       
module MyString = 
        
    open System    
      
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings : int, stringToAdd : string): string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty
                  
module CheckNetConnection =  
    
    open FSharp.Control

    open Microsoft.Maui.Networking
    open System.Net.NetworkInformation

    open Helpers.Builders

    //******************** Zatim se nepouziva ********************************************     
           
    let internal checkInternetConnectivityWM () = //Pro Windows Machine
        
        NetworkInterface.GetIsNetworkAvailable () |> Option.ofBool 
    
    
    let internal checkInternetConnectivityAE () =

            let requestLocationPermission () =
                
                async
                    {
                        let! status = 
                            Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>() |> Async.AwaitTask

                        match status <> PermissionStatus.Granted with
                        | true  ->
                                let! result = 
                                    Permissions.RequestAsync<Permissions.LocationWhenInUse>() |> Async.AwaitTask

                                return result = PermissionStatus.Granted
                        | false ->
                                return true
                    }

            async
                {
                    try   
                        
                        match Connectivity.Current.ConnectionProfiles |> Option.ofNull with            
                        | Some profiles
                            ->
                            match profiles |> List.ofSeq with
                            | [] 
                                -> 
                                return None                                         
                            | _  
                                ->
                                let! hasPermission = requestLocationPermission ()

                                let cond1 = profiles |> Seq.contains ConnectionProfile.WiFi
                                let cond2 = Connectivity.NetworkAccess = NetworkAccess.Internet

                                match cond1 && cond2 && hasPermission with
                                | true  -> return Some ()
                                | false -> return None  

                        | None 
                            ->
                            return None
                    with
                    | _ -> return None
                
                }  
            |> Async.RunSynchronously  