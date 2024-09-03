namespace DataTable

open System
open System.Data

//******************************

open Types
open Types.ErrorTypes

open Helpers
open Helpers.Builders

open Settings
open Settings.SettingsKODIS

open DataModelling.Dto
open DataModelling.DataModel

open TransformationLayers.TransformationLayerGet


//nutno byti v tryWith
module InsertSelectSort =      
   
    let private insertIntoDataTable (dt : DataTable) (dataToBeInserted : DtDtoSend list) =
            
        dataToBeInserted 
        |> List.iter 
            (fun item ->
                       (*
                       let (startDate, endDate) =   

                           pyramidOfDoom
                               {
                                   let! startDate = item.startDate, (DateTime.MinValue, DateTime.MinValue)                                                      
                                   let! endDate = item.endDate, (DateTime.MinValue, DateTime.MinValue)                             
                              
                                   return (startDate, endDate)
                               }
                       *)
                            
                       let newRow = dt.NewRow()
                       
                       newRow.["OldPrefix"] <- item.OldPrefix
                       newRow.["NewPrefix"] <- item.NewPrefix
                       newRow.["StartDate"] <- item.StartDate
                       newRow.["EndDate"] <- item.EndDate
                       newRow.["TotalDateInterval"] <- item.TotalDateInterval
                       newRow.["VT_Suffix"] <- item.Suffix
                       newRow.["JS_GeneratedString"] <- item.JsGeneratedString
                       newRow.["CompleteLink"] <- item.CompleteLink
                       newRow.["FileToBeSaved"] <- item.FileToBeSaved
                       newRow.["PartialLink"] <- item.PartialLink
                       
                       dt.Rows.Add(newRow)
            )                  

    let internal sortLinksOut (dt : DataTable) (dataToBeInserted : DtDtoSend list) validity : Result<(CompleteLink * FileToBeSaved) list, PdfDownloadErrors> = 
               
        try            
            try                
                insertIntoDataTable dt dataToBeInserted  

                let condition dateValidityStart dateValidityEnd currentTime (fileToBeSaved : string) = 

                    match validity with 
                    | CurrentValidity           -> 
                                                 ((dateValidityStart <= currentTime
                                                 && 
                                                 dateValidityEnd >= currentTime)
                                                 ||
                                                 (dateValidityStart = currentTime 
                                                 && 
                                                 dateValidityEnd = currentTime))
                                                 &&
                                                 (
                                                    not <| fileToBeSaved.Contains("046_2024_01_02_2024_12_14")
                                                 )
                                                 &&
                                                 (
                                                    not <| fileToBeSaved.Contains("020_2024_01_02_2024_12_14")
                                                 )

                    | FutureValidity            ->
                                                 dateValidityStart > currentTime
                  
                    | WithoutReplacementService ->                                         
                                                 ((dateValidityStart <= currentTime 
                                                 && 
                                                 dateValidityEnd >= currentTime)
                                                 ||
                                                 (dateValidityStart = currentTime 
                                                 && 
                                                 dateValidityEnd = currentTime))
                                                 &&
                                                 (not <| fileToBeSaved.Contains("_v") 
                                                 && not <| fileToBeSaved.Contains("X")
                                                 && not <| fileToBeSaved.Contains("NAD")) 
                                                 &&
                                                 (dateValidityEnd <> summerHolidayEnd1
                                                 && 
                                                 dateValidityEnd <> summerHolidayEnd2)
                                                 &&
                                                 (
                                                    not <| fileToBeSaved.Contains("020_2024_01_02_2024_12_14")
                                                 )
                                        
                let currentTime = DateTime.Now.Date

                let dtDataDtoGetDataTable (row : DataRow) : DtDtoGet =                         
                    {           
                        NewPrefix = Convert.ToString (row.["NewPrefix"]) |> Option.ofNullEmpty
                        StartDate = Convert.ToDateTime (row.["StartDate"]) |> Option.ofNull
                        EndDate = Convert.ToDateTime (row.["EndDate"]) |> Option.ofNull
                        CompleteLink = Convert.ToString (row.["CompleteLink"]) |> Option.ofNullEmpty
                        FileToBeSaved = Convert.ToString (row.["FileToBeSaved"]) |> Option.ofNullEmpty
                        PartialLink = Convert.ToString (row.["PartialLink"]) |> Option.ofNullEmpty 
                    } 

                let dataTransformation row = dtDataDtoGetDataTable >> dtDataTransformLayerGet <| row                  
                  
                let seqFromDataTable = dt.AsEnumerable() |> Seq.distinct 
                        
                validity 
                |> function
                    | FutureValidity ->                             
                                      seqFromDataTable    
                                      |> Seq.groupBy (fun row -> (row |> dataTransformation).PartialLink)
                                      |> Seq.map (fun (partialLink, group) -> group |> Seq.head)
                                      |> Seq.filter
                                          (fun row ->
                                                    let startDate = (row |> dataTransformation).StartDate |> function StartDateDt value -> value
                                                    let endDate = (row |> dataTransformation).EndDate |> function EndDateDt value -> value
                                                    let fileToBeSaved = (row |> dataTransformation).FileToBeSaved |> function FileToBeSaved value -> value                      
                                        
                                                    condition startDate endDate currentTime fileToBeSaved
                                          )     
                                      |> Seq.map
                                          (fun row ->
                                                    (row |> dataTransformation).CompleteLink,
                                                    (row |> dataTransformation).FileToBeSaved
                                          )
                                      |> Seq.distinct //na rozdil od ITVF v SQL se musi pouzit distinct                                     
                                      |> List.ofSeq
                                      |> Ok

                    | _              -> 
                                      seqFromDataTable
                                      |> Seq.groupBy (fun row -> (row |> dataTransformation).PartialLink)
                                      |> Seq.map (fun (partialLink, group) -> group |> Seq.head)
                                      |> Seq.filter
                                          (fun row ->
                                                    let startDate = (row |> dataTransformation).StartDate |> function StartDateDt value -> value
                                                    let endDate = (row |> dataTransformation).EndDate |> function EndDateDt value -> value
                                                    let fileToBeSaved = (row |> dataTransformation).FileToBeSaved |> function FileToBeSaved value -> value                       
                                        
                                                    condition startDate endDate currentTime fileToBeSaved
                                          )           
                                      |> Seq.sortByDescending (fun row -> (row |> dataTransformation).StartDate)
                                      |> Seq.groupBy (fun row -> (row |> dataTransformation).NewPrefix)
                                      |> Seq.map
                                          (fun (newPrefix, group)
                                              ->
                                               newPrefix, 
                                               group |> Seq.head
                                          )
                                      |> Seq.map
                                          (fun (newPrefix, row) 
                                              ->
                                               (row |> dataTransformation).CompleteLink,
                                               (row |> dataTransformation).FileToBeSaved
                                          )
                                      |> Seq.distinct 
                                      |> List.ofSeq
                                      |> Ok
            finally
                dt.Clear()               
    
        with 
        | ex ->
              string ex.Message |> ignore //TODO logfile
              Error DataTableError