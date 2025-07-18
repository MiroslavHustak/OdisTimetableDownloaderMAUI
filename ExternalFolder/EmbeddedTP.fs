namespace EmbeddedTP

open System
open FSharp.Data

module EmbeddedTP =
    
     //pro type provider musi byt konstanta (nemozu pouzit sprintf partialPathJson) a musi byt forward slash"

    let [<Literal>] ResolutionFolder = __SOURCE_DIRECTORY__

    type JsonProvider1 =
        JsonProvider<"KODISJson/kodisMHDTotal.json", EmbeddedResource = "EmbeddedTP, EmbeddedTP.KODISJson.kodisMHDTotal.json", ResolutionFolder = ResolutionFolder>

    type JsonProvider2 =
        JsonProvider<"KODISJson/kodisMHDTotal2_0.json", EmbeddedResource = "EmbeddedTP, EmbeddedTP.KODISJson.kodisMHDTotal2_0.json", ResolutionFolder = ResolutionFolder>

    let pathkodisMHDTotal = 
        try
            System.IO.Path.Combine(ResolutionFolder, @"KODISJson/kodisMHDTotal.json")
        with
        |_ -> String.Empty

    let pathkodisMHDTotal2_0 = 
        try
            System.IO.Path.Combine(ResolutionFolder, @"KODISJson/kodisMHDTotal2_0.json")
        with
        |_ -> String.Empty


(*
Vypada to, ze tento projekt nemoze mit adresar uvnitr hlavniho solution, tudiz v GitHubu pak EmbeddedTP neni

V adresari, kaj je .git, zrob adresar ExternalFolder (radeji takto, at neni EmbeddedTP dva razy)

Do nej presypej obsah EmbeddedTP

Vcil jeden command po druhem, ne najednou

git add ExternalFolder

git commit -m "Add symlink to external folder"

git push

Opakuj pri kazde zmene EmbeddedTP
*)