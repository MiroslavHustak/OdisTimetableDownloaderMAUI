namespace Settings

open System

//***************************

open Types.Types
open Types.ErrorTypes

module SettingsGeneral =  

    let internal logFileName = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\logEntries.json"

    let internal ODISDefault =  
        {          
            OdisDir1 = "JR_ODIS_aktualni_vcetne_vyluk"
            OdisDir2 = "JR_ODIS_pouze_budouci_platnost"
            //odisDir3 = "JR_ODIS_pouze_aktualni_vyluky"
            OdisDir4 = "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk" 
            OdisDir5 = "JR_ODIS_pouze_linky_dopravce_DPO" 
            OdisDir6 = "JR_ODIS_pouze_linky_dopravce_MDPO" 
        }   

    let internal listODISDefault4 = 
        [ 
            ODISDefault.OdisDir1
            ODISDefault.OdisDir2
            //ODISDefault.odisDir3
            ODISDefault.OdisDir4 
        ]    
        
    let internal timeOutInSeconds = 10 
    let internal waitingForNetConn = 30 //vterin

    let internal maxDegreeOfParallelism = 16
    let internal maxDegreeOfParallelismThrottled = 6
    let internal maxDegreeOfParallelismMedium = 12

    let internal myIdeaOfASmallList = 24
    let internal myIdeaOfALargelList = 100

    //let internal partialPathJsonTemp = @"/storage/emulated/0/Android/data/com.companyname.OdisTimetableDownloaderMAUI/"  //Android 7.1      
    
    let internal apiKeyTest = "test747646s5d4fvasfd645654asgasga654a6g13a2fg465a4fg4a3" 
    let internal urlLogging = "http://kodis.somee.com/api/logging" 

    // F# compiler directives
    #if WINDOWS
    let internal partialPathJsonTemp = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\KODISJson2\" //@"KODISJson2/" //v binu //tohle je pro stahovane json, ne pro type provider
    let internal kodisPathTemp = @"g:\Users\User\Data\"
    let internal kodisPathTemp4 = @"g:\Users\User\Data4\"
    let internal dpoPathTemp = @"g:\Users\User\Data4\"
    let internal mdpoPathTemp = @"g:\Users\User\Data4\"

    let internal pathTP_CurrentValidity = @"g:\Users\User\Data\JR_ODIS_aktualni_vcetne_vyluk\"
    let internal pathCanopy_CurrentValidity = @"g:\Users\User\Data4\JR_ODIS_aktualni_vcetne_vyluk\"    

    let internal pathTP_FutureValidity = @"g:\Users\User\Data\JR_ODIS_pouze_budouci_platnost\"   
    let internal pathCanopy_FutureValidity = @"g:\Users\User\Data4\JR_ODIS_pouze_budouci_platnost\"
     
    let internal pathTP_WithoutReplacementService = @"g:\Users\User\Data\JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk\" 
    let internal pathCanopy_WithoutReplacementService = @"g:\Users\User\Data4\JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk\"  
    #else
    let internal partialPathJsonTemp = @"/storage/emulated/0/FabulousTimetables/JsonData/com.companyname.OdisTimetableDownloaderMAUI/" 
    let internal kodisPathTemp = @"/storage/emulated/0/FabulousTimetables/"
    let internal kodisPathTemp4 = @"/storage/emulated/0/FabulousTimetables4/" 
    let internal dpoPathTemp = @"/storage/emulated/0/FabulousTimetables4/"
    let internal mdpoPathTemp = @"/storage/emulated/0/FabulousTimetables4/"  

    let internal pathTP_CurrentValidity = @"g:\Users\User\Data\JR_ODIS_aktualni_vcetne_vyluk\"
    let internal pathCanopy_CurrentValidity = @"g:\Users\User\Data4\JR_ODIS_aktualni_vcetne_vyluk\"    

    let internal pathTP_FutureValidity = @"g:\Users\User\Data\JR_ODIS_pouze_budouci_platnost\"   
    let internal pathCanopy_FutureValidity = @"g:\Users\User\Data4\JR_ODIS_pouze_budouci_platnost\"

    let internal pathTP_WithoutReplacementService = @"g:\Users\User\Data\JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk\" 
    let internal pathCanopy_WithoutReplacementService = @"g:\Users\User\Data4\JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk\"  
    


    #endif

    (*
    Right-click g:\Users\User\... (vsechny adresare anebo adresar nad tim), select Properties > Security, and grant full control.
        
    do *.fsproj pridat:
    <PropertyGroup Condition="$(TargetPlatformIdentifier) == 'windows'">
	  <DefineConstants>WINDOWS</DefineConstants>
    </PropertyGroup> 
    *)