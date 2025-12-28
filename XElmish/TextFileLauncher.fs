namespace OdisTimetableDownloaderMAUI

open Microsoft.Maui.Storage
open Microsoft.Maui.ApplicationModel

open FsToolkit.ErrorHandling

open Helpers
open Types.Haskell_IO_Monad_Simulation

module TextFileLauncher =  
   
   let internal openTextFileReadOnly (path : string) = 

       IO (fun ()
               ->
               option 
                   {
                       try
                           let! filePath = SafeFullPath.safeFullPathOption path 
                           let file = System.IO.FileInfo filePath 
                           
                           let safeAsync = 
                               async
                                   {
                                       try
                                           let request : OpenFileRequest = 
                                               OpenFileRequest
                                                   (
                                                       Title = file.Name,
                                                       File = ReadOnlyFile file.FullName  // This is read-only
                                                   )  
                                           
                                           let! result = Launcher.Default.OpenAsync request |> Async.AwaitTask
                                           
                                           return result
                                       with 
                                       | _ -> return false
                                   }                          
                           return safeAsync 
                       with
                       | _ -> return! None
                   }
       )