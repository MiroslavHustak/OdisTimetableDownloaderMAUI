namespace Settings

open System

module Messages = 

    let internal msg1CurrentValidity = "Stahují se aktuálně platné JŘ ODIS ..."
    let internal msg2CurrentValidity = "Momentálně nejsou dostupné odkazy na aktuálně platné JŘ ODIS. Chvíli strpení, prosím."
    let internal msg3CurrentValidity = "Aktuálně platné JŘ ODIS nebyly k dispozici pro stažení."

    let internal msg1FutureValidity = "Stahují se JŘ ODIS platné v budoucnosti ..."
    let internal msg2FutureValidity = "Momentálně nejsou dostupné odkazy na JŘ ODIS platné v budoucnosti. Chvíli strpení, prosím."
    let internal msg3FutureValidity = "JŘ ODIS platné v budoucnosti nebyly k dispozici pro stažení."

    let internal msg1WithoutReplacementService = "Stahují se teoreticky dlouhodobě platné JŘ ODIS ..."
    let internal msg2WithoutReplacementService = "Momentálně nejsou dostupné odkazy na dlouhodobě platné JŘ ODIS. Chvíli strpení, prosím."
    let internal msg3WithoutReplacementService = "Dlouhodobě platné JŘ ODIS nebyly k dispozici pro stažení."

    let internal jsonDownloadError = "Došlo k chybě, JSON soubory nebyly úspěšně staženy." 
    let internal jsonCancel = "Stahování JSON souborů předčasně ukončeno." 
    let internal rcError = "Chyba při zpracování dat, JŘ ODIS nebyly úspěšně staženy." 
    let internal jsonFilteringError = "Chyba při zpracování JSON, JŘ ODIS nebyly úspěšně staženy." 
    let internal dataFilteringError = "Chyba při filtrování dat, JŘ ODIS nebyly úspěšně staženy." 
    let internal fileDeleteError = "Chyba při mazání starých souborů, JŘ ODIS nebyly úspěšně staženy." 
    let internal folderCopyingError = "Chyba při zálohování starých souborů, restartuj aplikaci." 
    let internal folderMovingError = "Chyba při zálohování starých souborů, restartuj aplikaci." 
    let internal createFolderError = String.Empty //"Chyba při tvorbě adresářů, JŘ ODIS nebyly úspěšně staženy."
    let internal createFolderError2 = String.Empty //"Chyba při tvorbě adresáře pro TP_Canopy_Difference." 
    let internal noFolderError = "Nejsou vytvořeny adresáře pro příslušné soubory, JŘ ODIS nebyly úspěšně staženy."

    #if ANDROID
    let internal fileDownloadError =
        sprintf "%s %s"
        <| "Chyba při stahování JŘ ODIS."
        <| "Vypni aplikaci, připoj se k internetu, pokud je třeba, a spusť ji znovu." 
    #else
    let internal fileDownloadError = "Chyba při stahování pdf souborů, JŘ ODIS nebyly úspěšně staženy." 
    #endif

    let internal canopyError = "REST API error." 

    let internal deleteOldTimetablesMsg1 = "Chvíli strpení, usilovně odstraňuji zálohované předchozí JŘ ..."  
    let internal deleteOldTimetablesMsg2 = "Odstranění zálohovaných předchozích JŘ úspěšně provedeno."
    let internal deleteOldTimetablesMsg3 = "Při odstraňování zálohovaných předchozích JŘ došlo k problému, ověř, že nemáš otevřen předchozí JŘ."

    let internal dispatchMsg1 = "Dokončeno stahování JSON souborů." 
    let internal dispatchMsg2 = "Chvíli strpení, usilovně třídím, filtruji či provádím další pomocné operace, které nikoho nezajímají ..."
    let internal dispatchMsg3 = "Kompletní balík JŘ ODIS úspěšně stažen." 
    
    let internal dispatchMsg0 = "Došlo k chybě, JŘ ODIS nebyly staženy, navíc ani úklid se nepodařil."  
    let internal dispatchMsg4 = "Došlo k chybě, JŘ ODIS nebyly úspěšně staženy."  

    let internal mdpoMsg0 = "Došlo k chybě, JŘ MDPO nebyly staženy, navíc ani úklid se nepodařil."  
    let internal mdpoMsg1 = "Došlo k chybě, JŘ MDPO nebyly staženy."
    let internal mdpoMsg2 = "Došlo k chybě, všechny JŘ MDPO nebyly úspěšně staženy." 
    let internal mdpoCancelMsg = "Stahování JŘ MDPO předčasně ukončeno."
    let internal mdpoCancelMsg1 = "Stahování JŘ MDPO předčasně ukončeno, úklid se nepodařil."

    let internal dpoMsg0 = "Došlo k chybě, JŘ DPO nebyly staženy, navíc ani úklid se nepodařil."  
    let internal dpoMsg1 = "Došlo k chybě, JŘ DPO nebyly staženy."  
    let internal dpoMsg2 = "Došlo k chybě, všechny JŘ DPO nebyly úspěšně staženy." 
    let internal dpoCancelMsg = "Stahování JŘ DPO předčasně ukončeno."
    let internal dpoCancelMsg1 = "Stahování JŘ DPO předčasně ukončeno, úklid se nepodařil."
  
    let internal progressMsgKodis = "Stahují se JSON soubory potřebné pro stahování JŘ ODIS ..." 
    let internal progressMsgKodis1 = "Varianta bez stahování JSON souborů a s použitím web API. Vyčkej na zahájení ..." 
    let internal progressMsgDpo = "Zálohují se staré JŘ DPO a stahují se nové ..." 
    let internal progressMsgMdpo = "Zálohují se staré zastávkové JŘ MDPO a stahují se nové ..."

    let internal mauiDpoMsg = "JŘ DPO úspěšně staženy."
    let internal mauiMdpoMsg = "Zastávkové JŘ MDPO úspěšně staženy."

    let internal labelOdis = "Stahování JŘ ODIS"

    let internal buttonClearing = "Odstranit předchozí JŘ"  
    let internal hintClearing = "Odstranění předchozích JŘ"
    let internal buttonKodis = "Kompletní JŘ ODIS (Json TP)"   
    let internal buttonKodis4 = "Kompletní JŘ ODIS (Canopy)"   
    let internal hintOdis = "Stahování kompletních JŘ ODIS všech dopravců"
    let internal buttonDpo = "JŘ dopravce DPO"
    let internal hintDpo = "Stahování aktuálních JŘ dopravce DPO"
    let internal buttonMdpo = "JŘ dopravce MDPO"
    let internal hintMdpo = "Stahování zastávkových JŘ dopravce MDPO"
    let internal hintCancel = "Zrušení práce aplikace"
    let internal buttonCancel = "Zrušit stahování"   
    let internal buttonRestart = "Restart"
    let internal hintRestart = "Zpět na úvod anebo restart aplikace"    
    let internal buttonHome = "Zpět na úvod"  
    let internal buttonRequestPermission = "Spustit AppInfo"  
   
    let internal noNetConn = "Není přístup k internetu." 
    let internal noNetConnPlus = "Kvůli přerušení připojení k internetu bude činnost aplikace ukončena, vyčkej na Restart." 
    let internal noNetConnPlusPlus = "Není přístup k internetu, počkám zhruba dvě minuty." 
    let internal noNetConn1 = "Operace nebyla provedena, není přístup k internetu." 
    let internal noNetConn2 = "Není přístup k internetu, nutno vyčkat, až bude." 
    let internal noNetConnInitial = "Aplikace vyžaduje připojení k internetu. Vypni aplikaci, připoj se k internetu a spusť ji znovu."  
   
    let internal yesNetConn = "Přípojení k internetu funguje." 
   
    let internal ctsMsg2 = "Nebylo možné detekovat ani vytvořit adresáře pro stahované JŘ." 
    let internal ctsMsg = "Problém s detekcí internetového připojení."
    
    let internal cancelMsg1 = "Kvůli přerušení připojení k internetu se činnost aplikace ukončuje, může to chvíli trvat ..."
    let internal cancelMsg1NoConn = "Po obnovení připojení k internetu se činnost aplikace ukončí, může to chvíli trvat ..."
    let internal cancelMsg2 = "Činnost aplikace byla předčasně ukončena."
    let internal cancelMsg3 = "Chvíli strpení, stahování se ukončuje ..."

    let internal cancelMsg4 = "Stahování JŘ KODIS předčasně ukončeno."
    let internal cancelMsg5 = "Stahování JŘ KODIS předčasně ukončeno, úklid se nepodařil."

    let internal netConnError = "Přerušené internetové připojení způsobilo přerušení stahování souborů. Stahuj vše znova."
    let internal unKnownError = "Chyba, kterou se mi už nechce diagnostikovat, způsobila přerušení stahování souborů. Stahuj vše znova."

    let internal buttonQuit = "Vypnout aplikaci" 
    let internal quitError = "Nepodařilo se vypnout aplikaci, zkus znovu" 

    let internal appInfoInvoker = 
        sprintf "%s %s %s"
        <| "Objevilo se tlačítko \"Spustit AppInfo\"?" 
        <| "Klepni na něj, povol manipulaci se soubory a restartuj aplikaci." 
        <| "U Androidu 10- se k tomu povolení asi budeš muset proklepat."

    let (|Second|Seconds|SecondPlural|) = function 1 -> Second | 2 | 3 | 4 -> Seconds | _ -> SecondPlural

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