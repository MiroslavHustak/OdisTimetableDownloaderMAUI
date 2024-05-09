namespace TransformationLayers

open System

open Types

open Helpers
open Helpers.Builders
open Helpers.TryParserDate

open DataModelling.Dto
open DataModelling.DataModel

//Type-driven design

module TransformationLayerGet =
       
    let private dtDataTransformLayerGetDefault : DtDataGet = 
        {      
            newPrefix = NewPrefix String.Empty
            startDate = StartDateDt DateTime.MinValue
            endDate = EndDateDt DateTime.MinValue
            completeLink = CompleteLink String.Empty
            fileToBeSaved = FileToBeSaved String.Empty
        } 

    let internal dtDataTransformLayerGet (dtDtoGet : DtDtoGet) : DtDataGet =  
        
        pyramidOfDoom
           {
               let! newPrefix = dtDtoGet.newPrefix, dtDataTransformLayerGetDefault //pri nesouladu se vraci vse jako default bez ohledu na ostatni vysledky
               let! startDate = dtDtoGet.startDate, dtDataTransformLayerGetDefault 
               let! endDate = dtDtoGet.endDate, dtDataTransformLayerGetDefault 
               let! completeLink = dtDtoGet.completeLink, dtDataTransformLayerGetDefault 
               let! fileToBeSaved = dtDtoGet.fileToBeSaved, dtDataTransformLayerGetDefault 

               return //vraci pouze pokud je vse spravne
                   {      
                       newPrefix = NewPrefix newPrefix
                       startDate = StartDateDt startDate
                       endDate = EndDateDt endDate
                       completeLink = CompleteLink completeLink
                       fileToBeSaved = FileToBeSaved fileToBeSaved
                   } 
           }

module TransformationLayerSend =
    
    let internal dtDataTransformLayerSend (dtDataSend : DtDataSend) : DtDtoSend =
        {
            oldPrefix = dtDataSend.oldPrefix |> function OldPrefix value -> value
            newPrefix = dtDataSend.newPrefix |> function NewPrefix value -> value
            startDate =
                let startdate = dtDataSend.startDate |> function StartDateDtOpt value -> value
                match startdate with Some value -> value | None -> DateTime.MinValue
            endDate = 
                let endDate = dtDataSend.endDate |> function EndDateDtOpt value -> value
                match endDate with Some value -> value | None -> DateTime.MinValue
            totalDateInterval = dtDataSend.totalDateInterval |> function TotalDateInterval value -> value
            suffix = dtDataSend.suffix |> function Suffix value -> value
            jsGeneratedString = dtDataSend.jsGeneratedString |> function JsGeneratedString value -> value
            completeLink = dtDataSend.completeLink |> function CompleteLink value -> value
            fileToBeSaved = dtDataSend.fileToBeSaved |> function FileToBeSaved value -> value
        }