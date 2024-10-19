namespace Settings

open System

//***************************

open Types.Types
open Types.ErrorTypes

module SettingsGeneral =  

    let internal logFileName = "logs/app.log"

    let internal ODISDefault =  
        {          
            OdisDir1 = "JR_ODIS_aktualni_vcetne_vyluk"
            OdisDir2 = "JR_ODIS_pouze_budouci_platnost"
            //odisDir3 = "JR_ODIS_pouze_aktualni_vyluky"
            OdisDir4 = "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk" 
            OdisDir5 = "JR_ODIS_pouze_linky_dopravce_DPO" 
            OdisDir6 = "JR_ODIS_pouze_linky_dopravce_MDPO" 
        }   

    let internal listODISDefault4 = 
        [ 
            ODISDefault.OdisDir1
            ODISDefault.OdisDir2
            //ODISDefault.odisDir3
            ODISDefault.OdisDir4 
        ]  

    let internal connErrorCodeDefault =                        
        {
            BadRequest            = "400 Bad Request"
            InternalServerError   = "500 Internal Server Error"
            NotImplemented        = "501 Not Implemented"
            ServiceUnavailable    = "503 Service Unavailable"           
            NotFound              = String.Empty  
            CofeeMakerUnavailable = "418 I'm a teapot. Look for a coffee maker elsewhere."
        }   

    let internal listConnErrorCodeDefault =                        
        [
            connErrorCodeDefault.BadRequest
            connErrorCodeDefault.InternalServerError 
            connErrorCodeDefault.NotImplemented       
            connErrorCodeDefault.ServiceUnavailable              
            connErrorCodeDefault.NotFound              
            connErrorCodeDefault.CofeeMakerUnavailable 
        ]   

    //let internal partialPathJsonTemp = @"e:\FabulousMAUI\OdisTimetableDownloaderMAUI\KODISJson2\" //@"KODISJson2/" //v binu //tohle je pro stahovane json, ne pro type provider
    //let internal partialPathJsonTemp = @"/storage/emulated/0/Android/data/com.companyname.OdisTimetableDownloaderMAUI/"  //Android 7.1
    let internal partialPathJsonTemp = @"/storage/emulated/0/FabulousTimetables/JsonData/com.companyname.OdisTimetableDownloaderMAUI/"  //Android 12
    
    //let internal kodisPathTemp = @"c:\Users\User\Data\"
    let internal kodisPathTemp = @"/storage/emulated/0/FabulousTimetables/"

    //let internal dpoPathTemp = @"c:\Users\User\Data\"
    let internal dpoPathTemp = @"/storage/emulated/0/FabulousTimetables/"

    //let internal mdpoPathTemp = @"c:\Users\User\Data\"
    let internal mdpoPathTemp = @"/storage/emulated/0/FabulousTimetables/"

    //let internal connectionCheckSwitch = "Windows Machine"
    //let internal connectionCheckSwitch = "Android 7.1" 
    let internal connectionCheckSwitch = "Android 12" 