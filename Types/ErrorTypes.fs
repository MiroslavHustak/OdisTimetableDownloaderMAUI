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
        | TimeoutErrorMHD
        | TlsHandshakeErrorMHD
   
    type [<Struct>] internal JsonDownloadErrors = 
        | JsonTimeoutError
        | JsonDownloadError     
        | JsonConnectionError       
        | NetConnJsonError of string //string is still heap-allocated, ale tady by to nemelo vadit
        | StopJsonDownloading //cancellation
        | FolderMovingError
        | JsonLetItBeKodis
        | JsonTlsHandshakeError

    type [<Struct>] internal JsonParsingErrors = 
        | JsonParsingError
        | JsonParsingTimeoutError
        | JsonDataFilteringError
        | StopJsonParsing //cancellation  
        | LetItBeParsing

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
        | ApiResponseError
        | ApiDecodingError
        | NetConnPdfError of string
        | StopDownloading //cancellation
        | LetItBeKodis4
        | TlsHandshakeError

    type internal ParsingAndDownloadingErrors =
        | PdfDownloadError2 of PdfDownloadErrors
        | JsonParsingError2 of JsonParsingErrors
        | JsonDownloadError2 of JsonDownloadErrors

    type internal ExceptionClassification =
        | TlsError2
        | TimeoutError2  
        | NetworkError2
        | UnknownError2  