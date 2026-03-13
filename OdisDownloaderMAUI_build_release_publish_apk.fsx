#r "nuget: Fake.Core.Target"
#r "nuget: Fake.Core.Process"
#r "nuget: Fake.IO.FileSystem"

open Fake.Core
open Fake.Core.TargetOperators

// Bootstrap FAKE context for plain "dotnet fsi" usage
let execContext = Fake.Core.Context.FakeExecutionContext.Create false "build.fsx" []
Fake.Core.Context.setExecutionContext (Fake.Core.Context.RuntimeContext.Fake execContext)

let projectDir = "E:/FabulousMAUI/OdisTimetableDownloaderMAUI"
let fsproj     = "OdisTimetableDownloaderMAUI.fsproj"

Target.create "Clean" 
    (fun _ 
        ->
        let result =
            CreateProcess.fromRawCommandLine
                "dotnet"
                $"clean {fsproj} -c Release"
            |> CreateProcess.withWorkingDirectory projectDir
            |> Proc.run

        match result.ExitCode <> 0 with
        | true  -> failwith "Clean failed"
        | false -> ()
)

Target.create "Publish" 
    (fun _ 
        ->
        let args =
            [ $"publish {fsproj}"
              "-f net8.0-android"
              "-c Release"
              "-p:AndroidPackageFormat=apk"
              "-p:PublishTrimmed=true"
              "-p:EmbedAssembliesIntoApk=true" ]
            |> String.concat " "

        let result =
            CreateProcess.fromRawCommandLine "dotnet" args
            |> CreateProcess.withWorkingDirectory projectDir
            |> Proc.run

        match result.ExitCode <> 0 with
        | true  -> failwith "Publish failed"
        | false -> ()
)

"Clean" ==> "Publish"

Target.runOrDefault "Publish"