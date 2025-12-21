namespace Types

module ErrorTypes =

    type [<Struct>] internal MHDErrors =       
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
   
    type [<Struct>] internal JsonDownloadErrors = 
        | JsonTimeoutError
        | JsonDownloadError     
        | JsonConnectionError       
        | NetConnJsonError of string //string is still heap-allocated, ale tady by to nemelo vadit
        | StopJsonDownloading //cancellation
        | FolderMovingError
        | LetItBeKodis

    type [<Struct>] internal JsonParsingErrors = 
        | JsonParsingError
        | JsonDataFilteringError
        | StopJsonParsing //cancellation  

    type [<Struct>] internal PdfDownloadErrors =
        | RcError         
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
        | StopDownloading //cancellation
        | LetItBeKodis4

    type internal JsonParsingAndPdfDownloadErrors =
        | PdfError of PdfDownloadErrors
        | JsonError of JsonParsingErrors