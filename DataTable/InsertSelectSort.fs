namespace DataTable

open System
open System.Data

//******************************

open Types

open Helpers
open Helpers.Builders
open Helpers.CloseApp

open Settings
open Settings.SettingsKODIS

open DataModelling.Dto
open DataModelling.DataModel

open TransformationLayers.TransformationLayerGet


//chyby vezme tryWith Err18
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
                       
                       newRow.["OldPrefix"] <- item.oldPrefix
                       newRow.["NewPrefix"] <- item.newPrefix
                       newRow.["StartDate"] <- item.startDate
                       newRow.["EndDate"] <- item.endDate
                       newRow.["TotalDateInterval"] <- item.totalDateInterval
                       newRow.["VT_Suffix"] <- item.suffix
                       newRow.["JS_GeneratedString"] <- item.jsGeneratedString
                       newRow.["CompleteLink"] <- item.completeLink
                       newRow.["FileToBeSaved"] <- item.fileToBeSaved
                       newRow.["PartialLink"] <- item.partialLink
                       
                       dt.Rows.Add(newRow)
            )                  

    let internal sortLinksOut dt (dataToBeInserted : DtDtoSend list) validity = 

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
                                                     match currentTime >= DateTime(2024, 9, 2) with  
                                                     | true  -> true
                                                     | false -> not <| fileToBeSaved.Contains("046_2024_01_02_2024_12_14")
                                                 )

                    | FutureValidity            ->
                                                 dateValidityStart > currentTime
                    (*  
                    | ReplacementService        -> 
                                                 ((dateValidityStart <= currentTime 
                                                 && 
                                                 dateValidityEnd >= currentTime)
                                                 ||
                                                 (dateValidityStart = currentTime 
                                                 && 
                                                 dateValidityEnd = currentTime))
                                                 &&
                                                 (fileToBeSaved.Contains("_v") 
                                                 || fileToBeSaved.Contains("X")
                                                 || fileToBeSaved.Contains("NAD"))
                    *)
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
                                        
                let currentTime = DateTime.Now.Date

                let dtDataDtoGetDataTable (row : DataRow) : DtDtoGet =                         
                    {           
                        newPrefix = Convert.ToString (row.["NewPrefix"]) |> Option.ofNullEmpty
                        startDate = Convert.ToDateTime (row.["StartDate"]) |> Option.ofNull
                        endDate = Convert.ToDateTime (row.["EndDate"]) |> Option.ofNull
                        completeLink = Convert.ToString (row.["CompleteLink"]) |> Option.ofNullEmpty
                        fileToBeSaved = Convert.ToString (row.["FileToBeSaved"]) |> Option.ofNullEmpty
                        partialLink = Convert.ToString (row.["PartialLink"]) |> Option.ofNullEmpty 
                    } 

                let dataTransformation row =                                 
                    try Ok (dtDataDtoGetDataTable >> dtDataTransformLayerGet <| row)                  
                    with ex -> Error <| string ex.Message
                                       
                    |> function
                        | Ok value  -> 
                                     value  
                        | Error err ->
                                     //logInfoMsg <| sprintf "Err901A %s" err 
                                     //closeItBaby err
                                     dtDataDtoGetDataTable >> dtDataTransformLayerGet <| row 

                let seqFromDataTable = dt.AsEnumerable() |> Seq.distinct 
                        
                validity 
                |> function
                    | FutureValidity ->                             
                                      seqFromDataTable    
                                      |> Seq.groupBy (fun row -> (row |> dataTransformation).partialLink)
                                      |> Seq.map (fun (partialLink, group) -> group |> Seq.head)
                                      |> Seq.filter
                                          (fun row ->
                                                    let startDate = (row |> dataTransformation).startDate |> function StartDateDt value -> value
                                                    let endDate = (row |> dataTransformation).endDate |> function EndDateDt value -> value
                                                    let fileToBeSaved = (row |> dataTransformation).fileToBeSaved |> function FileToBeSaved value -> value                      
                                        
                                                    condition startDate endDate currentTime fileToBeSaved
                                          )     
                                      |> Seq.map
                                          (fun row ->
                                                    (row |> dataTransformation).completeLink,
                                                    (row |> dataTransformation).fileToBeSaved
                                          )
                                      |> Seq.distinct //na rozdil od ITVF v SQL se musi pouzit distinct                                     
                                      |> List.ofSeq
                                      |> Ok

                    | _              -> 
                                      seqFromDataTable
                                      |> Seq.groupBy (fun row -> (row |> dataTransformation).partialLink)
                                      |> Seq.map (fun (partialLink, group) -> group |> Seq.head)
                                      |> Seq.filter
                                          (fun row ->
                                                    let startDate = (row |> dataTransformation).startDate |> function StartDateDt value -> value
                                                    let endDate = (row |> dataTransformation).endDate |> function EndDateDt value -> value
                                                    let fileToBeSaved = (row |> dataTransformation).fileToBeSaved |> function FileToBeSaved value -> value                       
                                        
                                                    condition startDate endDate currentTime fileToBeSaved
                                          )           
                                      |> Seq.sortByDescending (fun row -> (row |> dataTransformation).startDate)
                                      |> Seq.groupBy (fun row -> (row |> dataTransformation).newPrefix)
                                      |> Seq.map
                                          (fun (newPrefix, group)
                                              ->
                                               newPrefix, 
                                               group |> Seq.head
                                          )
                                      |> Seq.map
                                          (fun (newPrefix, row) 
                                              ->
                                               (row |> dataTransformation).completeLink,
                                               (row |> dataTransformation).fileToBeSaved
                                          )
                                      |> Seq.distinct 
                                      |> List.ofSeq
                                      |> Ok
            finally
                dt.Clear()               
    
        with ex -> Error <| string ex.Message
        
        |> function
            | Ok value  -> 
                         value  
            | Error err ->
                         //logInfoMsg <| sprintf "Err901B %s" err 
                         //closeItBaby err
                         []

          