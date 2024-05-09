namespace Settings

open System

module Messages = ()
    
    //let [<Literal>] private formatBody = "\n%s%s"   

    (*
    let internal msg1 () = printfn "Zase se někdo vrtal v listu s odkazy a cestami. Je nutná jejich kontrola. Zmáčkni cokoliv pro ukončení programu." 
    let internal msg2 () = printfn "Probíhá stahování a ukládání JSON souborů ze stránek KODISu."
    let internal msg3 () = printfn "Dokončeno stahování a ukládání JSON souborů ze stránek KODISu."
    let internal msg4 () = printfn "Probíhá filtrace odkazů na neplatné jízdní řády."
    let internal msg5A = "Chyba v průběhu stahování JSON souborů."
    let internal msg5 () = printfn "%s" msg5A 
    let internal msg9 () = printfn "Chyba při filtraci odkazů na JŘ." 
    let internal msg10 () = printfn "Probíhá filtrace odkazů na neplatné jízdní řády. \nChvíli strpení, prosím ..."
    let internal msg11 () = printfn "Provedeno odstranění všech starých JŘ ve zvolené variantě, pokud existovaly. \nChvíli strpení, prosím ..."
    let internal msg12 () = printfn "Provedeno odstranění starých JŘ ve zvolené variantě, pokud existovaly. \nChvíli strpení, prosím ..."
    let internal msg13 () = printfn "Pravděpodobně někdo odstranil daný adresář v průběhu práce tohoto programu."  
    let internal msg14 () = printfn "Nadřel jsem se, ale úkol jsem úspěšně dokončil :-)"
    let internal msg15 () = printfn "Chvíli strpení, prosím..." 
    let internal msg16 = "Chyba v některé fázi procesu programu pro stahování JŘ. Ověř, zdali nepoužíváš adresáře, které se odstraňují či do kterých se zapisuje, například zdali nemáš otevřený některý JŘ v dotčených adresářích. "  
    let private s1 = "Došlo k přerušení připojení k internetu. Stahování souborů se musí ukončit, i kdyby zrovna došlo k obnovení připojení."
    let private s2 = "Zmáčknutím tlačítka Ok vypni tento program a pak jej spusť znovu."
    let internal msg17 () = sprintf "%s %s" s1 s2
    let private s3 = "Došlo k přerušení připojení k internetu."
    let private s4 = "Pokud již bylo zahájeno stahování JSON nebo PDF souborů, po obnovení připojení buď bude stahování pokračovat, anebo dojde k chybě - sleduj pokyny v textovém rozhraní nebo v dalším dialogovém okně."
    let internal msg18 = sprintf "%s %s" s3 s4
    let internal msg19 () = printfn "Log entries merged with the old ones in LogEntries2"
    let internal msg20 = "Chyba v průběhu stahování JŘ DPO." 
    let internal msg21 = "Chyba v průběhu stahování JŘ, u JŘ MDPO se to někdy stává. Zkus to za chvíli znovu."    
    let internal msg22 = "Chyba v průběhu stahování JŘ KODIS."
    let internal msg23 = "Chyba při paralelním stahování JŘ."
    let internal msg24 = "Chyba při rozdělování listu pro multi-threading."
    let internal msg25 () = printfn "Triggers activated"

    let internal msgParam1 = printfn formatBody "No jéje, někde nastala chyba. Zmáčkni cokoliv pro ukončení programu a zkus to znovu. Popis chyby: " 
    let internal msgParam2 = printfn formatBody "Jízdní řád s tímto odkazem se nepodařilo stáhnout: \n"  
    let internal msgParam3 = printfn "Probíhá stahování příslušných JŘ a jejich ukládání do [%s]."  
    let internal msgParam4 = printfn "Dokončeno stahování příslušných JŘ a jejich ukládání do [%s]. \nChvíli strpení, prosím..."  
    let internal msgParam5 = printfn "Adresář [%s] neexistuje, příslušné JŘ do něj určené nemohly být staženy." 
    let internal msgParam6 = printfn "Chyba v řetězci [%s]." 
    let internal msgParam7 = printfn"%s" 
    let internal msgParam8 = printfn "%s\n" 
    let internal msgParam9 = printf "%s\r" 
    let internal msgParam10 = printfn "Parsování neproběhlo korektně u tohoto řetězce: %s"  
    let internal msgParam11 = printfn "Soubor %s nenalezen" 
    let internal msgParam12 = printfn "Adresář %s nenalezen"
    let internal msgParam13 = printfn "Pro volbu stahování do adresáře [%s] nejsou momentálně dostupné žádné odkazy na JŘ. \nChvíli strpení, prosím..."   
    *)