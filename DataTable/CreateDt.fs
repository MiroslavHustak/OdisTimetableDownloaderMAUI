namespace DataTable

open System
open System.Data

//chyby vezme tryWith Err901B
module CreateDt = 
        
    let internal dt () = 

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
        addColumn "PartialLink" typeof<string>
        
        dtTimetableLinks