﻿namespace Types

module ErrorTypes =

    type internal ConnErrorCode = 
        {
            BadRequest : string
            InternalServerError : string
            NotImplemented : string
            ServiceUnavailable : string        
            NotFound : string
            CofeeMakerUnavailable : string
        }

    [<Struct>]     
    type internal TryWithErrors = 
        | IOExnErr of IOExnErr : string
        | UnauthorizedAccessExnErr of UnauthorizedAccessExnErr : string        
        | ArgumentNullExnErr of ArgumentNullExnErr : string //zatim nepouzito
        | FormatExErr of FormatExErr : string  //zatim nepouzito
        | AllOtherErrors of AllOtherErrors : string

    [<Struct>]     
    type internal JsonDownloadErrors = 
        | JsonDownloadError     
        | CancelJsonProcess    

    [<Struct>]     
    type internal PdfDownloadErrors =
        | RcError 
        | JsonFilteringError
        | DataFilteringError
        | FileDeleteError
        | CreateFolderError
        | FileDownloadError
        | CancelPdfProcess  

   