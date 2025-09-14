namespace NativeHelpers

open System
open System.Runtime.InteropServices

module Native =

    // C++ code:

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

    (*
    Pridat do fsproj
    <ItemGroup>
	  <None Include="e:\FabulousMAUI\OdisTimetableDownloaderMAUI\x64\Release\CppHelpers.dll">
		  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
         </None>
     </ItemGroup>
    *)

    // Rust code:

    [<DllImport("string_combine_dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern IntPtr combine_strings(IntPtr s1, IntPtr s2)

    [<DllImport("string_combine_dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void free_string(IntPtr ptr)

    (*
    Pridat do fsproj
    <ItemGroup>
          <None Include="e:\FabulousMAUI\OdisTimetableDownloaderMAUI\x64\Release\string_combine_dll.dll">
		      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
         </None>
     </ItemGroup>
    *)

    // Adjust "rust_copy_move.dll" to match your DLL file name and location
    [<DllImport("rust_copy_move.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern int rust_copy_c(string src, string dst)

    [<DllImport("rust_copy_move.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)>]
    extern int rust_move_c(string src, string dst)

    (*
    Pridat do fsproj
    <ItemGroup>
              <None Include="e:\FabulousMAUI\OdisTimetableDownloaderMAUI\x64\Release\rust_copy_move.dll">
		          <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
         </None>
     </ItemGroup>
    *)