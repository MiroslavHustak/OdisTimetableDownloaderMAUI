namespace SubmainFunctions

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//******************************************

open FSharp.Data
open FsToolkit.ErrorHandling

//******************************************

open Helpers
open Helpers.Builders

open Types.ErrorTypes  

open Settings.Messages
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
                      let document = FSharp.Data.HtmlDocument.Load url //neni nullable, nesu exn
                  
                      document.Descendants "a"
                      |> Seq.choose 
                          (fun htmlNode    ->
                                            htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                                            |> Option.bind
                                                (fun attr -> 
                                                           pyramidOfDoom
                                                               {
                                                                   let! nodes = htmlNode.InnerText() |> Option.ofNullEmpty, None
                                                                   let! attr = attr.Value() |> Option.ofNullEmpty, None
                                                               
                                                                   return Some (nodes, attr)
                                                               }                                                          
                                                )            
                          )  
                      |> Seq.filter
                          (fun (_ , item2) ->
                                            item2.Contains @"/jr/" && item2.Contains ".pdf" && not (item2.Contains "AE-en.pdf") 
                          )
                      |> Seq.map 
                          (fun (_ , item2) ->  
                                            let linkToPdf = sprintf"%s%s" pathDpoWeb item2  //https://www.dpo.cz // /jr/2023-04-01/024.pdf 

                                            let adaptedLineName =
                                                let s (item2 : string) = item2.Replace(@"/jr/", String.Empty).Replace(@"/", "?").Replace(".pdf", String.Empty) 
                                                let rec x s =                                                                            
                                                    match (getLastThreeCharacters s).Contains("?") with
                                                    | true  -> x (sprintf "%s%s" s "_")                                                                             
                                                    | false -> s
                                                (x << s) item2
                                        
                                            let lineName = 
                                                let s adaptedLineName = sprintf "%s_%s" (getLastThreeCharacters adaptedLineName) adaptedLineName  
                                                let s1 s = removeLastFourCharacters s 
                                                sprintf"%s%s" <| (s >> s1) adaptedLineName <| ".pdf"
                                            
                                            let pathToFile = 
                                                let item2 = item2.Replace("?", String.Empty)                                            
                                                sprintf "%s/%s" pathToDir lineName

                                            linkToPdf, pathToFile
                          )
                      |> Seq.toList
                      |> List.distinct
            ) 

    let internal downloadAndSaveTimetables reportProgress (token : CancellationToken) (filterTimetables : (string * string) list) =  

        let downloadFileTaskAsync (uri : string) (pathToFile : string) : Async<Result<unit, string>> =  
       
            async
                {                      
                    try    
                        let client = 
                            
                            pyramidOfDoom
                                {
                                    let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty
                                    let! client = new HttpClient() |> Option.ofNull, Error String.Empty

                                    return Ok client        
                                }
                        
                        match client with  
                        | Ok client ->      
                                     use! response = client.GetAsync uri |> Async.AwaitTask
                        
                                     match response.IsSuccessStatusCode with //true if StatusCode was in the range 200-299; otherwise, false.
                                     | true  -> 
                                              let! stream = response.Content.ReadAsStreamAsync() |> Async.AwaitTask  
                                              let pathToFile = pathToFile.Replace("?", String.Empty)
                                              use fileStream = new FileStream(pathToFile, FileMode.CreateNew) 
                                              do! stream.CopyToAsync fileStream |> Async.AwaitTask  
                                              
                                              return Ok ()
                                     | false -> 
                                              let errorType = 
                                                  match response.StatusCode with        //TODO logfile
                                                  | HttpStatusCode.BadRequest          -> Error connErrorCodeDefault.BadRequest
                                                  | HttpStatusCode.InternalServerError -> Error connErrorCodeDefault.InternalServerError
                                                  | HttpStatusCode.NotImplemented      -> Error connErrorCodeDefault.NotImplemented
                                                  | HttpStatusCode.ServiceUnavailable  -> Error connErrorCodeDefault.ServiceUnavailable
                                                  | HttpStatusCode.NotFound            -> Error uri  
                                                  | _                                  -> Error connErrorCodeDefault.CofeeMakerUnavailable   
                                         
                                              return errorType   
                                
                        | Error err -> 
                                     err |> ignore //TODO logfile  
                                     return Error String.Empty 
                           
                    with                                                         
                    | ex ->
                          string ex.Message |> ignore //TODO logfile
                          return Error String.Empty   
                } 
    
        let downloadTimetables reportProgress (token : CancellationToken) : Result<unit, string> = 
        
            let l = filterTimetables |> List.length
        
            filterTimetables 
            |> List.mapi
                (fun i (link, pathToFile)
                    ->  
                     async                                                
                         {   
                             match token.IsCancellationRequested with
                             | false -> 
                                      reportProgress (float i + 1.0, float l)  
                                      return! downloadFileTaskAsync link pathToFile    
                             | true  -> 
                                      return! async { return failwith "failwith to be used because the Choice type requires exn" }    
                        } 
                    |> Async.Catch
                    |> Async.RunSynchronously
                    |> Result.ofChoice                             
                ) 
            |> Result.sequence  
            |> function
                | Ok _     ->
                            Ok ()   
                | Error ex -> 
                            string ex.Message |> ignore //TODO logfile
                            //quli rozliseni chyb, Result.sequence da pouze jednu
                            match (string ex.Message).Contains("failwith to be used because the Choice type requires exn") with
                            | true  -> Error dpoCancelMsg 
                            | false -> Error dpoMsg2

        downloadTimetables reportProgress token    