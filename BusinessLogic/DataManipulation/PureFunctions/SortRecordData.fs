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