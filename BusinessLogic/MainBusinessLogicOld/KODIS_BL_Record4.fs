namespace BusinessLogic

open System
open System.IO
open System.Net
open System.Threading

//*******************

open FsHttp
open FSharp.Control
open FsToolkit.ErrorHandling

//*******************

open Types
open Types.Types
open Types.ErrorTypes

//*******************

open Api.Logging
open Api.FutureLinks

open Helpers.Builders
open Helpers.DirFileHelper
open Helpers.ExceptionHelpers

open Settings.SettingsGeneral
open Filtering.FilterTimetableLinks
open Types.Haskell_IO_Monad_Simulation

module KODIS_BL_Record4 =    
        
    // 30-10-2024 Docasne reseni do doby, nez v KODISu odstrani naprosty chaos v json souborech a v retezcich jednotlivych odkazu  
    // 16-12-2024 Nic neni trvalejsiho, nez neco docasneho ...
    // 31-12-2025 ... kdo by to byl rekl, ze se nic nezmeni   
    
    let internal operationOnDataFromJson (token : CancellationToken) variant dir = 
    
        IO (fun () 
                ->                                
                let result1 () : Async<Result<(string * string) list, ParsingAndDownloadingErrors>> = 
                    async
                        {
                            token.ThrowIfCancellationRequested() 

                            match! getFutureLinksFromRestApi >> runIO <| urlApi with
                            | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                            | Error err -> return Error <| PdfDownloadError2 err
                        }
    
                let result2 () : Async<Result<(string * string) list, ParsingAndDownloadingErrors>> = 
                    async
                        {
                            token.ThrowIfCancellationRequested() 
                            
                            match variant with
                            | FutureValidity 
                                ->
                                match! getFutureLinksFromRestApi >> runIO <| urlJson with
                                | Ok value  -> return runIO <| filterTimetableLinks variant dir (Ok value)
                                | Error err -> return Error <| PdfDownloadError2 err
                            | _              
                                -> 
                                return Ok []
                        }
       
                async 
                    {
                        let! results = 
                            [| 
                                result1 ()
                                result2 ()
                            |]
                            |> Async.Parallel
                            |> Async.Catch
    
                        match results with
                        | Choice1Of2 resultsArray 
                            ->
                            return
                                match List.ofArray resultsArray with
                                | [ Ok list1; Ok list2 ]
                                    -> 
                                    Ok (List.distinct (list1 @ list2))

                                | [ Error err; _ ]    
                                    -> 
                                    runIO (postToLog <| err <| "#013")                                     
                                    Error err

                                | [ _; Error err ] 
                                    ->                                    
                                    runIO (postToLog <| err <| "#014")   
                                    Error err

                                | _                   
                                    ->
                                    runIO (postToLog <| JsonDataFilteringError <| "#015")                                    
                                    Error <| JsonParsingError2 JsonDataFilteringError

                        | Choice2Of2 ex
                            -> 
                            match isCancellationGeneric LetItBeParsing StopJsonParsing JsonParsingTimeoutError JsonDataFilteringError token ex with
                            | err 
                                when err = StopJsonParsing
                                ->
                                //runIO (postToLog <| string ex.Message <| "#123456C")
                                return Error <| JsonParsingError2 StopJsonParsing                            
                            | err 
                                ->
                                runIO (postToLog <| string ex.Message <| "#016")                      
                                return Error <| JsonParsingError2 JsonDataFilteringError 
                              
                    }
                |> fun a -> Async.RunSynchronously(a, cancellationToken = token)
        )    

    let internal downloadAndSave token =  

        IO (fun () 
                ->         
                let downloadAndSaveTimetables (token : CancellationToken) =  
         
                    reader
                        {                           
                            let! context = fun env -> env 
         
                            let l = context.list |> List.length
                                 
                            let counterAndProgressBar =
                                MailboxProcessor<MsgIncrement>
                                    .StartImmediate
                                        <|
                                        fun inbox 
                                            ->                                                  
                                            let rec loop n = 
                                                async
                                                    {
                                                        try
                                                            let! Inc i = inbox.Receive()
                                                            context.reportProgress (float n, float l)
                                                            return! loop (n + i)
                                                        with
                                                        | ex -> runIO (postToLog <| ex.Message <| "#903-MP")
                                                    }
                                            loop 0
                                                 
                            //mel jsem 2x stejnou linku s jinym jsGeneratedString, takze uri bylo unikatni, ale cesta k souboru 2x stejna
                            let removeDuplicatePathPairs uri pathToFile =
                                (uri, pathToFile)
                                ||> List.zip 
                                |> List.distinctBy snd
                         
                            let uri, pathToFile =
                                context.list
                                |> List.distinct
                                |> List.unzip
                                |> fun (uri, pathToFile) -> removeDuplicatePathPairs uri pathToFile
                                |> List.unzip        
                            
                            return 
                                try
                                    (token, uri, pathToFile)
                                    |||> List.Parallel.map2_IO_AW_Token_Async //context.listMappingFunction                            
                                        (fun uri (pathToFile : string) 
                                            -> 
                                            async
                                                {
                                                    try 
                                                        counterAndProgressBar.Post <| Inc 1
                                                
                                                        token.ThrowIfCancellationRequested () //pouzit v parallel loops jen u async verze
    
                                                        let pathToFileExistFirstCheck = // bez tohoto file checking mobilni app nefunguje, TOCTOU race zatim nebyl problem        
                                                            runIO <| checkFileCondition pathToFile (fun fileInfo -> not fileInfo.Exists) //tady potrebuji vedet, ze tam nahodou uz nebo jeste neni (melo by se to spravne vse mazat)                        
                                                            in
                                                            match pathToFileExistFirstCheck with  
                                                            | Some _
                                                                -> 
                                                                let existingFileLength =                               
                                                                    runIO <| checkFileCondition pathToFile (fun fileInfo -> fileInfo.Exists)
                                                                    |> Option.map (fun _ -> (FileInfo pathToFile).Length)
                                                                    |> Option.defaultValue 0L
                                                 
                                                                let get uri = 
    
                                                                    let headerContent1 = "Range" 
                                                                    let headerContent2 = sprintf "bytes=%d-" existingFileLength 
                       
                                                                    match existingFileLength > 0L with
                                                                    | true  -> 
                                                                            http
                                                                                {
                                                                                    GET uri
                                                                                    config_timeoutInSeconds timeOutInSeconds2 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                                    config_cancellationToken token 
                                                                                    header "User-Agent" "FsHttp/Android7.1"
                                                                                    header headerContent1 headerContent2
                                                                                }
                                                                    | false ->
                                                                            http
                                                                                {
                                                                                    GET uri
                                                                                    config_timeoutInSeconds timeOutInSeconds2 //pouzije se kratsi cas, pokud zaroven token a timeout
                                                                                    config_cancellationToken token 
                                                                                    header "User-Agent" "FsHttp/Android7.1"
                                                                                }    

                                                                use! response = get >> Request.sendAsync <| uri

                                                                let statusCode = response.statusCode
                                                 
                                                                match statusCode with
                                                                | HttpStatusCode.PartialContent | HttpStatusCode.OK // 206 // 200
                                                                    ->  
                                                                    do! response.SaveFileAsync pathToFile |> Async.AwaitTask
                                                                    return Ok result
                                                                 
                                                                | HttpStatusCode.Forbidden 
                                                                    ->
                                                                    runIO <| postToLog () (sprintf "%s %s Error%s" <| uri <| "Forbidden 403" <| "#2211-K4") 
                                                                    return Error <| PdfDownloadError2 FileDownloadError
    
                                                                | status
                                                                    ->
                                                                    runIO (postToLog <| (string status) <| "#2212-K4")
                                                                    return Error <| PdfDownloadError2 FileDownloadError                                                               
                                                           
                                                            | None 
                                                                ->
                                                                runIO (postToLog <| "pathToFileExistFirstCheck failed" <| "#2230-K4")
                                                                return Error <| PdfDownloadError2 FileDownloadError      
                                                    with
                                                    | ex                             
                                                        -> 
                                                        match isCancellationGeneric LetItBeKodis4 StopDownloading TimeoutError FileDownloadError token ex with
                                                        | err 
                                                            when err = StopDownloading
                                                            ->
                                                            //runIO (postToLog <| string ex.Message <| "#123456C")
                                                            return Error <| PdfDownloadError2 StopDownloading
                                                        | err 
                                                            ->
                                                            runIO (postToLog <| string ex.Message <| "#024-K4")
                                                            return Error <| PdfDownloadError2 err   
                                                }                                                
                                        )  

                                    |> fun a -> Async.RunSynchronously(a, cancellationToken = token)

                                with
                                | ex                             
                                    -> 
                                    match isCancellationGeneric LetItBeKodis4 StopDownloading TimeoutError FileDownloadError token ex with
                                    | err 
                                        when err = StopDownloading
                                        ->
                                        //runIO (postToLog <| string ex.Message <| "#123456D")
                                        [ Error <| PdfDownloadError2 StopDownloading ]
                                    | err 
                                        ->
                                        runIO (postToLog <| string ex.Message <| "#024-6-K4")
                                        [ Error <| PdfDownloadError2 err ]    

                            |> List.tryPick (Result.either (fun _ -> None) (Error >> Some))
                            |> Option.defaultValue (Ok ())                               
                         } 
     
                reader
                    {    
                        let! context = fun env -> env
             
                        return
                           pyramidOfDamnation //TOCTOU race condition by tady nemel byt problem       
                               {
                                   let! _ = context.dir |> Directory.Exists, Error (PdfDownloadError2 NoFolderError)
                                   let! _ = context.list <> List.Empty, Ok String.Empty
                               
                                   return
                                       downloadAndSaveTimetables token context
                                       |> Result.map (fun _ -> String.Empty)       
                               }           
                    }       
        )