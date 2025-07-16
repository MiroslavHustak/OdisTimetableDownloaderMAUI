namespace Types

module ErrorTypes =
   
    [<Struct>] 
    type internal MHDErrors =       
        | BadRequest
        | InternalServerError
        | NotImplemented 
        | ServiceUnavailable        
        | NotFound
        | CofeeMakerUnavailable
        | FileDownloadErrorMHD
        | ConnectionError
        | FileDeleteErrorMHD
        | FileCopyingErrorMHD
        | StopDownloadingMHD
        | TestDuCase of string
   
    type internal JsonDownloadErrors = 
        | JsonTimeoutError
        | JsonDownloadError     
        | JsonConnectionError       
        | NetConnJsonError of string
        | StopJsonDownloading
           
    type internal PdfDownloadErrors =
        | RcError         
        | DataFilteringError
        | JsonFilteringError //vyjimecne to musime dat tady
        | FileDeleteError
        | CreateFolderError
        | CreateFolderError1
        | FileDownloadError
        | NoFolderError
        | CanopyError
        | TimeoutError
        | PdfConnectionError
        | ApiResponseError of string
        | ApiDecodingError
        | NetConnPdfError of string
        | StopDownloading