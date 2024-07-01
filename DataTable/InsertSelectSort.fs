namespace DataTable

open System
open System.Data

open FsToolkit
open FsToolkit.ErrorHandling

open Types

open Helpers
open Helpers.Builders
open Helpers.CloseApp

open Settings

open DataModelling.Dto
open DataModelling.DataModel

open TransformationLayers.TransformationLayerGet

module InsertSelectSort = 
        
    let private dt = 

        let dtTimetableLinks = new DataTable()
        
        let addColumn (name : string) (dataType : Type) =

            let dtColumn = new DataColumn()

            dtColumn.DataType <- dataType
            dtColumn.ColumnName <- name

            dtTimetableLinks.Columns.Add(dtColumn)
        
        //musi byt jen .NET type, aby nebyly problemy 
        addColumn "OldPrefix" typeof<string>
        addColumn "NewPrefix" typeof<string>
        addColumn "StartDate" typeof<DateTime>
        addColumn "EndDate" typeof<DateTime>
        addColumn "TotalDateInterval" typeof<string>
        addColumn "VT_Suffix" typeof<string>
        addColumn "JS_GeneratedString" typeof<string>
        addColumn "CompleteLink" typeof<string>
        addColumn "FileToBeSaved" typeof<string>
        
        dtTimetableLinks

    let private insertIntoDataTable (dataToBeInserted : DtDtoSend list) =
            
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

                       dt.Rows.Add(newRow)
            )                  

    let internal sortLinksOut () (dataToBeInserted : DtDtoSend list) validity = 
                
        insertIntoDataTable dataToBeInserted  

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

        let currentTime = DateTime.Now.Date

        let dtDataDtoGetDataTable (row : DataRow) : DtDtoGet =                         
            {           
                newPrefix = Convert.ToString (row.["NewPrefix"]) |> Option.ofNullEmpty
                startDate = Convert.ToDateTime (row.["StartDate"]) |> Option.ofNull
                endDate = Convert.ToDateTime (row.["EndDate"]) |> Option.ofNull
                completeLink = Convert.ToString (row.["CompleteLink"]) |> Option.ofNullEmpty
                fileToBeSaved = Convert.ToString (row.["FileToBeSaved"]) |> Option.ofNullEmpty
            } 

        let dataTransformation row =                                 
            try
                dtDataDtoGetDataTable >> dtDataTransformLayerGet <| row
            with
            | ex -> 
                  closeItBaby (string ex.Message)
                  dtDataDtoGetDataTable >> dtDataTransformLayerGet <| row                 
        
        let seqFromDataTable = dt.AsEnumerable() |> Seq.distinct 

        match validity with
        | FutureValidity -> 
                          seqFromDataTable                          
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

        | _              -> 
                          seqFromDataTable
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
                              (fun (_ , row) 
                                  ->
                                   (row |> dataTransformation).completeLink,
                                   (row |> dataTransformation).fileToBeSaved
                              )
                          |> Seq.distinct 
                          |> List.ofSeq