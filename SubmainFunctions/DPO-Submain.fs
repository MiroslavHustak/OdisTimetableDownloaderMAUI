namespace SubmainFunctions

open System
open System.IO
open System.Net
open System.Net.Http

open FSharp.Data
open FsToolkit.ErrorHandling

open Helpers
open Helpers.Builders

open Types.ErrorTypes  

open Settings.SettingsDPO
open Settings.SettingsGeneral

//HttpClient
module DPO_Submain =

    //************************Submain functions************************************************************************
     
    let internal filterTimetables () pathToDir = 

        let getLastThreeCharacters input =
            match String.length input <= 3 with
            | true  -> input 
            | false -> input.Substring(input.Length - 3)

        let removeLastFourCharacters input =
            match String.length input <= 4 with
            | true  -> String.Empty
            | false -> input.[..(input.Length - 5)]                    
    
        let urlList = 
            [
                pathDpoWebTimetablesBus      
                pathDpoWebTimetablesTrBus
                pathDpoWebTimetablesTram
            ]
    
        urlList
        |> List.collect 
            (fun url -> 
                      let document = FSharp.Data.HtmlDocument.Load(url) //neni nullable, nesu exn
                  
                      document.Descendants "a"
                      |> Seq.choose 
                          (fun htmlNode    ->
                                            htmlNode.TryGetAttribute("href") //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                                            |> Option.map (fun a -> string <| htmlNode.InnerText(), string <| a.Value()) //priste to uz tak nerobit, u string zrob Option.ofNull, atd.                                         
                          )  
                      |> Seq.filter
                          (fun (_ , item2) ->
                                            item2.Contains @"/jr/" && item2.Contains ".pdf" && not (item2.Contains "AE-eng.pdf") 
                          )
                      |> Seq.map 
                          (fun (_ , item2) ->  
                                            let linkToPdf = sprintf"%s%s" pathDpoWeb item2  //https://www.dpo.cz // /jr/2023-04-01/024.pdf 

                                            let adaptedLineName =
                                                let s (item2: string) = item2.Replace(@"/jr/", String.Empty).Replace(@"/", "?").Replace(".pdf", String.Empty) 
                                                let rec x s =                                                                            
                                                    match (getLastThreeCharacters s).Contains("?") with
                                                    | true  -> x (sprintf "%s%s" s "_")                                                                             
                                                    | false -> s
                                                (x << s) item2
                                        
                                            let lineName = 
                                                let s adaptedLineName = sprintf"%s_%s" (getLastThreeCharacters adaptedLineName) adaptedLineName  
                                                let s1 s = removeLastFourCharacters s 
                                                sprintf"%s%s" <| (s >> s1) adaptedLineName <| ".pdf"
                                            
                                            let pathToFile = 
                                                let item2 = item2.Replace("?", String.Empty)
                                                let lineName = 
                                                    match item2.Contains("NAD") with
                                                    | true when item2.Contains("NAD1") -> "NAD1.pdf"
                                                    | true when item2.Contains("NAD2") -> "NAD2.pdf"
                                                    | true when item2.Contains("NAD3") -> "NAD3.pdf"
                                                    | true when item2.Contains("NAD4") -> "NAD4.pdf"
                                                    | true when item2.Contains("NAD5") -> "NAD5.pdf"
                                                    | true when item2.Contains("NAD6") -> "NAD6.pdf"
                                                    | true when item2.Contains("NAD7") -> "NAD7.pdf"
                                                    | true when item2.Contains("NAD8") -> "NAD8.pdf"
                                                    | true when item2.Contains("NAD9") -> "NAD9.pdf"
                                                    | _                                -> lineName
                                            
                                                sprintf "%s/%s" pathToDir lineName
                                            linkToPdf, pathToFile
                          )
                      |> Seq.toList
                      |> List.distinct
            ) 

    let internal downloadAndSaveTimetables reportProgress (filterTimetables: (string * string) list) =  

        let downloadFileTaskAsync (uri: string) (pathToFile : string) : Async<Result<unit, string>> =  
       
            async
                {                      
                    try    
                        let client = 
                            
                            pyramidOfDoom
                                {
                                    let!_ = not <| File.Exists(pathToFile) |> Option.ofBool, Error String.Empty
                                    let! client = new HttpClient() |> Option.ofNull, Error String.Empty

                                    return Ok client        
                                }
                        
                        match client with
                        | Ok client ->      
                                     use! response = client.GetAsync(uri) |> Async.AwaitTask
                        
                                     match response.IsSuccessStatusCode with //true if StatusCode was in the range 200-299; otherwise, false.
                                     | true  -> 
                                              let! stream = response.Content.ReadAsStreamAsync() |> Async.AwaitTask  
                                              let pathToFile = pathToFile.Replace("?", String.Empty)
                                              use fileStream = new FileStream(pathToFile, FileMode.CreateNew) 
                                              do! stream.CopyToAsync(fileStream) |> Async.AwaitTask  
                                              
                                              return Ok ()
                                     | false -> 
                                              let errorType = 
                                                  match response.StatusCode with
                                                  | HttpStatusCode.BadRequest          -> Error connErrorCodeDefault.BadRequest
                                                  | HttpStatusCode.InternalServerError -> Error connErrorCodeDefault.InternalServerError
                                                  | HttpStatusCode.NotImplemented      -> Error connErrorCodeDefault.NotImplemented
                                                  | HttpStatusCode.ServiceUnavailable  -> Error connErrorCodeDefault.ServiceUnavailable
                                                  | HttpStatusCode.NotFound            -> Error uri  
                                                  | _                                  -> Error connErrorCodeDefault.CofeeMakerUnavailable   
                                         
                                              return errorType   
                                
                        | Error _   -> 
                                     return Error String.Empty 
                           
                    with                                                         
                    | _ -> return Error String.Empty   
                } 
    
        let downloadTimetables reportProgress : Result<unit, string> = 
        
            let l = filterTimetables |> List.length
        
            filterTimetables 
            |> List.mapi
                (fun i (link, pathToFile)
                    ->                                                 
                     async                                                
                         {   
                             reportProgress (float i + 1.0, float l)  
                             return! downloadFileTaskAsync link pathToFile                                                                                                                               
                         } 
                     |> Async.Catch
                     |> Async.RunSynchronously
                     |> Result.ofChoice  
                ) 
            |> Result.sequence  
            |> function
                | Ok _    ->
                           //client.Dispose() 
                           Ok ()   
                | Error _ ->
                           //client.Dispose() 
                           Error "Došlo k chybě, všechny JŘ DPO nebyly úspěšně staženy."

        downloadTimetables reportProgress    