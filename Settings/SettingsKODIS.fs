namespace Settings

open System

//************************

open SettingsGeneral

module SettingsKODIS =

    //************************Constants and types**********************************************************************

    //tu a tam zkontrolovat json, zdali KODIS nezmenil jeho strukturu 
     
    let internal partialPathJson = partialPathJsonTemp //viz SeetingsGeneral

    type internal Context2 = 
        {
            summerHolidayEnd1 : DateTime
            summerHolidayEnd2 : DateTime
            currentTime : DateTime
            dateTimeMinValue : DateTime     
        }

    let internal context2 =  
        {
            summerHolidayEnd1 = DateTime (2026, 8, 31)
            summerHolidayEnd2 = DateTime (2026, 8, 31) //(2026, 9, 1)
            currentTime = DateTime.Now.Date  //try with is overkill, as the system clock is a trusted dependency, and failures are too rare to justify catching.     
            dateTimeMinValue = DateTime.MinValue  
        }

    //let [<Literal>] internal pathKodisWeb = @"https://kodisweb-backend.herokuapp.com/"
    let [<Literal>] internal pathKodisWeb2 = @"https://kodis-backend-staging-85d01eccf627.herokuapp.com/api/linky-search?"
    let [<Literal>] internal pathKodisAmazonLink = @"https://kodis-files.s3.eu-central-1.amazonaws.com/" 

    let [<Literal>] internal lineNumberLength = 3 //3 je delka retezce pouze pro linky 001 az 999
    
    let internal sortedLines =
        [ 
            "linky 001-199"; "linky 200-299"; "linky 300-399"; 
            "linky 400-499"; "linky 500-599"; "linky 600-699"; 
            "linky 700-799"; "linky 800-899"; "linky 900-999"; 
            "vlakové linky S"; "vlakové linky R"; "linky X, NAD, P, AE"
        ]
    
    let [<Literal>] private interval = 12 //menit dle json struktury
    let [<Literal>] private lastCode = 132 //menit dle json struktury //delitelne 12

    let private codes = [ 0 .. interval .. lastCode ] |> List.map string //(fun x -> string x) 
        
    let private jsonLinkListPartial code =
        [        
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Brunt%C3%A1l&groups%5B1%5D=MHD%20%C4%8Cesk%C3%BD%20T%C4%9B%C5%A1%C3%ADn&groups%5B2%5D=MHD%20Fr%C3%BDdek-M%C3%ADstek&groups%5B3%5D=MHD%20Hav%C3%AD%C5%99ov&groups%5B4%5D=MHD%20Karvin%C3%A1&groups%5B5%5D=MHD%20Krnov&groups%5B6%5D=MHD%20Nov%C3%BD%20Ji%C4%8D%C3%ADn&groups%5B7%5D=MHD%20Opava&groups%5B8%5D=MHD%20Orlov%C3%A1&groups%5B9%5D=MHD%20Ostrava&groups%5B10%5D=MHD%20Stud%C3%A9nka&groups%5B11%5D=MHD%20T%C5%99inec&groups%5B12%5D=NAD%20MHD&start=" code "&limit=12"       
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=75&groups%5B1%5D=232-293&groups%5B2%5D=331-392&groups%5B3%5D=440-465&groups%5B4%5D=531-583&groups%5B5%5D=613-699&groups%5B6%5D=731-788&groups%5B7%5D=811-885&groups%5B8%5D=901-990&groups%5B9%5D=NAD&start=" code "&limit=12"           
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=S1-S34&groups%5B1%5D=R8-R62&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Brunt%C3%A1l&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20%C4%8Cesk%C3%BD%20T%C4%9B%C5%A1%C3%ADn&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Fr%C3%BDdek-M%C3%ADstek&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Hav%C3%AD%C5%99ov&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Karvin%C3%A1&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Krnov&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Nov%C3%BD%20Ji%C4%8D%C3%ADn&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Opava&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Orlov%C3%A1&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Ostrava&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20Stud%C3%A9nka&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=MHD%20T%C5%99inec&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=NAD%20MHD&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=75&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=232-293&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=331-392&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=440-465&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=531-583&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=613-699&start=" code "&limit=12"          
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=731-788&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=811-885&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=901-990&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=S1-S34&start=" code "&limit=12"
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=R8-R62&start=" code "&limit=12"            
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=NAD&start=" code "&limit=12" 
            sprintf "%s%s%s%s" pathKodisWeb2 "groups%5B0%5D=Lodní%20doprava&start=" code "&limit=12"
        ]

    let internal jsonLinkList3 = codes |> List.collect jsonLinkListPartial // (fun code -> jsonLinkListPartial code)
          
    let private pathToJsonListPartial code =     
        [         
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDTotal2_" code ".json"          
            sprintf "%s%s%s%s" partialPathJson @"kodisRegionTotal2_" code ".json"            
            sprintf "%s%s%s%s" partialPathJson @"kodisTrainTotal2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDBruntal2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDCT2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDFM2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDHavirov2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDKarvina2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDBKrnov2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDNJ2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDOpava2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDOrlova2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDOstrava2_" code ".json"           
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDStudenka2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDTrinec2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisMHDNAD2_" code ".json"            
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion752_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion2002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion3002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion4002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion5002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion6002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion7002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion8002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisRegion9002_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisTrainPomaliky2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisTrainRychliky2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisNAD2_" code ".json"
            sprintf "%s%s%s%s" partialPathJson @"kodisBoat_" code ".json"
        ] 

    let internal pathToJsonList3 = codes |> List.collect pathToJsonListPartial //(fun code -> pathToJsonListPartial code) 