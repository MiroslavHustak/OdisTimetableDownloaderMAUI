namespace OdisTimetableDownloaderMAUI

#if ANDROID
open Android.Content
open Android.Provider

open Types.Haskell_IO_Monad_Simulation

module FileLauncher =
      
    let internal openStorageRoot (context : Context) =
        
        IO (fun ()
                ->
                use intent = new Intent(Intent.ActionOpenDocumentTree)
    
                let uri =
                    DocumentsContract.BuildRootUri(
                        "com.android.externalstorage.documents",
                        "primary"
                    )

                (*
                /storage/emulated/0/MyApp/DownloadsA

                let uri =
                    DocumentsContract.BuildDocumentUri(
                        "com.android.externalstorage.documents",
                        "primary:MyApp/DownloadsA"
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