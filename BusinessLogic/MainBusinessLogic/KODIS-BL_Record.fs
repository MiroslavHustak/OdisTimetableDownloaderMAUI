namespace BusinessLogic

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

open Types
open Types.Types
open Types.ErrorTypes

open Settings.Messages
open Settings.SettingsKODIS
open Settings.SettingsGeneral

open Helpers
open Helpers.Builders

open JsonData.SortJsonData
open IO_Operations.IO_Operations
open Filtering.FilterTimetableLinks

module KODIS_BL_Record =            
   
    let private myDelete () = 

        let pathToDir = kodisPathTemp                   
                        
        match deleteAllODISDirectories pathToDir with
        | Ok _    -> ()
        | Error _ -> () //potlacime to, nestoji to za to s tim neco robit

    //************************Main code***********************************************************

    //For testing purposes
    let internal downloadAndSaveJsonTest () =   
        
        let pathKodisWeb9 = @"https://kodis-backend-staging-85d01eccf627.herokuapp.com/api/linky-search?"           
        let jsonLink9 = sprintf "%s%s" pathKodisWeb9 "groups%5B0%5D=MHD%20Brunt%C3%A1l&groups%5B1%5D=MHD%20%C4%8Cesk%C3%BD%20T%C4%9B%C5%A1%C3%ADn&groups%5B2%5D=MHD%20Fr%C3%BDdek-M%C3%ADstek&groups%5B3%5D=MHD%20Hav%C3%AD%C5%99ov&groups%5B4%5D=MHD%20Karvin%C3%A1&groups%5B5%5D=MHD%20Krnov&groups%5B6%5D=MHD%20Nov%C3%BD%20Ji%C4%8D%C3%ADn&groups%5B7%5D=MHD%20Opava&groups%5B8%5D=MHD%20Orlov%C3%A1&groups%5B9%5D=MHD%20Ostrava&groups%5B10%5D=MHD%20Stud%C3%A9nka&groups%5B11%5D=MHD%20T%C5%99inec&groups%5B12%5D=NAD%20MHD&start=0&limit=12"       
                
        async
            {    
                use! response = get >> Request.sendAsync <| jsonLink9 
                do! response.SaveFileAsync >> Async.AwaitTask <| @"e:\FabulousMAUI\test.json"    

                return "Test"
            }    
   
    let internal downloadAndSaveJson jsonLinkList pathToJsonList (token : CancellationToken) reportProgress = //FsHttp
               
        let l = jsonLinkList |> List.length

        let counterAndProgressBar =
            MailboxProcessor.Start <|
                fun inbox 
                    ->
                    let rec loop n = 
                        async { match! inbox.Receive() with Inc i -> reportProgress (float n, float l); return! loop (n + i) }
                    loop 0
      
        try 
            (jsonLinkList, pathToJsonList)
            ||> List.Parallel.map2
                (fun (uri: string) path
                    ->           
                    async
                        {    
                            //each async task or thread only stops when it reaches a point where it checks the token status (IsCancellationRequested), which can lead to a slight delay.     
                            match token.IsCancellationRequested with
                            | false -> 
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
                                        counterAndProgressBar.Post(Inc 1)                                                   
                                        return! response.SaveFileAsync >> Async.AwaitTask <| path                                
                                    | _ ->  
                                        return () //TODO zaznamenat do logfile a nechat chybu tise projit      
                            | true  ->                                
                                    return! async { return token.ThrowIfCancellationRequested() } 
                        }  
                    |> Async.RunSynchronously
                )
            |> ignore
            |> Ok                     
        with
        | :? OperationCanceledException 
            when 
                token.IsCancellationRequested 
                    ->
                    myDelete ()
                    Error CancelJsonProcess      

        | :? HttpRequestException as ex 
            ->
            match ex.InnerException with
            | :? System.Security.Authentication.AuthenticationException 
                ->
                myDelete ()
                Error <| NetConnJsonError netConnError
            | _ 
                ->
                myDelete ()
                Error <| NetConnJsonError unKnownError    
    
    let internal operationOnDataFromJson (token : CancellationToken) variant dir =   

        try               
            match token.IsCancellationRequested with
            | false -> digThroughJsonStructure >> filterTimetableLinks variant dir <| token 
            | true  -> failwith String.Empty
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
                                                                config_timeoutInSeconds 300  //for educational purposes
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
                                                        return ()      //nechame chybu tise projit //TODO chybu zaznamenat do logfile 
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
                                    -> 
                                    Error CancelPdfProcess      

                        | :? HttpRequestException as ex 
                            ->
                            match ex.InnerException with
                            | :? System.Security.Authentication.AuthenticationException 
                                ->
                                Error <| NetConnPdfError netConnError
                            | _ 
                                ->
                                Error <| NetConnPdfError unKnownError
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