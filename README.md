Bulk downloader of ODIS timetables (preliminary code for an Android app)

Nenašel by se někdo, kdo se vyzná v UI/UX/FE mobilních aplikací (nejlépe .NET MAUI, méně lépe Avalonia) a je zároveň fanda do veřejné dopravy (aby měl motivaci pro malý bezplatný projekt)?

Naprogramoval jsem pro nadšence do klasických jízdních řádů na severní Moravě a ve Slezsku hromadný "stahovač" kompletně všech "klasických" JŘ ODIS, program lze samozřejmě rozšířit i na jiné kraje v ČR či SR či jinde. Proč tuto možnost často nenabízejí (či spíše nechtějí nabízet) příslušné instituce je už story ne pro diskuzi na GitHubu. 

Zatím mám "stahovač" v konzolové podobě (proof of concept) https://github.com/MiroslavHustak/OdisTimetableDownloader a velmi primitivní "androidní" podobě v tomto repozitáři [https://github.com/MiroslavHustak/OdisTimetableDownloaderMAUI/blob/master/App.fs](https://github.com/MiroslavHustak/OdisTimetableDownloaderMAUI/blob/master/XElmish/App.fs), abych se přesvědčil, že to na mobilu funguje. 

Prosím, neděste se toho, že kód je v Fabulous/Elmish/MVU (to je to, co vidíte v App.fs - domnívám se, že to snadno pochopíte a že vám to bude připadat daleko jednodušší, než C#, MVVM a XAML) a v F# (to je to, co vidíte všude). Můžete na mne mluvit i C Sharpem a XAMLem, já tomu porozumím (C# jsem opustil v době, kdy vyšla verze 7.3, s XAMLem jsem se potýkal před třemi lety). Kontrolky jsou v .NET MAUI, takže se v tom rychle vyznáte. A s F# a Elmishem pomohu, pokud bude třeba (i když funkcionální programování je velmi jednoduché a intuitivní, to vám půjde samo). 

Chtělo by to danou appku "zmobilnit" do slušně vypadající podoby ve Fabulous (fabulous.dev), kontrolky buď .NET MAUI nebo Avalonia. Našel by se někdo ochotný? Díky moc. Úsilí mohu odměnit lahví skvělé slivovice.

Já osobně nemám talent pro UX/UI a z tohoto důvodu ani žádné velké nadšení pro FE. A ani jsem nic "mobilního" ještě nevyvíjel. Ale s Elmishem pomohu, co budu moci, už jsem v tom programoval. A samozřejme pomohu s F# obecně.
