﻿namespace NativeHelpers

open System
open System.Runtime.InteropServices

module Native =

    //type ProgressCallback = delegate of float * float -> unit
    
    [<DllImport(@"CppHelpers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)>]
    extern int CopyDirContent64(
        [<MarshalAs(UnmanagedType.LPWStr)>] string source,
        [<MarshalAs(UnmanagedType.LPWStr)>] string targetParent,
        int copyOptions,
        int overwriteOptions
        //ProgressCallback callback
    )

    //copyOptions 0 = cely adresar vcetne podadresaru a obsahu, 1 = jen obsah adresare vcetne podadresaru
    //overwriteOptions 0 = overwrite all, 1 = overwrite all older, jina hodnota nez 0 ci 1 = neprovede se nic

    [<DllImport(@"CppHelpers.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.Cdecl)>]
    extern int MoveDirContent64(
        [<MarshalAs(UnmanagedType.LPWStr)>] string source,
        [<MarshalAs(UnmanagedType.LPWStr)>] string targetParent,
        int moveOption  // 0 = move entire folder, 1 = move contents only
    )

    // git add ExternalFolderCpp
    // git commit -m "Add extra project"
    // git push origin master

    (*
    Pridat do fsproj
    <ItemGroup>
	  <None Include="e:\FabulousMAUI\OdisTimetableDownloaderMAUI\x64\Release\CppHelpers.dll">
		  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
         </None>
     </ItemGroup>
    *)