Bulk downloader of ODIS timetables (preliminary code for an Android app)

Nenašel by se někdo, kdo se vyzná v UI/UX/FE mobilních aplikací (nejlépe Avalonia nebo .NET MAUI) a je zároveň fanda do veřejné dopravy (aby měl motivaci)?

Naprogramoval jsem pro nadšence do klasických jízdních řádů na severní Moravě a ve Slezsku hromadný "stahovač" kompletně všech "klasických" JŘ ODIS, program lze samozřejmě rozšířit i na jiné kraje v ČR či SR či jinde. Proč tuto možnost často nenabízejí (či spíše nechtějí nabízet) příslušné instituce je už story ne pro diskuzi na GitHubu. 

Zatím mám "stahovač" v konzolové podobě (proof of concept) https://github.com/MiroslavHustak/OdisTimetableDownloader a velmi primitivní "androidní" podobě v tomto repozitáři [https://github.com/MiroslavHustak/OdisTimetableDownloaderMAUI/blob/master/App.fs](https://github.com/MiroslavHustak/OdisTimetableDownloaderMAUI/blob/master/XElmish/App.fs), abych se přesvědčil, že to na mobilu funguje. 

Prosím, neděste se toho, že kód je v Fabulous/Elmish/MVU (to je to, co vidíte v App.fs - domnívám se, že to snadno pochopíte a že vám to bude připadat daleko jednodušší, než C#, MVVM a XAML) a v F# (to je to, co vidíte všude). Můžete na mne mluvit i C Sharpem a XAMLem, já tomu porozumím (C# jsem opustil v době, kdy vyšla verze 7.3, s XAMLem jsem se potýkal před třemi lety). Kontrolky jsou zatím v .NET MAUI, takže se v tom rychle vyznáte. A s F# a Elmishem pomohu, pokud bude třeba (i když funkcionální programování je velmi jednoduché a intuitivní, to vám půjde samo). 

Chtělo by to danou appku "zmobilnit" do slušně vypadající podoby ve Fabulous (fabulous.dev), kontrolky nejlépe Avalonia nebo alespoň .NET MAUI. Našel by se někdo ochotný? 

Já osobně nemám talent pro UX/UI a z tohoto důvodu ani žádné velké nadšení pro FE. A ani jsem nic "mobilního" ještě nevyvíjel. Ale s Elmishem pomohu, co budu moci, už jsem v tom programoval. A samozřejme pomohu s F# obecně.

************************************************************
Solution structure (without Rust code and without Rust dll):
<pre lang="markdown"> ```
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

EmbeddedTP (Project)
├── EmbeddedTP.fsproj
├── EmbeddedTP.fs
├── KODISJson/
│   ├── kodisMHDTotal.json
│   └── kodisMHDTotal2_0.json

OdisTimetableDownloaderMAUI (Solution)
├── OdisTimetableDownloaderMAUI.fsproj
├── AssemblyInfo/
│   └── AssemblyInfo.fs
├── NativeCode/
│   └── NativeCode.fs
├── Types/
│   ├── TDD.fs
│   ├── Types.fs
│   └── ErrorTypes.fs
├── Settings/
│   ├── Messages.fs
│   ├── SettingsGeneral.fs
│   ├── SettingsDPO.fs
│   ├── SettingsKODIS.fs
│   └── SettingsMDPO.fs
├── ComputationExpressions/
│   └── CEBuilders.fs
├── FreeMonadSupport/
│   └── CmdLineWorkflows.fs
├── ErrorHandling/
│   └── ErrorHandlers.fs
├── Helpers/
│   ├── Helpers.fs
│   ├── Serialization.fs
│   ├── Parsing.fs
│   ├── HardRestart.fs
│   ├── ListParallel.fs
│   └── AndroidSpecificCode.fs
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
│   │       ├── SortJsonDataFull.fs
│   │       ├── SortJsonData.fs
│   │       └── FilterTimetableLinks.fs
│   └── IO_Operations/
│       ├── PureFunctions/
│       │   └── CreatePathsAndNames.fs
│       └── ImpureFunctions/
│           ├── FutureLinks.fs
│           └── IO_Operations.fs
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
├── XElmish/
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
├── Monadic_function_composition.txt
├── TODO_list.txt
├── CppHelpers.dll (external, copied to output)
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
