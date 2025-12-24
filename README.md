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
│   ├── Types.fs
│   ├── TypeAlgebra.fs
│   └── ErrorTypes.fs
├── Settings/
│   ├── Messages.fs
│   ├── SettingsGeneral.fs
│   ├── SettingsDPO.fs
│   ├── SettingsKODIS.fs
│   └── SettingsMDPO.fs
├── ComputationExpressions/
│   └── CEBuilders.fs
├── ErrorHandling/
│   └── ErrorHandlers.fs
├── Helpers/
│   ├── IO_Monad_Experiments/
│   │   └── IO_Monad.fs
│   ├── CopyOrMoveDir.fs
│   ├── Helpers.fs
│   ├── Serialization.fs
│   ├── Parsing.fs│   
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
├── BusinessLogic/
│   ├── DataManipulation/
│   │   ├── PureFunctions/
│   │   │   └── SortRecordData.fs
│   │   └── ImpureFunctions/
│   │       ├── ParseJsonDataFull.fs
│   │       ├── ParseJsonData.fs
│   │       └── FilterTimetableLinks.fs
│   ├── IO_Operations/
│   │   ├── PureFunctions/
│   │   │   └── CreatePathsAndNames.fs
│   │   └── ImpureFunctions/
│   │       ├── FutureLinks.fs
│   │       └── IO_Operations.fs
│   ├── MainBusinessLogic/
│   │   ├── DPO_BL.fs
│   │   ├── MDPO_BL.fs
│   │   ├── KODIS_BL_Record.fs
│   │   ├── KODIS_BL_Record4.fs
│   │   └── TP_Canopy_Difference.fs
├── ApplicationDesign/
│   ├── DPO.fs
│   ├── MDPO.fs
│   ├── KODIS_Record.fs
│   └── KODIS_Record4.fs
├── XElmish/ (minimal design for stress testing only)
    ├── HardRestart.fs
    ├── AndroidSpecificCode.fs
│   ├── ProgressCircle.fs
│   ├── Counters.fs
│   └── App.fs
├── Resources/
│   ├── AppIcon/
│   │   └── appicon.svg
│   ├── Fonts/
│   │   └── * (all font files)
│   ├── Images/
│   │   ├── dotnet_bot.svg
│   │   └── * (other image files)
│   ├── Raw/
│   │   └── * (raw assets)
│   └── Splash/
│       └── splash.svg
├── Platforms/
│   ├── Android/
│   │   ├── Resources/
│   │   │   ├── xml/
│   │   │   │   └── network_security_config.xml
│   │   │   ├── values/
│   │   │   │   └── colors.xml
│   │   ├── Assets/
│   │   │   └── **/* (all asset files, excluding hidden folders)
│   │   └── AndroidManifest.xml
│   ├── iOS/
│   │   └── Info.plist
│   ├── macCatalyst/
│   │   └── Info.plist
│   └── Windows/
│       ├── app.manifest
│       └── Package.appxmanifest
├── logs/ (empty folder)
├── bin/
│   └── Release/
│       └── net8.0-windows10.0.19041.0/
│           └── win10-x64/ (empty folder)
├── Monadic_composition.txt
├── TODO_list.txt
├── CppHelpers.dll (external, copied to output)
├── string_combine_dll.dll (external, copied to output)
├── MauiProgram.fs
├── MainActivity.fs (Android-specific)
├── MainApplication.fs (Android-specific)
├── AppDelegate.fs (iOS and macCatalyst-specific)
├── Program.fs (iOS and macCatalyst-specific)
├── App.fs (Windows-specific)
├── Main.fs (Windows-specific)
└── Project References/
    └── EmbeddedTP.fsproj
``` </pre>
