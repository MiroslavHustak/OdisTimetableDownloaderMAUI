namespace Helpers
       
module MyString = 
        
    open System    
      
    [<CompiledName "CreateStringSeqFold">] 
    let internal createStringSeqFold (numberOfStrings: int, stringToAdd: string): string =

        [1 .. numberOfStrings]
        |> List.fold (fun acc i -> (+) acc stringToAdd) String.Empty
                  
module CheckNetConnection =  

    open Microsoft.Maui.Networking
    open System.Net.NetworkInformation

    //**************************************

    open Settings.SettingsGeneral
    
    let internal checkInternetConnectivity () =

        match connectionCheckSwitch with
        | "Windows Machine" -> 
                             NetworkInterface.GetIsNetworkAvailable () |> Option.ofBool 
        //"Android 7.1"  
        | _                 -> 
                             match Connectivity.NetworkAccess with
                             | NetworkAccess.Internet -> Some ()
                             | _                      -> None