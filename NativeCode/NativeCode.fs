namespace NativeHelpers

open System
open System.Runtime.InteropServices

module Native =

    [<DllImport(@"copyingDirectories.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)>]
    extern void CopyDirContent64(
        [<MarshalAs(UnmanagedType.LPWStr)>] string source,
        [<MarshalAs(UnmanagedType.LPWStr)>] string targetParent,
        int copyOptions,
        int overwriteOptions
    )

    //copyOptions 0 = cely adresar vcetne podadresaru a obsahu, 1 = jen obsah adresare vcetne podadresaru
    //overwriteOptions 0 = overwrite all, 1 = overwrite all older, jina hodnota nez 0 ci 1 = neprovede se nic