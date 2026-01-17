**Bulk downloader of ODIS timetables (preliminary code for an Android app)**

************************************************************

**The actual solution structure, revealing unidirectional F# project dependencies (as GitHub distorts the reality by displaying only an alphabetical order), is shown in the chart below:**
<pre lang="markdown"> ```
RustHelpers (DLL)
├── CombiningStrings/
│   └── lib.rs
├── CopyingAndMoving/
│   ├── copy_move.rs
│   └── lib.rs
│
CppHelpers (DLL and disabled project)
├── CppHelpers.vcxproj
├── Header Files/
│   ├── framework.h
│   └── pch.h
├── Source Files/
│   ├── dllmain.cpp
│   └── pch.cpp
├── Utilities/
│   ├── copyingDirectories.h
│   └── copyingDirectories.cpp
├── Utilities/
│   ├── movingDirectories.h
│   └── movingDirectories.cpp
│
EmbeddedTP (Project)
├── EmbeddedTP.fsproj
├── EmbeddedTP.fs
├── KODISJson/
│   ├── kodisMHDTotal.json
│   └── kodisMHDTotal2_0.json
│
OdisTimetableDownloaderMAUI (Solution)
├── OdisTimetableDownloaderMAUI.fsproj
├── AssemblyInfo/
│   └── AssemblyInfo.fs
├── JavaInteroperabilityCode/
│   └── JavaInteroperabilityCode.fs
├── NativeCode/
│   └── NativeCode.fs
├── Types/
│   ├── TDD.fs
│   ├── ErrorTypes.fs
│   ├── Types.fs
│   └── Grid3Algebra.fs  
├── Settings/
│   ├── Messages.fs
│   ├── SettingsGeneral.fs
│   ├── SettingsDPO.fs
│   ├── SettingsKODIS.fs
│   └── SettingsMDPO.fs
├── ApplicativeFunctors/
│   └── Applicatives.fs
├── ComputationExpressions/
│   └── CEBuilders.fs
├── OptionResultExtensions/ 
│   ├── ResultExtensions.fs
│   └── OptionExtensions.fs
├── Helpers/
│   ├── IO_Monad_Experiments/
│   │   └── IO_Monad.fs
│   ├── CopyOrMoveDir.fs
│   ├── Helpers.fs
│   ├── Serialization.fs
│   ├── Parsers.fs 
│   └── ListParallel.fs
├── Monads/
│   ├── FreeMonads/
│   │   ├── CmdLineWorkflows.fs
│   │   └── FreeMonad.fs
│   └── StateMonads/
│       └── StateMonad.fs
├── Connectivity/
│   └── Connectivity.fs
├── DataModelling/
│   ├── DataModels.fs
│   ├── DataTransferModels.fs
│   └── TransformationLayers.fs
├── Logging/
│   ├── LogEntries.fs
│   └── Logging.fs
├── ExceptionHandling/ 
│   └── ExceptionHandlers.fs  
├── BusinessLogic/
│   ├── DataManipulation/
│   │   ├── PureFunctions/
│   │   │   └── SortRecordData.fs
│   │   └── ImpureFunctions/
│   │       ├── ParseJsonDataFull.fs
│   │       ├── ParseJsonData.fs
│   │       └── FilterTimetableLinks.fs
│   ├── IO_Operations/
│   │   ├── PureHelpers/ 
│   │   │   └── CreatePathsAndNames.fs
│   │   └── ImpureFunctions/
│   │       ├── FutureLinks.fs
│   │       └── IO_Operations.fs
│   ├── MainBusinessLogicOld/   
│   │   └── ..... //not relevant 
│   └── MainBusinessLogic_R/
│       ├── DPO_BL_R.fs
│       ├── MDPO_BL_R.fs
│       ├── KODIS_BL_Record_R.fs
│       ├── KODIS_BL_Record4_R.fs
│       └── TP_Canopy_Difference_R.fs
├── ApplicationDesignOld/ 
│   └── ..... //not relevant
├── ApplicationDesign_R/
│   ├── DPO_R.fs
│   ├── MDPO_R.fs
│   ├── KODIS_Record_R.fs
│   └── KODIS_Record4_R.fs
├── XElmish/
│   ├── ComparisonResultFileLauncher.fs 
│   ├── HardRestart.fs
│   ├── ActorModels.fs 
│   ├── AndroidSpecificCode.fs
│   ├── ProgressCircle.fs
│   ├── Counters.fs
│   ├── App_R.fs 
│   └── ..... //not relevant 
├── Resources/   
│   └── ..... //not relevant       
├── Platforms/
│   ├── Android/
│   │   ├── Resources/
│   │   │   ├── xml/
│   │   │   │   └── network_security_config.xml
│   │   │   └── values/
│   │   │       └── colors.xml
│   │   ├── Assets/
│   │   │   └── ..... //not relevant 
│   │   ├── AndroidManifest.xml
│   │   ├── MainActivity.fs
│   │   └── MainApplication.fs   
│   └── Windows/│    
│       ├── App.fs
│       └── Main.fs
└── MauiProgram.fs
``` </pre>
