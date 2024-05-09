namespace Settings

open System

open Types.DirNames
open Types.ErrorTypes

module SettingsGeneral =  

    let internal logFileName = "logs/app.log"

    let internal ODISDefault =  
        {          
            odisDir1 = "JR_ODIS_aktualni_vcetne_vyluk"
            odisDir2 = "JR_ODIS_pouze_budouci_platnost"
            //odisDir3 = "JR_ODIS_pouze_aktualni_vyluky"
            odisDir4 = "JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk" 
            odisDir5 = "JR_ODIS_pouze_linky_dopravce_DPO" 
            odisDir6 = "JR_ODIS_pouze_linky_dopravce_MDPO" 
        }   

    let internal listODISDefault4 = 
        [ 
            ODISDefault.odisDir1
            ODISDefault.odisDir2
            //ODISDefault.odisDir3
            ODISDefault.odisDir4 
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