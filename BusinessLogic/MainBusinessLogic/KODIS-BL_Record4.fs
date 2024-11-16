namespace BusinessLogic4

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading
open System.Net.NetworkInformation

//************************************************************

open FsHttp
open FsToolkit.ErrorHandling

//************************************************************

open Types.Types
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open Settings.Messages
open Settings.SettingsGeneral

open Api.CallApi
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    
    let private myDelete () = 

        let pathToDir = kodisPathTemp                   
                        
        match deleteAllODISDirectories pathToDir with
        | Ok _    -> ()
        | Error _ -> () //potlacime to, nestoji to za to s tim neco robit

    //************************Main code***********************************************************
        
    let internal operationOnDataFromJson token variant dir =   

        try          
            getFromRestApi >> filterTimetableLinks variant dir <| ()
        with
        | ex 
            ->
            string ex.Message |> ignore //TODO logfile                 
            Error DataFilteringError 
                    
    let internal downloadAndSave token = 

        let downloadAndSaveTimetables (token : CancellationToken) =
        
            reader
                {
                    let! context = fun env -> env 
        
                    let l = context.list |> List.length
        
                    let counterAndProgressBar =
                        MailboxProcessor.Start <|
                            fun inbox 
                                ->
                                let rec loop n = 
                                    async { match! inbox.Receive() with Inc i -> context.reportProgress (float n, float l); return! loop (n + i) }
                                loop 0
                                                            
                    return    
                        try
                            context.list
                            |> List.unzip             
                            ||> context.listMappingFunction
                                (fun uri (pathToFile: string) 
                                    -> 
                                    async
                                        {            
                                            match token.IsCancellationRequested with
                                            | false ->  
                                                    counterAndProgressBar.Post(Inc 1)
        
                                                    let get uri =
                                                        http 
                                                            {
                                                                config_timeoutInSeconds 300  
                                                                GET(uri) 
                                                            }    
        
                                                    use! response = get >> Request.sendAsync <| uri  
        
                                                    match response.statusCode with
                                                    | HttpStatusCode.OK 
                                                        -> 
                                                        let pathToFileExist =  
                                                            pyramidOfDoom
                                                                {
                                                                    let filepath = Path.GetFullPath(pathToFile) |> Option.ofNullEmpty 
                                                                    let! filepath = filepath, None
        
                                                                    let fInfodat: FileInfo = FileInfo filepath
                                                                    let! _ = not fInfodat.Exists |> Option.ofBool, None   
                                                                                     
                                                                    return Some ()
                                                                } 
                                                                                 
                                                        match pathToFileExist with
                                                        | Some _ -> return! response.SaveFileAsync >> Async.AwaitTask <| pathToFile       
                                                        | None   -> return ()  //nechame chybu tise projit  //TODO chybu zaznamenat do logfile  
                                                                                                                                                                      
                                                    | _                
                                                        -> 
                                                        return ()      //chyba se projevi dale
                                            | true  ->
                                                    return! async { return token.ThrowIfCancellationRequested() }           
                                        } 
                                    |> Async.RunSynchronously                                                        
                                )  
                            |> ignore
                            
                            Ok String.Empty

                        with
                        | :? OperationCanceledException 
                            when 
                                token.IsCancellationRequested 
                                    -> Error CancelPdfProcess      

                        | :? HttpRequestException as ex 
                            ->
                            match ex.InnerException with
                            | :? System.Security.Authentication.AuthenticationException 
                                -> Error <| NetConnPdfError netConnError
                            | _ 
                                -> Error <| NetConnPdfError unKnownError
                } 
        
        reader
            {    
                let! context = fun env -> env
                
                return
                    match context.dir |> Directory.Exists with 
                    | false ->
                            Error FileDownloadError                                             
                    | true  ->
                            try
                                match context.list with
                                | [] -> 
                                     Ok String.Empty 
                                | _  ->
                                     match downloadAndSaveTimetables token context with
                                     | Ok value  -> 
                                                 Ok value
                                     | Error err ->
                                                 let pathToDir = kodisPathTemp                   
                                                                                     
                                                 match deleteAllODISDirectories pathToDir with
                                                 | Ok _    -> Ok String.Empty
                                                 | Error _ -> Error err //preneseme jen vyse uvedene chyby, chyba z delete nestoji za to resit
                            with
                            | ex 
                                ->
                                string ex.Message |> ignore //TODO logfile   
                                myDelete () 
                                Error FileDownloadError 
            }               