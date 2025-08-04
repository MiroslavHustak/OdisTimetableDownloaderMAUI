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
        | FolderCopyOrMoveErrorMHD
        | StopDownloadingMHD
        | LetItBeMHD
        | TestDuCase of string
   
    type internal JsonDownloadErrors = 
        | JsonTimeoutError
        | JsonDownloadError     
        | JsonConnectionError       
        | NetConnJsonError of string
        | StopJsonDownloading
        | FolderMovingError
        | LetItBeKodis
           
    type internal PdfDownloadErrors =
        | RcError         
        | DataFilteringError
        | JsonFilteringError //vyjimecne to musime dat tady
        | FileDeleteError
        | FolderMovingError4
        | CreateFolderError4
        | CreateFolderError2
        | NoPermissionError
        | FileDownloadError
        | NoFolderError
        | CanopyError
        | TimeoutError
        | PdfConnectionError
        | ApiResponseError of string
        | ApiDecodingError
        | NetConnPdfError of string
        | StopDownloading
        | LetItBeKodis4