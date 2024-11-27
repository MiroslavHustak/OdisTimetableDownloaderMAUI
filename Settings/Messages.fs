namespace Settings

open System

module Messages = 

    let internal msg1CurrentValidity = "Stahují se aktuálně platné JŘ ODIS ..."
    let internal msg2CurrentValidity = "Momentálně nejsou dostupné odkazy na aktuálně platné JŘ ODIS."
    let internal msg3CurrentValidity = "Aktuálně platné JŘ ODIS nebyly k dispozici pro stažení."

    let internal msg1FutureValidity = "Stahují se JŘ ODIS platné v budoucnosti ..."
    let internal msg2FutureValidity = "Momentálně nejsou dostupné odkazy na JŘ ODIS platné v budoucnosti."
    let internal msg3FutureValidity = "JŘ ODIS platné v budoucnosti nebyly k dispozici pro stažení."

    let internal msg1WithoutReplacementService = "Stahují se teoreticky dlouhodobě platné JŘ ODIS ..."
    let internal msg2WithoutReplacementService = "Momentálně nejsou dostupné odkazy na dlouhodobě platné JŘ ODIS."
    let internal msg3WithoutReplacementService = "Dlouhodobě platné JŘ ODIS nebyly k dispozici pro stažení."

    let internal jsonDownloadError = "Došlo k chybě, JSON soubory nebyly úspěšně staženy." 
    let internal rcError = "Chyba při zpracování dat, JŘ ODIS nebyly úspěšně staženy." 
    let internal jsonFilteringError = "Chyba při zpracování JSON, JŘ ODIS nebyly úspěšně staženy." 
    let internal dataFilteringError = "Chyba při filtrování dat, JŘ ODIS nebyly úspěšně staženy." 
    let internal fileDeleteError = "Chyba při mazání starých souborů, JŘ ODIS nebyly úspěšně staženy." 
    let internal createFolderError = "Chyba při tvorbě adresářů, JŘ ODIS nebyly úspěšně staženy." 
    let internal noFolderError = "Nejsou vytvořeny adresáře pro příslušné soubory, JŘ ODIS nebyly úspěšně staženy."
    let internal fileDownloadError = "Chyba při stahování pdf souborů, JŘ ODIS nebyly úspěšně staženy." 
    let internal canopyError = "REST API error." 

    let internal dispatchMsg1 = "Dokončeno stahování JSON souborů." 
    let internal dispatchMsg2 = "Chvíli strpení, prosím, CPU se snaží, co může ..."
    let internal dispatchMsg3 = "Kompletní balík JŘ ODIS úspěšně stažen." 
    let internal dispatchMsg4 = "Došlo k chybě, JŘ ODIS nebyly úspěšně staženy."  

    let internal mdpoMsg1 = "Došlo k chybě, JŘ MDPO nebyly staženy."  
    let internal mdpoMsg2 = "Došlo k chybě, všechny JŘ MDPO nebyly úspěšně staženy." 
    let internal mdpoCancelMsg = "Stahování JŘ MDPO předčasně ukončeno."

    let internal dpoMsg1 = "Došlo k chybě, JŘ DPO nebyly staženy."  
    let internal dpoMsg2 = "Došlo k chybě, všechny JŘ DPO nebyly úspěšně staženy." 
    let internal dpoCancelMsg = "Stahování JŘ DPO předčasně ukončeno."

    let internal progressMsgKodis = "Stahují se JSON soubory potřebné pro stahování JŘ ODIS ..." 
    let internal progressMsgKodis1 = "Varianta bez stahování JSON souborů a s použitím web API." 
    let internal progressMsgDpo = "Stahují se JŘ DPO ..." 
    let internal progressMsgMdpo = "Stahují se zastávkové JŘ MDPO ..."

    let internal mauiDpoMsg = "JŘ DPO úspěšně staženy."
    let internal mauiMdpoMsg = "Zastávkové JŘ MDPO úspěšně staženy."

    let internal labelOdis = "Stahování JŘ ODIS"

    let internal buttonKodis = "Kompletní JŘ ODIS (Json TP)"   
    let internal buttonKodis4 = "Kompletní JŘ ODIS (Canopy)"   
    let internal hintOdis = "Stahování kompletních JŘ ODIS všech dopravců"
    let internal buttonDpo = "JŘ dopravce DPO"
    let internal hintDpo = "Stahování aktuálních JŘ dopravce DPO"
    let internal buttonMdpo = "JŘ dopravce MDPO"
    let internal hintMdpo = "Stahování zastávkových JŘ dopravce MDPO"
    let internal hintCancel = "Zrušení práce aplikace"
    let internal buttonCancel = "Ukončit stahování"     
    let internal hintRestart = "Zpět na úvod anebo restart aplikace"
    let internal buttonRestart = "Zpět na úvod" //"Restart"   
    let internal buttonHome = "Zpět na úvod"  
    let internal buttonRequestPermission = "Povolit manipulaci se soubory"  
   
    let internal noNetConn = "Není přístup k internetu." 
    let internal noNetConnPlus = "Kvůli přerušení připojení k internetu bude činnost aplikace ukončena, vyčkej na Restart." 
    let internal noNetConnPlusPlus = "Není přístup k internetu, počkám zhruba dvě minuty." 
    let internal noNetConn1 = "Operace nebyla provedena, není přístup k internetu." 
    let internal noNetConn2 = "Není přístup k internetu, nutno vyčkat, až bude." 
    let internal noNetConnInitial = "Aplikace vyžaduje připojení k internetu. Vypni aplikaci, připoj se na internet a spusť ji znovu."  
   
    let internal yesNetConn = "Přípojení k internetu funguje." 
    let internal yesNetConnPlus = "Vyčkej na objevení se tlačítka \"Restart\"."
   
    let internal ctsMsg = "Restartuj aplikaci. Pokud se tato chyba objeví vícekrát, kontaktuj programátora." 
    let internal ctsMsg2 = "Nebylo možné detekovat ani vytvořit adresáře pro stahované JŘ." 
    
    let internal cancelMsg1 = "Kvůli přerušení připojení k internetu se činnost aplikace ukončuje, může to chvíli trvat ..."
    let internal cancelMsg1NoConn = "Po obnovení připojení k internetu se činnost aplikace ukončí, může to chvíli trvat ..."
    let internal cancelMsg2 = "Činnost aplikace byla předčasně ukončena."

    let internal netConnError = "Přerušené internetové připojení způsobilo přerušení stahování souborů. Stahuj vše znova."
    let internal unKnownError = "Chyba, kterou se mi už nechce diagnostikovat, způsobila přerušení stahování souborů. Stahuj vše znova."

    let internal buttonQuit = "Okamžité vypnutí aplikace" 

    let (|Second|Seconds|SecondPlural|) =
        function 1 -> Second | 2 | 3 | 4 -> Seconds | _ -> SecondPlural

    // F# compiler directives
    #if WINDOWS 
    let internal quitMsg param =         
        match param with
        | Second       -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřinu, pokud nedojde k obnovení připojení." param
        | Seconds      -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřiny, pokud nedojde k obnovení připojení." param
        | SecondPlural -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřin, pokud nedojde k obnovení připojení." param

    let internal continueDownload = "Stahovací operace se po přerušení pokusí automaticky obnovit, vznik chyby je možný."
    #else           
    let internal quitMsg param =
        match param with
        | Second       -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřinu ..." param
        | Seconds      -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřiny ..." param 
        | SecondPlural -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřin ..." param 

    let internal continueDownload = String.Empty
     #endif