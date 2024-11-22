namespace Types

module ErrorTypes =

    [<Struct>] 
    type internal ConnErrorCode = 
        {
            BadRequest : string
            InternalServerError : string
            NotImplemented : string
            ServiceUnavailable : string        
            NotFound : string
            CofeeMakerUnavailable : string
        }

    type internal JsonDownloadErrors = 
        | JsonDownloadError     
        | CancelJsonProcess  
        | JsonConnectionError
        | NetConnJsonError of string
           
    type internal PdfDownloadErrors =
        | RcError 
        | JsonFilteringError
        | DataFilteringError
        | FileDeleteError
        | CreateFolderError
        | FileDownloadError
        | NoFolderError
        | CanopyError
        | TimeoutError
        | CancelPdfProcess  //TODO zrusit
        | PdfConnectionError
        | ApiResponseError of string
        | ApiDecodingError
        | NetConnPdfError of string