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
    let internal fileDownloadError = "Chyba při stahování pdf souborů, JŘ ODIS nebyly úspěšně staženy." 

    let internal dispatchMsg1 = "Dokončeno stahování JSON souborů." 
    let internal dispatchMsg2 = "Chvíli strpení, prosím, CPU se snaží, co může ..."
    let internal dispatchMsg3 = "Kompletní balík JŘ ODIS úspěšně stažen." 
    let internal dispatchMsg4 = "Došlo k chybě, JŘ ODIS nebyly úspěšně staženy."  

    let internal mdpoMsg1 = "Došlo k chybě, JŘ MDPO nebyly staženy."  
    let internal mdpoMsg2 = "Došlo k chybě, všechny JŘ MDPO nebyly úspěšně staženy." 
    let internal mdpoCancelMsg = "Stahování JŘ MDPO předčasně ukončeno uživatelem."

    let internal dpoMsg1 = "Došlo k chybě, JŘ DPO nebyly staženy."  
    let internal dpoMsg2 = "Došlo k chybě, všechny JŘ DPO nebyly úspěšně staženy." 
    let internal dpoCancelMsg = "Stahování JŘ DPO předčasně ukončeno uživatelem."

    let internal progressMsgKodis = "Stahují se JSON soubory potřebné pro stahování JŘ ODIS ..." 
    let internal progressMsgDpo = "Stahují se JŘ DPO ..." 
    let internal progressMsgMdpo = "Stahují se zastávkové JŘ MDPO ..."

    let internal mauiDpoMsg = "JŘ DPO úspěšně staženy."
    let internal mauiMdpoMsg = "Zastávkové JŘ MDPO úspěšně staženy."

    let internal labelOdis = "Stahování JŘ ODIS"
    let internal buttonKodis = "Stahuj kompletní balík JŘ ODIS"   
    let internal hintOdis = "Stahování kompletních JŘ ODIS všech dopravců"
    let internal buttonDpo = "Stahuj JŘ dopravce DPO"
    let internal hintDpo = "Stahování aktuálních JŘ dopravce DPO"
    let internal buttonMdpo = "Stahuj JŘ dopravce MDPO"
    let internal hintMdpo = "Stahování zastávkových JŘ dopravce MDPO"
    let internal hintCancel = "Zrušení práce aplikace"
   
    let internal noNetConn = "Není přístup k internetu, zkus to za chvíli znovu."  
    
    let internal cancelMsg1 = "Proces se ukončuje, může to chvíli trvat ..."
    let internal cancelMsg2 = "Stahování JŘ ODIS předčasně ukončeno uživatelem."