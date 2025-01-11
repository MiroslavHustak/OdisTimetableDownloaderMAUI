namespace IO_Operations

open System.IO

//************************************************************
open Types
open Types.ErrorTypes

open Helpers.Builders

open CreatingPathsAndNames

open Settings.SettingsKODIS
open Settings.SettingsGeneral

module IO_Operations =    
    
    let internal deleteAllODISDirectories pathToDir = 

        let deleteIt : Reader<string list, Result<unit, PdfDownloadErrors>> = 
    
            reader //Reader monad for educational purposes only, no real benefit here  
                {
                    let! getDefaultRecordValues = fun env -> env 
                    
                    return 
                        try
                            //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                            let dirInfo = DirectoryInfo pathToDir                                                       
                                in
                                dirInfo.EnumerateDirectories() 
                                |> Seq.filter (fun item -> getDefaultRecordValues |> List.contains item.Name) //prunik dvou kolekci (plus jeste Seq.distinct pro unique items)
                                |> Seq.distinct 
                                |> Seq.iter _.Delete(true)  
                                |> Ok
                                //smazeme pouze adresare obsahujici stare JR, ostatni ponechame              
                        with 
                        | ex ->
                             string ex.Message |> ignore // TODO logfile 
                             Error FileDeleteError
                }

        deleteIt listODISDefault4  

    let internal deleteOneODISDirectory variant pathToDir =

        //smazeme pouze jeden adresar obsahujici stare JR, ostatni ponechame

        let deleteIt : Reader<string list, Result<unit, PdfDownloadErrors>> =  

            reader //Reader monad for educational purposes only, no real benefit here  
                {   
                    let! getDefaultRecordValues = fun env -> env
                                                          
                    return 
                        try
                            //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                            let dirInfo = DirectoryInfo pathToDir        
                                in
                                dirInfo.EnumerateDirectories()
                                |> Seq.filter (fun item -> item.Name = createDirName variant getDefaultRecordValues) 
                                |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce  
                                |> Ok               
                                
                        with 
                        | ex
                            ->
                            string ex.Message |> ignore // TODO logfile 
                            Error FileDeleteError                       
                }

        deleteIt listODISDefault4    
        
    let internal deleteOneODISDirectoryMHD dirName pathToDir = 
        
            try      
                //rozdil mezi Directory a DirectoryInfo viz Unique_Identifier_And_Metadata_File_Creator.sln -> MainLogicDG.fs
                let dirInfo = DirectoryInfo pathToDir   
                    in 
                    dirInfo.EnumerateDirectories()
                    |> Seq.filter (fun item -> item.Name = dirName) 
                    |> Seq.iter _.Delete(true) //trochu je to hack, ale nemusim se zabyvat tryHead, bo moze byt empty kolekce 
                    |> Ok
            with
            | _ -> Error FileDownloadErrorMHD //dpoMsg1    
      
    let internal createFolders dirList =  
        try
            dirList
            |> List.iter
                (fun (dir: string) 
                    ->                
                    match dir.Contains("JR_ODIS_aktualni_vcetne_vyluk") || dir.Contains("JR_ODIS_teoreticky_dlouhodobe_platne_bez_vyluk") with 
                    | true  ->    
                            sortedLines 
                            |> List.iter
                                (fun item
                                    -> 
                                    let dir = dir.Replace("_vyluk", sprintf "%s/%s" "_vyluk" item)
                                    Directory.CreateDirectory dir |> ignore
                                )           
                    | false -> 
                            Directory.CreateDirectory dir |> ignore           
                ) 
            |> Ok

        with 
        | ex
            ->
            string ex.Message |> ignore // TODO logfile 
            Error CreateFolderError     
        
    let internal ensureMainDirectoriesExist () =
       
        try
            [
                partialPathJsonTemp 
                kodisPathTemp 
                kodisPathTemp4 
                dpoPathTemp 
                mdpoPathTemp
            ]        
            |> List.iter
                (fun pathDir 
                    ->
                    match Directory.Exists pathDir with
                    | true  -> () 
                    | false -> Directory.CreateDirectory pathDir |> ignore 
                )
            |> Ok  
        with 
        | ex
            ->
            string ex.Message |> ignore // TODO logfile 
            Error CreateFolderError     