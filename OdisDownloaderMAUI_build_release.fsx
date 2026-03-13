#r "nuget: Fake.Core.Target"
#r "nuget: Fake.Core.Process"

open Fake.Core

// Bootstrap FAKE context for plain "dotnet fsi" usage
let execContext = Fake.Core.Context.FakeExecutionContext.Create false "build.fsx" []
Fake.Core.Context.setExecutionContext (Fake.Core.Context.RuntimeContext.Fake execContext)

let projectDir = "E:/FabulousMAUI/OdisTimetableDownloaderMAUI"
let fsproj     = "OdisTimetableDownloaderMAUI.fsproj"

Target.create "Build" 
    (fun _ 
        ->
        let result =
            CreateProcess.fromRawCommandLine
                "dotnet"
                $"build {fsproj} -c Release"
            |> CreateProcess.withWorkingDirectory projectDir
            |> Proc.run

        match result.ExitCode <> 0 with
        | true  -> failwith "Build failed"
        | false -> ()
    )

Target.runOrDefault "Build"