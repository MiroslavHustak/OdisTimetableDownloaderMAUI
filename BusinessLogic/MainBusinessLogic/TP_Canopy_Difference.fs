namespace BusinessLogic

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading

//******************************************

open FSharp.Data
open FsToolkit.ErrorHandling

//******************************************

open Settings
open Settings.SettingsGeneral

//TODO: dodelat
module TP_Canopy_Difference = 
    
    let private getDirNames pathToDir = Directory.EnumerateDirectories pathToDir         
        
    let private getUniqueFileNames (folderPathTP: string) (folderPathCanopy: string) =

        let fileNamesTP =
            Directory.EnumerateFiles folderPathTP
            |> Seq.map Path.GetFileName
            |> Set.ofSeq
        
        let fileNamesCanopy =
            Directory.EnumerateFiles folderPathCanopy
            |> Seq.map Path.GetFileName
            |> Set.ofSeq
        
        Set.difference fileNamesTP fileNamesCanopy |> Set.toList, Set.difference fileNamesCanopy fileNamesTP |> Set.toList
    
    let private result2 (folderPathTP: string) (folderPathCanopy: string) =  
    
        (getDirNames folderPathTP, getDirNames folderPathCanopy)
        ||> Seq.iter2
            (fun pathTP pathCanopy 
                ->
                let uniqueFileNamesTP, uniqueFileNamesCanopy = getUniqueFileNames pathTP pathCanopy 
                printfn "Je v TP, ale chybi v Canopy %A" uniqueFileNamesTP                  
                printfn "Je v Canopy, ale chybi v TP %A" uniqueFileNamesCanopy
                printfn "************************************************" 
            )  
            
    let private result (folderPathTP: string) (folderPathCanopy: string) =
       
        match folderPathTP = pathTP_FutureValidity && folderPathCanopy = pathCanopy_FutureValidity with
        | true  -> (seq {folderPathTP}, seq {folderPathCanopy})
        | false -> (getDirNames folderPathTP, getDirNames folderPathCanopy)

        ||> Seq.iter2
            (fun pathTP pathCanopy
                ->
                let uniqueFileNamesTP, uniqueFileNamesCanopy = getUniqueFileNames pathTP pathCanopy 
                printfn "Je v TP, ale chybi v Canopy %A" uniqueFileNamesTP                  
                printfn "Je v Canopy, ale chybi v TP %A" uniqueFileNamesCanopy
                printfn "************************************************" 
            )
         
    let internal main () = //Rozpracovana cast, netestovano na pritomnost souboru      
            
        try     
            printfn "CurrentValidity"
            result pathTP_CurrentValidity pathCanopy_CurrentValidity
       
            printfn "FutureValidity"
            let uniqueFileNamesTP, uniqueFileNamesCanopy = getUniqueFileNames pathTP_FutureValidity pathCanopy_FutureValidity 
            printfn "Je v TP, ale chybi v Canopy %A" uniqueFileNamesTP                  
            printfn "Je v Canopy, ale chybi v TP %A" uniqueFileNamesCanopy
            printfn "************************************************" 
         
            printfn "WithoutReplacementService"
            result pathTP_WithoutReplacementService pathCanopy_WithoutReplacementService
        with
        | ex -> printfn "%s\n" (string ex.Message)


    