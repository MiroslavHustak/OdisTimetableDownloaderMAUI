namespace Settings

open System

//*******************

open Types.Grid3Algebra

module SettingsGeneral =  

    let [<Literal>] internal currentValidity = "JR_ODIS_aktualni_vcetne_vyluk"
    let [<Literal>] internal futureValidity = "JR_ODIS_pouze_budouci_platnost"
    let [<Literal>] internal longTermValidity = "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk"

    let [<Literal>] internal dpo = "JR_ODIS_pouze_linky_dopravce_DPO"
    let [<Literal>] internal mdpo = "JR_ODIS_pouze_linky_dopravce_MDPO"

    let [<Literal>] internal logFileName = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\logEntries.json"
    let [<Literal>] internal logFileNameWindows = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\tp_canopy_difference.txt"
    let [<Literal>] internal logFileNameWindows2 = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\stress_testing_logs.txt"
    let [<Literal>] internal logFileNameAndroid = @"/storage/emulated/0/Logs/tp_canopy_difference.txt"
    let [<Literal>] internal logFileNameAndroid2 = @"/storage/emulated/0/Logs/stress_testing_logs.txt"
    let [<Literal>] internal logDirTP_Canopy = @"/storage/emulated/0/Logs"
   
    //CARDINALITY AND ISOMORPHISM
    let internal ODIS_Variants =  
        {          
            board = 
                { 
                    board = 
                        fun row col
                            ->
                            match (row, col) with
                            | (I1, I1) -> currentValidity
                            | (I1, I2) -> futureValidity
                            | (I1, I3) -> String.Empty
                            | (I2, I1) -> longTermValidity
                            | (I2, I2) -> dpo
                            | (I2, I3) -> mdpo
                            | _        -> String.Empty
                }
        }

    //Pokud bych chtel vsude napr. String.Empty v default record, pak staci toto:
    let internal ODIS_Variants2 = { board = defaultGridFunction String.Empty }

    let internal listOfODISVariants = 
        [ 
            ODIS_Variants.board.board I1 I1
            ODIS_Variants.board.board I1 I2
            ODIS_Variants.board.board I2 I1 
        ]    
        
    let [<Literal>] internal timeOutInSeconds = 31 
    let [<Literal>] internal timeOutInSeconds2 = 31
    let [<Literal>] internal waitingForNetConn = 30 //vterin

    let [<Literal>] internal maxFileSizeKb = 10L 

    let [<Literal>] internal maxDegreeOfParallelism = 18
    let [<Literal>] internal maxDegreeOfParallelismThrottled = 10
    let [<Literal>] internal maxDegreeOfParallelismMedium = 14

    let [<Literal>] internal myIdeaOfASmallList = 24
    let [<Literal>] internal myIdeaOfALargelList = 100

    let [<Literal>] internal maxRetries3 = 3
    let [<Literal>] internal maxRetries4 = 4
    let [<Literal>] internal maxRetries500 = 20    
    let [<Literal>] internal delayMs = 1000

    //let internal partialPathJsonTemp = @"/storage/emulated/0/Android/data/com.companyname.OdisTimetableDownloaderMAUI/"  //Android 7.1      
    
    let [<Literal>] internal apiKeyTest = "test747646s5d4fvasfd645654asgasga654a6g13a2fg465a4fg4a3" 
    
    let [<Literal>] internal urlLogging = "http://kodis.somee.com/api/logging" 
    let [<Literal>] internal urlApi = "http://kodis.somee.com/api/"  // Trailing slash preserved   
    let [<Literal>] internal urlJson = "http://kodis.somee.com/api/jsonLinks" 

    // Rust / Render
    //let [<Literal>] internal urlLogging = "https://rust-rest-api-endpoints.onrender.com/api/logging"
    //let [<Literal>] internal urlApi = "https://rust-rest-api-endpoints.onrender.com/api/canopy"  //Rust chce toto, TOD podumej cemu
    //let [<Literal>] internal urlJson = "https://rust-rest-api-endpoints.onrender.com/api/jsonLinks"    
    
    #if ANDROID
    let [<Literal>] internal partialPathJsonTemp = @"/storage/emulated/0/FabulousTimetables/JsonData/com.companyname.OdisTimetableDownloaderMAUI/" 
    let [<Literal>] internal kodisPathTemp = @"/storage/emulated/0/FabulousTimetables/"
    let [<Literal>] internal kodisPathTemp4 = @"/storage/emulated/0/FabulousTimetables4/" 
    let [<Literal>] internal dpoPathTemp = @"/storage/emulated/0/FabulousTimetables4/"
    let [<Literal>] internal mdpoPathTemp = @"/storage/emulated/0/FabulousTimetables4/" 
    let [<Literal>] internal oldTimetablesPath = @"/storage/emulated/0/FabulousTimetablesOld/"
    let [<Literal>] internal oldTimetablesPath4 = @"/storage/emulated/0/FabulousTimetablesOld4/"
    let internal path0 = sprintf "%s%s/" kodisPathTemp 
    let internal path4 = sprintf "%s%s/" kodisPathTemp4 
    #else
    let [<Literal>] internal partialPathJsonTemp = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\KODISJson2\" //@"KODISJson2/" //v binu //tohle je pro stahovane json, ne pro type provider
    let [<Literal>] internal kodisPathTemp = @"g:\Users\User\Data\"
    let [<Literal>] internal kodisPathTemp4 = @"g:\Users\User\Data4\"
    let [<Literal>] internal dpoPathTemp = @"g:\Users\User\Data4\"
    let [<Literal>] internal mdpoPathTemp = @"g:\Users\User\Data4\"
    let [<Literal>] internal oldTimetablesPath = @"g:\Users\User\DataOld\"
    let [<Literal>] internal oldTimetablesPath4 = @"g:\Users\User\DataOld4\"
    let internal path0 = sprintf "%s%s/" kodisPathTemp 
    let internal path4 = sprintf "%s%s/" kodisPathTemp4      
    #endif

    let internal pathTP_CurrentValidity = path0 <| ODIS_Variants.board.board I1 I1
    let internal pathCanopy_CurrentValidity = path4 <| ODIS_Variants.board.board I1 I1

    let internal pathTP_FutureValidity = path0 <| ODIS_Variants.board.board I1 I2   
    let internal pathCanopy_FutureValidity = path4 <| ODIS_Variants.board.board I1 I2

    let internal pathTP_LongTermValidity = path0 <| ODIS_Variants.board.board I2 I1 
    let internal pathCanopy_LongTermValidity = path4 <| ODIS_Variants.board.board I2 I1  
        
    (*
    Right-click g:\Users\User\... (vsechny adresare anebo adresar nad tim), select Properties > Security, and grant full control.
        
    do *.fsproj pridat:
    <PropertyGroup Condition="$(TargetPlatformIdentifier) == 'windows'">
	  <DefineConstants>WINDOWS</DefineConstants>
    </PropertyGroup> 
    *)