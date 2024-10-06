namespace Records

open System
open System.Data

open Types
open ErrorTypes

open Settings.SettingsKODIS

open DataModelling.DataModel

module SortRecordData =  
       
    let internal sortLinksOut (dataToBeFiltered : RcData list) currentTime dateTimeMinValue validity = 
         
        let condition dateValidityStart dateValidityEnd (fileToBeSaved : string) = 

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
                
        let dataToBeFiltered = dataToBeFiltered |> List.toSeq |> Seq.distinct   
                
        validity 
        |> function
            | FutureValidity ->  
                              dataToBeFiltered                                                                           
                              |> Seq.groupBy (fun row -> row.PartialLinkRc)
                              |> Seq.map (fun (partialLink, group) -> group |> Seq.tryHead)
                              |> Seq.choose id //tise to nechame projit 
                              |> Seq.filter
                                  (fun row ->
                                            let startDate = 
                                                row.StartDateRc
                                                |> function StartDateRcOpt value -> value
                                                |> function Some value -> value | None -> dateTimeMinValue
                                            let endDate = 
                                                row.EndDateRc                                                         
                                                |> function EndDateRcOpt value -> value
                                                |> function Some value -> value | None -> dateTimeMinValue
                                            let fileToBeSaved = row.FileToBeSavedRc |> function FileToBeSaved value -> value                         
                                        
                                            condition startDate endDate fileToBeSaved
                                    )     
                              |> Seq.map
                                  (fun row ->
                                            row.CompleteLinkRc,
                                            row.FileToBeSavedRc
                                  )
                              |> Seq.distinct //na rozdil od ITVF v SQL se musi pouzit distinct                                     
                              |> List.ofSeq
                              
            | _              -> 
                              dataToBeFiltered  
                              |> Seq.groupBy (fun row -> row.PartialLinkRc)
                              |> Seq.map (fun (partialLink, group) -> group |> Seq.tryHead)
                              |> Seq.choose id //tise to nechame projit 
                              |> Seq.filter
                                  (fun row ->
                                            let startDate = 
                                                row.StartDateRc
                                                |> function StartDateRcOpt value -> value
                                                |> function Some value -> value | None -> dateTimeMinValue
                                            let endDate = 
                                                row.EndDateRc                                                         
                                                |> function EndDateRcOpt value -> value
                                                |> function Some value -> value | None -> dateTimeMinValue
                                            let fileToBeSaved = row.FileToBeSavedRc  |> function FileToBeSaved value -> value                      
                                        
                                            condition startDate endDate fileToBeSaved
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