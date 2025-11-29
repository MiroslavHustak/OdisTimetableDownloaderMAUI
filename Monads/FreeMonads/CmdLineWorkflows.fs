namespace Helpers

module CommandLineWorkflow =
           
    type internal CommandLineInstruction<'a> =
        | SourceFilepath of (Result<string, string> -> 'a)
        | DestinFilepath of (Result<string, string> -> 'a)
        | CopyOrMove of (Result<string, string> * Result<string, string>)

    type internal CommandLineProgram<'a> =
        | Pure of 'a 
        | Free of CommandLineInstruction<CommandLineProgram<'a>>

    let internal mapI f = 
        function
            | SourceFilepath next -> SourceFilepath (next >> f)
            | DestinFilepath next -> DestinFilepath (next >> f)
            | CopyOrMove s        -> CopyOrMove s 
     
    //[<TailCall>] neni ani tail recursive ani CPS compliant
    let rec internal bind f = 
        function
            | Free x -> x |> mapI (bind f) |> Free
            | Pure x -> f x

    type internal CommandLineProgramBuilder = CommandLineProgramBuilder with
        member this.Bind(p, f) = //x |> mapI (bind f) |> Free
            match p with
            | Pure x     -> f x
            | Free instr -> Free (mapI (fun p' -> this.Bind(p', f)) instr)
        member _.Return x = Pure x
        member _.ReturnFrom p = p