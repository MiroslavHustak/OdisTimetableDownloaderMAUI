namespace AssemblyInfo

module AssemblyInfo =

    open System.Runtime.CompilerServices

    [<assembly : InternalsVisibleTo("Test.xUnit")>]
    [<assembly : InternalsVisibleTo("Test.Expecto")>]
    [<assembly : InternalsVisibleTo("Test.FsCheck")>]
    do ()

