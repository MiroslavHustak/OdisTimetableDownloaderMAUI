namespace JavaInteroperabilityCode

// nechej reference, jak jsou, intellisense jen halucinuje
open System
open System.Net.Http
open System.Threading

#if ANDROID

open Android.OS
open Android.App
open Android.Net
open Android.Views
open Android.Content
open Android.Runtime
open Android.Provider 

open Xamarin
open Xamarin.Essentials

//****************************** JAVA INTEROPERABILITY CODE ***********************************

open Java.Interop
open Javax.Net.Ssl

// Java Interoperability Code for Custom SSL/TLS Handling on Android
// For testing unsafe code only ! Not to be used in production !

type TrustAllHostnameVerifier() =

    inherit Java.Lang.Object() 

    interface IHostnameVerifier with
        member _.Verify(hostname : string, session : Javax.Net.Ssl.ISSLSession) = true

type TrustAllCertsManager() =

    inherit Java.Lang.Object() 

    interface IX509TrustManager with
        member _.GetAcceptedIssuers() = null
        member _.CheckClientTrusted(chain, authType) = ()
        member _.CheckServerTrusted(chain, authType) = ()

// Custom HttpClientHandler for bypassing SSL on Android
type UnsafeAndroidClientHandler() =

    inherit HttpClientHandler()

    do
        let trustAllCerts = [| new TrustAllCertsManager() :> ITrustManager |]

        let sslContext = SSLContext.GetInstance("TLS")
        sslContext.Init(null, trustAllCerts, new Java.Security.SecureRandom())

        HttpsURLConnection.DefaultSSLSocketFactory <- sslContext.SocketFactory
        HttpsURLConnection.DefaultHostnameVerifier <- new TrustAllHostnameVerifier()

#endif