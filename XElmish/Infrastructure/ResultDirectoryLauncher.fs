namespace OdisTimetableDownloaderMAUI

#if ANDROID
open Android.Content
open Android.Provider

open Types.Haskell_IO_Monad_Simulation

module FileLauncher =
      
    let internal openStorageRoot (context : Context) fabulousTimetablesFolder =
        
        IO (fun ()
                ->
                use intent = new Intent(Intent.ActionOpenDocumentTree)
                
                let uriPart = sprintf "primary:%s" fabulousTimetablesFolder

                let uri =
                    DocumentsContract.BuildRootUri(
                        "com.android.externalstorage.documents",
                        uriPart
                    )

                (*
                /storage/emulated/0/MyApp/DownloadsA
                @"/storage/emulated/0/FabulousTimetables/"

                let uri =
                    DocumentsContract.BuildDocumentUri(
                        "com.android.externalstorage.documents",
                        "primary:FabulousTimetables"
                    )
                *) 
    
                intent.PutExtra(
                    DocumentsContract.ExtraInitialUri,
                    uri
                )
                |> ignore<Intent>
    
                intent.AddFlags(
                    ActivityFlags.GrantReadUriPermission
                    ||| ActivityFlags.GrantWriteUriPermission
                    ||| ActivityFlags.NewTask
                )
                |> ignore<Intent>
    
                context.StartActivity intent
        )
#endif