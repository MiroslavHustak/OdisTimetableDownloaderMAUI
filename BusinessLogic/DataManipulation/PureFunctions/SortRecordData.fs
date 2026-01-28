namespace Records

open Types
open Types.Types

open Settings.SettingsKODIS
open DataModelling.DataModel

module SortRecordData =  

    // Code with array and Array.Parallel.map as fast as the following code with Seq. 
    
    let internal sortLinksOut (dataToBeFiltered : RcData list) validity = 
      
        let condition context2 dateValidityStart dateValidityEnd (fileToBeSaved : string) = 

            match validity with 
            | CurrentValidity
                -> 
                ((dateValidityStart <= context2.currentTime
                && 
                dateValidityEnd >= context2.currentTime)
                ||
                (dateValidityStart = context2.currentTime 
                && 
                dateValidityEnd = context2.currentTime))   

            | FutureValidity          
                ->
                dateValidityStart > context2.currentTime
               
            | LongTermValidity
                ->                                         
                ((dateValidityStart <= context2.currentTime 
                && 
                dateValidityEnd >= context2.currentTime)
                ||
                (dateValidityStart = context2.currentTime 
                && 
                dateValidityEnd = context2.currentTime))
                &&
                (not <| fileToBeSaved.Contains("_v") 
                && not <| fileToBeSaved.Contains("X")
                && not <| fileToBeSaved.Contains("NAD")) 
                &&
                (dateValidityEnd <> context2.summerHolidayEnd1
                && 
                dateValidityEnd <> context2.summerHolidayEnd2)
                           
        let dataToBeFiltered = dataToBeFiltered |> List.toSeq |> Seq.distinct   
             
        validity 
        |> function
            | FutureValidity
                ->  
                dataToBeFiltered                                                                           
                |> Seq.groupBy (fun row -> row.PartialLinkRc)  //nepredelavej to na |> Seq.groupBy _.PartialLinkRc, bo se v tom zase nevyznas...
                |> Seq.map (fun (partialLink, group) -> group |> Seq.tryHead)
                |> Seq.choose id //tise to nechame projit 
                |> Seq.filter
                    (fun row 
                        ->
                        let startDate = 
                            row.StartDateRc
                            |> function StartDateRcOpt value -> value
                            |> Option.defaultValue context2.dateTimeMinValue

                        let endDate = 
                            row.EndDateRc                                                         
                            |> function EndDateRcOpt value -> value
                            |> Option.defaultValue context2.dateTimeMinValue

                        let fileToBeSaved =
                            row.FileToBeSavedRc
                            |> function FileToBeSaved value -> value                         
                                     
                        condition context2 startDate endDate fileToBeSaved
                    )     
                |> Seq.map
                    (fun row 
                        ->
                        row.CompleteLinkRc,
                        row.FileToBeSavedRc
                    )
                |> Seq.distinct //na rozdil od ITVF v SQL se musi pouzit distinct                                     
                |> List.ofSeq
                           
            | _             
                -> 
                dataToBeFiltered  
                |> Seq.groupBy (fun row -> row.PartialLinkRc)
                |> Seq.map (fun (partialLink, group) -> group |> Seq.tryHead)
                |> Seq.choose id //tise to nechame projit 
                |> Seq.filter
                    (fun row
                        ->
                        let startDate = 
                            row.StartDateRc
                            |> function StartDateRcOpt value -> value
                            |> Option.defaultValue context2.dateTimeMinValue

                        let endDate = 
                            row.EndDateRc                                                         
                            |> function EndDateRcOpt value -> value
                            |> Option.defaultValue context2.dateTimeMinValue

                        let fileToBeSaved = 
                            row.FileToBeSavedRc 
                            |> function FileToBeSaved value -> value                      
                                     
                        condition context2 startDate endDate fileToBeSaved
                    )           
                |> Seq.sortByDescending (fun row -> row.StartDateRc)
                |> Seq.groupBy (fun row -> row.NewPrefixRc)
                |> Seq.map
                    (fun (newPrefix, group)
                        ->
                        group 
                        |> Seq.tryHead
                        |> Option.map (fun head -> (newPrefix, head))
                    )                            
                |> Seq.choose id   //tise to nechame projit 
                |> Seq.map
                    (fun (newPrefix, row) 
                        ->
                        row.CompleteLinkRc,
                        row.FileToBeSavedRc
                    )
                |> Seq.distinct 
                |> List.ofSeq

(*
//*************************************************

USE [TimetableDownloader]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER FUNCTION [dbo].[ITVF_GetLinksCurrentValidity] ()   
RETURNS TABLE
AS
RETURN 
(
    WITH CTE AS
    (
        SELECT 
            NewPrefix, 
            StartDate, 
            EndDate, 
            CompleteLink, 
            FileToBeSaved, 
            PartialLink,
            ROW_NUMBER() OVER (PARTITION BY PartialLink ORDER BY FileToBeSaved) AS rn
        FROM TimetableLinks
    )
    SELECT 
        NewPrefix, 
        StartDate, 
        CompleteLink, 
        FileToBeSaved, 
        MaxRow
    FROM
    (
        SELECT 
            NewPrefix, 
            StartDate, 
            EndDate, 
            CompleteLink, 
            FileToBeSaved, 
            rn,
            ROW_NUMBER() OVER (PARTITION BY NewPrefix ORDER BY StartDate DESC) AS MaxRow
        FROM CTE
        WHERE 
            (
                ((StartDate <= FORMAT(GETDATE(), 'yyyy-MM-dd') AND EndDate >= FORMAT(GETDATE(), 'yyyy-MM-dd'))
                OR
                (StartDate = FORMAT(GETDATE(), 'yyyy-MM-dd') AND EndDate = FORMAT(GETDATE(), 'yyyy-MM-dd')))
            )
            AND rn = 1
            AND 
            (               
                FileToBeSaved NOT LIKE '%046_2024_01_02_2024_12_14%'
            )
            AND 
            (               
                FileToBeSaved NOT LIKE '%020_2024_01_02_2024_12_14%'
            )
    ) AS myDerivedTable
    WHERE MaxRow = 1     
);

USE [TimetableDownloader]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER FUNCTION [dbo].[ITVF_GetLinksFutureValidity] ()   
RETURNS TABLE
AS
RETURN 
(
    WITH CTE AS
    (
        SELECT 
            NewPrefix, 
            StartDate, 
            EndDate, 
            CompleteLink, 
            FileToBeSaved, 
            PartialLink,
            ROW_NUMBER() OVER (PARTITION BY PartialLink ORDER BY FileToBeSaved) AS rn
        FROM TimetableLinks
    )
    SELECT 
        NewPrefix, 
        StartDate, 
        CompleteLink, 
        FileToBeSaved 
    FROM
    (
        SELECT 
            NewPrefix, 
            StartDate, 
            EndDate, 
            CompleteLink, 
            FileToBeSaved, 
            rn
        FROM CTE
        WHERE StartDate > FORMAT(GETDATE(), 'yyyy-MM-dd') AND rn = 1
    ) AS myDerivedTable     
);

USE [TimetableDownloader]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
ALTER FUNCTION [dbo].[ITVF_GetLinksWithoutReplacementService] ()   
RETURNS TABLE
AS
RETURN 
(
    WITH CTE AS
    (
        SELECT 
            NewPrefix, 
            StartDate, 
            EndDate, 
            CompleteLink, 
            FileToBeSaved, 
            PartialLink,
            ROW_NUMBER() OVER (PARTITION BY PartialLink ORDER BY FileToBeSaved) AS rn
        FROM TimetableLinks
    )
    SELECT 
        NewPrefix, 
        StartDate, 
        CompleteLink, 
        FileToBeSaved, 
        MaxRow
    FROM
    (
        SELECT 
            NewPrefix, 
            StartDate, 
            EndDate, 
            CompleteLink, 
            FileToBeSaved, 
            rn,
            ROW_NUMBER() OVER (PARTITION BY NewPrefix ORDER BY StartDate DESC) AS MaxRow
        FROM CTE
        WHERE 
        (
            (
                (StartDate <= FORMAT(GETDATE(), 'yyyy-MM-dd') AND EndDate >= FORMAT(GETDATE(), 'yyyy-MM-dd'))
                OR
                (StartDate = FORMAT(GETDATE(), 'yyyy-MM-dd') AND EndDate = FORMAT(GETDATE(), 'yyyy-MM-dd'))
            )
            AND
            (
                CHARINDEX('_v', FileToBeSaved) = 0 -- not
                AND CHARINDEX('X', FileToBeSaved) = 0 -- not
                AND CHARINDEX('NAD', FileToBeSaved) = 0 -- not
            )
            AND rn = 1
            AND EndDate <> '2024-08-31'
            AND EndDate <> '2024-09-01'
            AND 
            (               
                FileToBeSaved NOT LIKE '%020_2024_01_02_2024_12_14%'
            )
        )
    ) AS myDerivedTable
    WHERE MaxRow = 1     
);

*)