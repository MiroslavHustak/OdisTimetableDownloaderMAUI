namespace Settings

open Types.Types

module SettingsGeneral =  

    let internal logFileName = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\logEntries.json"
    let internal logFileNameWindows = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\tp_canopy_difference.txt"
    let internal logFileNameAndroid = @"/storage/emulated/0/Logs/tp_canopy_difference.txt"
    let internal logDirTP_Canopy = @"/storage/emulated/0/Logs"

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
    let internal maxDegreeOfParallelismThrottled = 4
    let internal maxDegreeOfParallelismMedium = 8

    let internal myIdeaOfASmallList = 24
    let internal myIdeaOfALargelList = 100

    //let internal partialPathJsonTemp = @"/storage/emulated/0/Android/data/com.companyname.OdisTimetableDownloaderMAUI/"  //Android 7.1      
    
    let internal apiKeyTest = "test747646s5d4fvasfd645654asgasga654a6g13a2fg465a4fg4a3" 
    let internal urlLogging = "http://kodis.somee.com/api/logging" 
    let internal urlApi = "http://kodis.somee.com/api/"  // Trailing slash preserved
    let internal urlJson = "http://kodis.somee.com/api/jsonLinks"
    #if ANDROID
    let internal partialPathJsonTemp = @"/storage/emulated/0/FabulousTimetables/JsonData/com.companyname.OdisTimetableDownloaderMAUI/" 
    let internal kodisPathTemp = @"/storage/emulated/0/FabulousTimetables/"
    let internal kodisPathTemp4 = @"/storage/emulated/0/FabulousTimetables4/" 
    let internal dpoPathTemp = @"/storage/emulated/0/FabulousTimetables4/"
    let internal mdpoPathTemp = @"/storage/emulated/0/FabulousTimetables4/" 
    let internal oldTimetablesPath = @"/storage/emulated/0/FabulousTimetablesOld/"
    let internal oldTimetablesPath4 = @"/storage/emulated/0/FabulousTimetablesOld4/"
    let path0 = sprintf "%s%s/" kodisPathTemp 
    let path4 = sprintf "%s%s/" kodisPathTemp4 
    #else
    let internal partialPathJsonTemp = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\KODISJson2\" //@"KODISJson2/" //v binu //tohle je pro stahovane json, ne pro type provider
    let internal kodisPathTemp = @"g:\Users\User\Data\"
    let internal kodisPathTemp4 = @"g:\Users\User\Data4\"
    let internal dpoPathTemp = @"g:\Users\User\Data4\"
    let internal mdpoPathTemp = @"g:\Users\User\Data4\"
    let internal oldTimetablesPath = @"g:\Users\User\DataOld\"
    let internal oldTimetablesPath4 = @"g:\Users\User\DataOld4\"
    let path0 = sprintf "%s%s/" kodisPathTemp 
    let path4 = sprintf "%s%s/" kodisPathTemp4      
    #endif

    let internal pathTP_CurrentValidity = path0 ODISDefault.OdisDir1 
    let internal pathCanopy_CurrentValidity = path4 ODISDefault.OdisDir1

    let internal pathTP_FutureValidity = path0 ODISDefault.OdisDir2   
    let internal pathCanopy_FutureValidity = path4 ODISDefault.OdisDir2

    let internal pathTP_WithoutReplacementService = path0 ODISDefault.OdisDir4  
    let internal pathCanopy_WithoutReplacementService = path4 ODISDefault.OdisDir4 

    (*
    Right-click g:\Users\User\... (vsechny adresare anebo adresar nad tim), select Properties > Security, and grant full control.
        
    do *.fsproj pridat:
    <PropertyGroup Condition="$(TargetPlatformIdentifier) == 'windows'">
	  <DefineConstants>WINDOWS</DefineConstants>
    </PropertyGroup> 
    *)