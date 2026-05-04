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
CppHelpers (Project)
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
├── DotNetInteroperabilityCode/
│   └── DotNetInteroperabilityCode.fs
├── JavaInteroperabilityCode/
│   ├── RealInternetChecker.fs
│   └── SSL_TLS_handling.fs
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
├── CEBuilders/
│   └── CEBuilders.fs
├── OptionResultExtensions/
│   ├── ResultExtensions.fs
│   └── OptionExtensions.fs
├── Helpers/
│   ├── IO_Monad_Experiments/
│   │   └── IO_Monad.fs 
│   ├── Helpers.fs
│   ├── Serialization.fs
│   └── Parsers.fs
├── ProgressTrackers/
│   └── ProgressTrackers.fs
├── Libraries/
│   ├── CopyOrMoveDir.fs
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
│   │       ├── ParseJsonData.fs
│   │       └── FilterTimetableLinks.fs
│   ├── IO_Operations/
│   │   ├── PureHelpers/
│   │   │   └── CreatePathsAndNames.fs
│   │   └── ImpureFunctions/
│   │       ├── FutureValidityRestApi.fs
│   │       └── IO_Operations.fs
│   └── MainBusinessLogic_R/
│       ├── KodisJsonTP/
│       │   ├── KODIS_BL_Record_R_Json.fs
│       │   └── KODIS_BL_Record_R.fs
│       ├── KodisCanopy/
│       │   ├── KODIS_BL_Record4_R_Json.fs
│       │   └── KODIS_BL_Record4_R.fs
│       ├── DPO_BL_R.fs
│       ├── MDPO_BL_R.fs
│       └── TP_Canopy_Difference_R.fs
├── ApplicationDesign_R/
│   ├── KodisJsonTP/
│   │   └── KODIS_Record_R.fs
│   ├── KodisCanopy/
│   │   └── KODIS_Record4_R.fs
│   ├── DPO_R.fs
│   └── MDPO_R.fs
├── XElmish/
│   ├── Infrastructure/
│   │   ├── ResultDirectoryLauncher.fs
│   │   ├── ComparisonResultFileLauncher.fs
│   │   ├── HardRestart.fs
│   │   ├── AndroidSpecificCode.fs
│   │   ├── Counters.fs
│   │   └── ActorModels.fs
│   ├── ViewHelpers/
│   │   ├── ScreenHelpers.fs
│   │   └── ProgressCircleUI.fs
│   ├── Engines/
│   │   ├── KodisTP.fs
│   │   ├── KodisCanopy.fs
│   │   ├── Dpo.fs
│   │   └── Mdpo.fs
│   ├── EducationalCode/
│   │   └── Test_App_Old.fs
│   └── App_New_UX.fs
├── Platforms/
│   └── Android/
│       └── Resources/
│           ├── xml/
│           │   └── network_security_config.xml
│           └── values/
│               └── colors.xml
└── MauiProgram.fs
``` </pre>
