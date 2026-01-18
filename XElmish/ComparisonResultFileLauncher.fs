namespace OdisTimetableDownloaderMAUI

open Microsoft.Maui.Storage
open Microsoft.Maui.ApplicationModel

open FsToolkit.ErrorHandling

open Helpers
open Types.Haskell_IO_Monad_Simulation
open Api.Logging

module ComparisonResultFileLauncher =  
   
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
                                                   
                                           return! Launcher.Default.OpenAsync request |> Async.AwaitTask
                                       with 
                                       | ex 
                                           ->
                                           runIO (postToLog2 <| string ex.Message <| "#0001FileLauncher")
                                           return false
                                   }                          
                           return safeAsync 
                       with
                       | ex 
                           ->
                           runIO (postToLog2 <| string ex.Message <| "#0002FileLauncher") 
                           return! None
                   }
       )