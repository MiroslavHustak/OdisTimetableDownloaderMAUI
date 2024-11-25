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