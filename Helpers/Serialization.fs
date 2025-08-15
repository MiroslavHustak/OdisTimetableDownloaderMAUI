namespace Helpers

open System.IO

//************************************************************

open FsToolkit.ErrorHandling

//************************************************************

open Helpers
open Helpers.Builders

open DirFileHelper

open Types.Haskell_IO_Monad_Simulation

module Serialization =     

    let internal serializeWithThoth (json : string) (path : string) : IO<Result<unit, string>> = 

        IO (fun () 
                ->        
                let prepareJsonAsyncWrite () = // it only prepares an asynchronous operation that writes the json string
      
                    try  
                        pyramidOfDoom //nelze option CE (TODO: look at the definition code to find out why)
                            {                   
                                let! path = Path.GetFullPath path |> Option.ofNullEmpty, None  
                                               
                                let pathOption =
                                    File.Exists path
                                    |> Option.fromBool path
                                    |> Option.orElseWith 
                                        (fun ()
                                            ->
                                            File.WriteAllText(path, jsonEmpty)
                                            Some path
                                        ) 

                                let! path = pathOption, None 
                                                             
                                let writer = new StreamWriter(path, false)                
                                let!_ = writer |> Option.ofNull, None
                                                                                 
                                return Some writer
                            }         
                      
                        |> Option.map 
                            (fun (writer : StreamWriter) 
                                ->
                                async
                                    {
                                        use writer = writer
                                        do! writer.WriteAsync json |> Async.AwaitTask

                                        return! writer.FlushAsync() |> Async.AwaitTask
                                    }
                            )
                    with
                    | _ -> None

                async
                    {
                        try    
                            match prepareJsonAsyncWrite () with
                            | Some asyncWriter     
                                ->
                                do! asyncWriter    
                                return Ok ()

                            | None
                                ->                              
                                return Error "StreamWriter Error" 
                        with
                        | ex -> return Error (string ex.Message) 
                    }   
                |> Async.RunSynchronously 
        )

(*
import Control.Monad (when, unless)
import Control.Monad.IO.Class (MonadIO, liftIO)
import Control.Monad.Trans.Maybe (MaybeT, runMaybeT)
import Control.Exception (try, IOException)
import System.IO (withFile, IOMode(WriteMode), hPutStr, hFlush)
import System.Directory (doesFileExist)
import Data.Text (Text)
import qualified Data.Text as Text
import qualified Data.Text.IO as TextIO

serializeWithThoth :: Text -> FilePath -> IO (Either String ())
serializeWithThoth json path = do
  result <- runMaybeT $ do
    -- Step 1: Validate path (non-empty, non-null)
    fullPath <- MaybeT $ do
      let fp = Text.pack path
      if Text.null fp
        then pure Nothing
        else pure (Just fp)

    exists <- liftIO $ doesFileExist (Text.unpack fullPath)
    unless exists $ liftIO $ TextIO.writeFile (Text.unpack fullPath) "{}"

    pure fullPath

  case result of
    Nothing -> pure (Left "Invalid path or file creation failed")
    Just fp -> do
      -- Step 4: Write JSON to file
      eResult <- try @IOException $ withFile (Text.unpack fp) WriteMode $ \h -> do
        TextIO.hPutStr h json
        hFlush h
      case eResult of
        Left ex -> pure (Left $ show ex)
        Right () -> pure (Right ())
*)