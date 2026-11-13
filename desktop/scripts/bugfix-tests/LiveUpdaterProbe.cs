using System;using System.IO;using System.Diagnostics;using System.Threading;using System.Reflection;using System.Net;
[assembly:AssemblyInformationalVersion("0.3.0-preview.6")]
public static class Storage {public static string PathName=Path.Combine(Path.GetTempPath(),"asterhub-live-updater");}
class LiveUpdaterProbe {
 static void Main(string[] args){var timer=Stopwatch.StartNew();ReleaseInfo release=null;
 Console.WriteLine("utc="+DateTime.UtcNow.ToString("o"));Console.WriteLine("proxyConfigured="+(WebRequest.DefaultWebProxy!=null));
 foreach(var endpoint in new[]{AppUpdates.Endpoint,UpdateSources.ManifestEndpoints[1]}){timer.Restart();try{release=AppUpdates.CheckAt(endpoint,UpdatePublicKey.Xml,CancellationToken.None).GetAwaiter().GetResult();Console.WriteLine("manifest host="+new Uri(endpoint).Host+" signed=true version="+release.version+" elapsedMs="+timer.ElapsedMilliseconds);}catch(Exception e){Console.WriteLine("manifest host="+new Uri(endpoint).Host+" failed="+e.GetType().Name+" message="+AppUpdates.FriendlyError(e)+" elapsedMs="+timer.ElapsedMilliseconds);}}
 timer.Restart();try{release=AppUpdates.Check(CancellationToken.None).GetAwaiter().GetResult();Console.WriteLine("appCheck signed=true version="+release.version+" endpoint="+new Uri(release.ManifestEndpoint).Host+" elapsedMs="+timer.ElapsedMilliseconds);}catch(Exception e){Console.WriteLine("appCheck failed="+e.GetType().Name+" message="+AppUpdates.FriendlyError(e)+" elapsedMs="+timer.ElapsedMilliseconds);Environment.ExitCode=1;return;}
 if(args.Length>0){timer.Restart();try{var path=AppUpdates.DownloadTo(release,null,CancellationToken.None,args[0]).GetAwaiter().GetResult();AppUpdates.VerifyInstaller(path,release);Console.WriteLine("download bytes="+release.installerBytes+" hashVerified=true elapsedMs="+timer.ElapsedMilliseconds+" path="+path);Console.WriteLine("installerExecuted=false");}catch(Exception e){Console.WriteLine("download failed="+e.GetType().Name+" message="+AppUpdates.FriendlyError(e)+" elapsedMs="+timer.ElapsedMilliseconds);Environment.ExitCode=1;}}
 }
}
