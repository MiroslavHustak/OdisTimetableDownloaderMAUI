namespace Settings

open System

module Messages = 

    let [<Literal>] internal msg1CurrentValidity = "Stahují se aktuálně platné JŘ ODIS ..."
    let [<Literal>] internal msg2CurrentValidity = "Momentálně nejsou dostupné odkazy na aktuálně platné JŘ ODIS. Chvíli strpení, prosím."
    let [<Literal>] internal msg3CurrentValidity = "Aktuálně platné JŘ ODIS nebyly k dispozici pro stažení."

    let [<Literal>] internal msg1FutureValidity = "Stahují se JŘ ODIS platné v budoucnosti ..."
    let [<Literal>] internal msg2FutureValidity = "Momentálně nejsou dostupné odkazy na JŘ ODIS platné v budoucnosti. Chvíli strpení, prosím."
    let [<Literal>] internal msg3FutureValidity = "JŘ ODIS platné v budoucnosti nebyly k dispozici pro stažení."

    let [<Literal>] internal msg1WithoutReplacementService = "Stahují se teoreticky dlouhodobě platné JŘ ODIS ..."
    let [<Literal>] internal msg2WithoutReplacementService = "Momentálně nejsou dostupné odkazy na dlouhodobě platné JŘ ODIS. Chvíli strpení, prosím."
    let [<Literal>] internal msg3WithoutReplacementService = "Dlouhodobě platné JŘ ODIS nebyly k dispozici pro stažení."

    let [<Literal>] internal jsonDownloadError = "Došlo k chybě, JSON soubory nebyly úspěšně staženy. Nejpravděpodobnější příčinou je přerušení přístupu k internetu." 
    let [<Literal>] internal jsonCancel = "Stahování JSON souborů předčasně ukončeno." 
    let [<Literal>] internal rcError = "Chyba při zpracování dat, JŘ ODIS nebyly úspěšně staženy." 
    let [<Literal>] internal jsonFilteringError = "Chyba při zpracování JSON, JŘ ODIS nebyly úspěšně staženy." 
    let [<Literal>] internal dataFilteringError = "Chyba při filtrování dat, JŘ ODIS nebyly úspěšně staženy." 
    let [<Literal>] internal fileDeleteError = "Chyba při mazání starých souborů, JŘ ODIS nebyly úspěšně staženy." 
    let [<Literal>] internal folderCopyingError = "Chyba při zálohování starých souborů, restartuj aplikaci." 
    let [<Literal>] internal folderMovingError = "Chyba při zálohování starých souborů, restartuj aplikaci." 
    let internal createFolderError = String.Empty //"Chyba při tvorbě adresářů, JŘ ODIS nebyly úspěšně staženy."
    let internal createFolderError2 = String.Empty //"Chyba při tvorbě adresáře pro TP_Canopy_Difference." 
    let [<Literal>] internal noFolderError = "Nejsou vytvořeny adresáře pro příslušné soubory, JŘ ODIS nebyly úspěšně staženy."

    #if ANDROID
    let internal fileDownloadError =
        sprintf "%s %s"
        <| "Chyba při stahování JŘ ODIS."
        <| "Vypni aplikaci, připoj se k internetu, pokud je třeba, a spusť ji znovu." 
    #else
    let [<Literal>] internal fileDownloadError = "Chyba při stahování pdf souborů, JŘ ODIS nebyly úspěšně staženy." 
    #endif

    let[<Literal>]  internal canopyError = "REST API error." 

    let [<Literal>] internal deleteOldTimetablesMsg1 = "Chvíli strpení, usilovně odstraňuji zálohované předchozí JŘ ..."  
    let [<Literal>] internal deleteOldTimetablesMsg2 = "Odstranění zálohovaných předchozích JŘ úspěšně provedeno."
    let [<Literal>] internal deleteOldTimetablesMsg3 = "Při odstraňování zálohovaných předchozích JŘ došlo k problému, ověř, že nemáš otevřen předchozí JŘ."

    let [<Literal>] internal dispatchMsg1 = "Dokončeno stahování JSON souborů." 
    let [<Literal>] internal dispatchMsg2 = "Chvíli strpení, usilovně třídím, filtruji či provádím další pomocné operace, které nikoho nezajímají ..."
    let [<Literal>] internal dispatchMsg3 = "Kompletní balík JŘ ODIS úspěšně stažen." 
    
    let [<Literal>] internal dispatchMsg0 = "Došlo k chybě, pravděpodobně JŘ ODIS nebyly staženy, navíc ani úklid se nepodařil. Nejpravděpodobnější příčinou je přerušení přístupu k internetu."  
    let [<Literal>] internal dispatchMsg4 = "Došlo k chybě, JŘ ODIS nebyly úspěšně staženy. Nejpravděpodobnější příčinou je přerušení přístupu k internetu."  

    let [<Literal>] internal mdpoMsg0 = "Došlo k chybě, JŘ MDPO nebyly staženy, navíc ani úklid se nepodařil. Nejpravděpodobnější příčinou je přerušení přístupu k internetu."  
    let [<Literal>] internal mdpoMsg1 = "Došlo k chybě, JŘ MDPO nebyly staženy. Nejpravděpodobnější příčinou je přerušení přístupu k internetu."
    let [<Literal>] internal mdpoMsg2 = "Došlo k chybě, všechny JŘ MDPO nebyly úspěšně staženy. Nejpravděpodobnější příčinou je přerušení přístupu k internetu." 
    let [<Literal>] internal mdpoCancelMsg = "Stahování JŘ MDPO předčasně ukončeno."
    let [<Literal>] internal mdpoCancelMsg1 = "Stahování JŘ MDPO předčasně ukončeno, úklid se nepodařil."

    let [<Literal>] internal dpoMsg0 = "Došlo k chybě, JŘ DPO nebyly staženy, navíc ani úklid se nepodařil. Nejpravděpodobnější příčinou je přerušení přístupu k internetu."  
    let [<Literal>] internal dpoMsg1 = "Došlo k chybě, JŘ DPO nebyly staženy. Nejpravděpodobnější příčinou je přerušení přístupu k internetu."  
    let [<Literal>] internal dpoMsg2 = "Došlo k chybě, všechny JŘ DPO nebyly úspěšně staženy. Nejpravděpodobnější příčinou je přerušení přístupu k internetu." 
    let [<Literal>] internal dpoCancelMsg = "Stahování JŘ DPO předčasně ukončeno."
    let [<Literal>] internal dpoCancelMsg1 = "Stahování JŘ DPO předčasně ukončeno, úklid se nepodařil."
  
    let [<Literal>] internal progressMsgKodis = "Stahují se JSON soubory potřebné pro stahování JŘ ODIS ..." 
    let [<Literal>] internal progressMsgKodis1 = "Varianta bez stahování JSON souborů a s použitím web API. Vyčkej na zahájení ..." 
    let [<Literal>] internal progressMsgDpo = "Zálohují se staré JŘ DPO a stahují se nové ..." 
    let [<Literal>] internal progressMsgMdpo = "Zálohují se staré zastávkové JŘ MDPO a stahují se nové ..."

    let [<Literal>] internal mauiDpoMsg = "JŘ DPO úspěšně staženy."
    let [<Literal>] internal mauiMdpoMsg = "Zastávkové JŘ MDPO úspěšně staženy."

    let [<Literal>] internal labelOdis = "Stahování JŘ ODIS"

    let [<Literal>] internal buttonClearing = "Odstranit předchozí JŘ"  
    let [<Literal>] internal hintClearing = "Odstranění předchozích JŘ"
    let [<Literal>] internal buttonKodis = "Kompletní JŘ ODIS (Json TP)"   
    let [<Literal>] internal buttonKodis4 = "Kompletní JŘ ODIS (Canopy)"   
    let [<Literal>] internal hintOdis = "Stahování kompletních JŘ ODIS všech dopravců"
    let [<Literal>] internal buttonDpo = "JŘ dopravce DPO"
    let [<Literal>] internal hintDpo = "Stahování aktuálních JŘ dopravce DPO"
    let [<Literal>] internal buttonMdpo = "JŘ dopravce MDPO"
    let [<Literal>] internal hintMdpo = "Stahování zastávkových JŘ dopravce MDPO"
    let [<Literal>] internal hintCancel = "Zrušení práce aplikace"
    let [<Literal>] internal buttonCancel = "Zrušit stahování"   
    let [<Literal>] internal buttonRestart = "Restart"
    let [<Literal>] internal hintRestart = "Zpět na úvod anebo restart aplikace"    
    let [<Literal>] internal buttonHome = "Zpět na úvod"  
    let [<Literal>] internal buttonRequestPermission = "Spustit AppInfo"  

    let [<Literal>] internal buttonClearingConfirmation = "Ano, pryč s nimi"
    let [<Literal>] internal buttonClearingCancel = "Ponechat"
   
    let [<Literal>] internal noNetConn = "Není přístup k internetu." 
    let [<Literal>] internal noNetConnPlus = "Kvůli přerušení připojení k internetu bude činnost aplikace ukončena." 
    let [<Literal>] internal noNetConnPlusPlus = "Není přístup k internetu, počkám zhruba dvě minuty." 
    let [<Literal>] internal noNetConn1 = "Operace nebyla provedena, není přístup k internetu." 
    let [<Literal>] internal noNetConn2 = "Není přístup k internetu, nutno vyčkat, až bude." 
    let [<Literal>] internal noNetConnInitial = "Aplikace vyžaduje připojení k internetu. Vypni aplikaci, připoj se k internetu a spusť ji znovu."  
   
    let [<Literal>] internal yesNetConn = "Přípojení k internetu funguje." 
   
    let [<Literal>] internal ctsMsg2 = "Nebylo možné detekovat ani vytvořit adresáře pro stahované JŘ." 
    let [<Literal>] internal ctsMsg = "Problém s detekcí internetového připojení."
    
    let [<Literal>] internal cancelMsg1 = "Kvůli přerušení připojení k internetu se činnost aplikace ukončuje, může to chvíli trvat ..."
    let [<Literal>] internal cancelMsg1NoConn = "Po obnovení připojení k internetu se činnost aplikace ukončí, může to chvíli trvat ..."
    let [<Literal>] internal cancelMsg2 = "Činnost aplikace byla předčasně ukončena."
    let [<Literal>] internal cancelMsg3 = "Chvíli strpení, stahování se ukončuje ..."

    let [<Literal>] internal cancelMsg4 = "Stahování JŘ KODIS předčasně ukončeno."
    let [<Literal>] internal cancelMsg5 = "Stahování JŘ KODIS předčasně ukončeno, úklid se nepodařil."

    let [<Literal>] internal netConnError = "Přerušené internetové připojení způsobilo přerušení stahování souborů. Stahuj vše znova."
    let [<Literal>] internal unKnownError = "Chyba, kterou se mi už nechce diagnostikovat, způsobila přerušení stahování souborů. Stahuj vše znova."

    let [<Literal>] internal buttonQuit = "Vypnout aplikaci" 
    let [<Literal>] internal buttonQuit2 = "Vypnout odpočítávání a aplikaci" 
    let [<Literal>] internal quitError = "Nepodařilo se vypnout aplikaci, zkus znovu" 

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

    let internal continueDownload = "Pokusím se po přerušení obnovit činnost (vznik chyby je možný), v případě neúspěchu vypni a znovu spusť aplikaci."
    #else           
    let internal quitMsg param =
        match param with
        | Second       -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřinu ..." param
        | Seconds      -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřiny ..." param 
        | SecondPlural -> sprintf "Není připojení k internetu, aplikace bude vypnuta za %i vteřin ..." param 

    let internal continueDownload = String.Empty
     #endif