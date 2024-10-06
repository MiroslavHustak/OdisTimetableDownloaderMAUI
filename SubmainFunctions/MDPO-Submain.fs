namespace SubmainFunctions

open Helpers.Builders

module MDPO_Submain =

    open System
    open System.IO
    open System.Net

    //**********************************

    open FsHttp
    open FSharp.Data
    open FsToolkit.ErrorHandling

    //**********************************

    open Helpers

    open Types.ErrorTypes

    open Settings.SettingsMDPO
    open Settings.SettingsGeneral    

    //FsHttp

    //************************Submain functions************************************************************************

    let internal filterTimetables () pathToDir = 
         
        let urlList = //aby to bylo jednotne s DPO
            [
                pathMdpoWebTimetables
            ]

        urlList    
        |> Seq.collect 
            (fun url -> 
                      let document = FSharp.Data.HtmlDocument.Load url //neni nullable, nesu exn
                      //HtmlDocument -> web scraping -> extracting data from HTML pages
                                                                                    
                      document.Descendants "a"                  
                      |> Seq.choose 
                          (fun htmlNode    ->
                                            htmlNode.TryGetAttribute "href" //inner text zatim nepotrebuji, cisla linek mam resena jinak  
                                            |> Option.bind
                                                (fun attr -> 
                                                           let nodes = htmlNode.InnerText() |> Option.ofNullEmpty
                                                           let attr = attr.Value() |> Option.ofNullEmpty
                                                        
                                                           match nodes, attr with
                                                           | Some nodes, Some attr -> Some (nodes, attr)
                                                           | _                     -> None 
                                                )                                                     
                          )      
                      |> Seq.filter 
                          (fun (_ , item2) -> 
                                            item2.Contains @"/qr/" && item2.Contains ".pdf"
                          )
                      |> Seq.map 
                          (fun (_ , item2) ->                                                                 
                                            let linkToPdf = sprintf "%s%s" pathMdpoWeb item2  //https://www.mdpo.cz // /qr/201.pdf
                                            let lineName (item2 : string) = item2.Replace(@"/qr/", String.Empty)  
                                            let pathToFile lineName = sprintf "%s/%s" pathToDir lineName
                                            linkToPdf, (pathToFile << lineName) item2
                          )                          
                      |> Seq.distinct                 
            )  
        |> Seq.fold (fun acc (key, value) -> Map.add key value acc) Map.empty //vyzkousime si tvorbu Map

    //FsHttp
    let internal downloadAndSaveTimetables reportProgress (pathToDir : string) (filterTimetables : Map<string, string>) =  

        let downloadFileTaskAsync (uri : string) (pathToFile : string) : Async<Result<unit, string>> =  
       
            async
                {                      
                    try    
                        let response = 
                            
                            pyramidOfDoom
                                {
                                    let!_ = not <| File.Exists pathToFile |> Option.ofBool, Error String.Empty
                                    let! response = get >> Request.sendAsync <| uri |> Option.ofNull, Error String.Empty //Option.ofNull tady neni treba, ale aby to bylo jednotne....

                                    return Ok response        
                                }
                        
                        match response with
                        | Ok response ->      
                                       use! response = response
                        
                                       match response.statusCode with        //TODO logfile
                                       | HttpStatusCode.OK                  ->                                                                   
                                                                             do! response.SaveFileAsync >> Async.AwaitTask <| pathToFile
                                                                             return Ok () 
                                       | HttpStatusCode.BadRequest          ->                                                                       
                                                                             return Error connErrorCodeDefault.BadRequest
                                       | HttpStatusCode.InternalServerError -> 
                                                                             return Error connErrorCodeDefault.InternalServerError
                                       | HttpStatusCode.NotImplemented      ->
                                                                             return Error connErrorCodeDefault.NotImplemented
                                       | HttpStatusCode.ServiceUnavailable  ->
                                                                             return Error connErrorCodeDefault.ServiceUnavailable
                                       | HttpStatusCode.NotFound            ->
                                                                             return Error uri  
                                       | _                                  ->
                                                                             return Error connErrorCodeDefault.CofeeMakerUnavailable                                 
                        | Error err   -> 
                                       err |> ignore  //TODO logfile
                                       return Error String.Empty 
                           
                    with                                                         
                    | ex ->
                          string ex.Message |> ignore  //TODO logfile
                          return Error String.Empty   
                } 
    
        let downloadTimetables reportProgress : Result<unit, string> = 
        
            let l = filterTimetables |> Map.count
        
            filterTimetables
            |> Map.toList 
            |> List.mapi  //bohuzel s Map nelze mapi nebo iteri
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
                | Ok _     ->
                            Ok ()   
                | Error ex -> 
                            string ex.Message |> ignore //TODO logfile
                            Error "Došlo k chybě, všechny JŘ MDPO nebyly úspěšně staženy."    

        downloadTimetables reportProgress 