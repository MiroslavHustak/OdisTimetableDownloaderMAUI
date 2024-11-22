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
        | JsonTimeoutError
        | JsonDownloadError     
        | JsonConnectionError       
        | NetConnJsonError of string
           
    type internal PdfDownloadErrors =
        | RcError         
        | DataFilteringError
        | JsonFilteringError //vyjimecne to musime dat tady
        | FileDeleteError
        | CreateFolderError
        | FileDownloadError
        | NoFolderError
        | CanopyError
        | TimeoutError
        | PdfConnectionError
        | ApiResponseError of string
        | ApiDecodingError
        | NetConnPdfError of string