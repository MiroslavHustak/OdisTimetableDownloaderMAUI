namespace Settings

open System
open System.IO

open Types.Types
open Types.Grid3Algebra
open Types.Haskell_IO_Monad_Simulation

#if ANDROID
open Android.Content
open Microsoft.Maui.Storage
#endif

module private Paths =

    let ensureDir path =     
        try
            Directory.CreateDirectory path |> ignore<DirectoryInfo>
            path
        with 
        | _ -> path 
    
    #if ANDROID    
    let basePathSandBox () = FileSystem.Current.AppDataDirectory  // TODO pro Google Play
    let basePath () = "/storage/emulated/0/ODIS/"
    #else
    let basePath () = @"g:\Users\User\"
    #endif  

module SettingsGeneral =     
   
    let [<Literal>] internal currentValidity = "JR_ODIS_aktualni_vcetne_vyluk"
    let [<Literal>] internal futureValidity = "JR_ODIS_pouze_budouci_platnost"
    let [<Literal>] internal longTermValidity = "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk"

    let [<Literal>] internal dpo = "JR_ODIS_pouze_linky_dopravce_DPO"
    let [<Literal>] internal mdpo = "JR_ODIS_pouze_linky_dopravce_MDPO"

    let internal ODIS_Variants =
        {
            board =
                {
                    board =
                        fun row col ->
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

    let internal ODIS_Variants2 =
        { board = defaultGridFunction String.Empty }

    let internal listOfODISVariants =
        [
            ODIS_Variants.board.board I1 I1
            ODIS_Variants.board.board I1 I2
            ODIS_Variants.board.board I2 I1
        ]

    let [<Literal>] internal maxRetries3 = 10
    let [<Literal>] internal maxRetries4 = 15
    let [<Literal>] internal maxRetries500 = 50

    let internal delayMsJson : int<ms> = 3_000<ms>
    let internal delayMs : int<ms> = 5_000<ms>

    let internal timeOutInSeconds : int<s> = 60<s>
    let internal timeOutInSeconds2 : int<s> = 60<s>
    let internal timeoutMs : int<ms> = 1_200_000<ms>
    let internal pingTimeoutMs : int<ms> = 3_000<ms>
    let internal waitingForNetConn : int<s> = 30<s>

    let internal maxFileSizeKb : int64<KiB> = 150L<KiB>

    let [<Literal>] internal maxDegreeOfParallelism = 18
    let [<Literal>] internal maxDegreeOfParallelismThrottled = 6
    let [<Literal>] internal maxDegreeOfParallelismMedium = 12

    let [<Literal>] internal myIdeaOfASmallList = 24
    let [<Literal>] internal myIdeaOfALargelList = 100

    let [<Literal>] internal apiKeyTest = "test747646s5d4fvasfd645654asgasga654a6g13a2fg465a4fg4a3"
    let [<Literal>] internal urlLogging = "http://kodis.somee.com/api/logging"
    let [<Literal>] internal urlApi = "http://kodis.somee.com/api/"
    let [<Literal>] internal urlJson = "http://kodis.somee.com/api/jsonLinks"
  
    let private logs () =
        Path.Combine(Paths.basePath(), "Logs")
       |> Paths.ensureDir
    
    #if ANDROID
    let internal jsonTemp () =
        Path.Combine(Paths.basePathSandBox (), "JsonData")
        |> Paths.ensureDir
    #else

    let internal jsonTemp () =
        Path.Combine(Paths.basePath (), "JsonData")
        |> Paths.ensureDir
    #endif
    
    let internal kodisPathTemp () =
        Path.Combine(Paths.basePath(), "JR_ODIS")
        |> Paths.ensureDir

    #if ANDROID   
    let internal kodisPathTempGP () =
        Path.Combine(Paths.basePathSandBox(), "JR_ODIS")
        |> Paths.ensureDir  
    #endif

    let internal kodisPathTemp4 () =
        Path.Combine(Paths.basePath(), "JR_ODIS_Extra")
        |> Paths.ensureDir

    #if ANDROID   
    let internal kodisPathTemp4GP () =
        Path.Combine(Paths.basePathSandBox(), "JR_ODIS_Extra")
        |> Paths.ensureDir  
    #endif   

    let internal dpoPathTemp () =
        Path.Combine(Paths.basePath(), "JR_ODIS_Extra")
        |> Paths.ensureDir

    #if ANDROID   
    let internal dpoPathTempGP () =
        Path.Combine(Paths.basePathSandBox(), dpo)
        |> Paths.ensureDir  
    #endif

    let internal mdpoPathTemp () =
        Path.Combine(Paths.basePath(), "JR_ODIS_Extra")
        |> Paths.ensureDir

    let internal logDirTP_Canopy () = logs ()

    let internal oldTimetablesPath () =
        Path.Combine(Paths.basePath(), "JR_ODIS_zaloha")
        |> Paths.ensureDir

    let internal oldTimetablesPath4 () =
        Path.Combine(Paths.basePath(), "JR_ODIS_zaloha_Extra")
        |> Paths.ensureDir

    let private path0 (variant : string) = Path.Combine(kodisPathTemp (), variant)
    let private path4 (variant : string) = Path.Combine(kodisPathTemp4 (), variant)

    #if ANDROID
    let internal partialPathJsonTemp () =
        Path.Combine(
            jsonTemp (),
            "com.companyname.OdisTimetableDownloaderMAUI"
        )
        |> Directory.CreateDirectory
        |> fun d -> d.FullName

    let internal logFileName () = Path.Combine(logs (), "logEntries.json")
    let internal logFileNameAndroid () = Path.Combine(logs (), "tp_canopy_difference.txt")
    let internal logFileNameAndroid2 () = Path.Combine(logs (), "stress_testing_logs.txt") 
    
    #else
    let internal partialPathJsonTemp () = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\KODISJson2\"   
    let [<Literal>] internal logFileName = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\logEntries.json"
    let [<Literal>] internal logFileNameWindows = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\tp_canopy_difference.txt"
    let [<Literal>] internal logFileNameWindows2 = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\logs\stress_testing_logs.txt"      
    #endif
     
    let internal pathTP_CurrentValidity () = path0 (ODIS_Variants.board.board I1 I1)
    let internal pathCanopy_CurrentValidity () = path4 (ODIS_Variants.board.board I1 I1)
    let internal pathTP_FutureValidity () = path0 (ODIS_Variants.board.board I1 I2)
    let internal pathCanopy_FutureValidity () = path4 (ODIS_Variants.board.board I1 I2)
    let internal pathTP_LongTermValidity () = path0 (ODIS_Variants.board.board I2 I1)
    let internal pathCanopy_LongTermValidity () = path4 (ODIS_Variants.board.board I2 I1)
        
    (*
    Right-click g:\Users\User\... (vsechny adresare anebo adresar nad tim), select Properties > Security, and grant full control.
        
    do *.fsproj pridat:
    <PropertyGroup Condition="$(TargetPlatformIdentifier) == 'windows'">
	    <DefineConstants>WINDOWS</DefineConstants>
    </PropertyGroup> 
    *)