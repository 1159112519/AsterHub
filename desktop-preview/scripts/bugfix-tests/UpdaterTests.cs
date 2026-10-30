using System;using System.IO;using System.Net;using System.Text;using System.Diagnostics;using System.Threading;using System.Threading.Tasks;using System.Security.Cryptography;using System.Web.Script.Serialization;using System.Reflection;
[assembly:AssemblyInformationalVersion("0.3.0-preview.6")]
public static class Storage {public static string PathName=Path.Combine(Path.GetTempPath(),"asterhub-updater-tests");}
class UpdaterTests {
 static int failures;static RSACryptoServiceProvider rsa=new RSACryptoServiceProvider(2048);static byte[] valid;static byte[] installer=new byte[2048];static string root;static HttpListener server;
 static byte[] Envelope(string version,string path=null,string sha=null,long size=2048){var j=new JavaScriptSerializer();var p=Encoding.UTF8.GetBytes(j.Serialize(new{appId="AsterHubPreview",version=version,publishedUtc="2026-10-30T00:00:00Z",notes="test",installerUrl=root+(path??"installer.exe"),installerSha256=sha??new string('a',64),installerBytes=size}));return Encoding.UTF8.GetBytes(j.Serialize(new{payload=Convert.ToBase64String(p),signature=Convert.ToBase64String(rsa.SignData(p,CryptoConfig.MapNameToOID("SHA256")))}));}
 static void Assert(bool value,string name){Console.WriteLine((value?"PASS ":"FAIL ")+name);if(!value)failures++;}
 static void Rejects(Action action,string name){try{action();Assert(false,name);}catch(InvalidDataException){Assert(true,name);}}
 static ReleaseInfo Candidates(string[] endpoints,int attempt,int total,CancellationToken token){
#if BASELINE
 return AppUpdates.CheckAt(endpoints[0],rsa.ToXmlString(false),token).GetAwaiter().GetResult();
#else
 return AppUpdates.CheckCandidatesAt(endpoints,rsa.ToXmlString(false),token,attempt,total).GetAwaiter().GetResult();
#endif
 }
 static void Main(){var socket=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();root="http://127.0.0.1:"+port+"/";valid=Envelope("0.3.0-preview.7");server=new HttpListener();server.Prefixes.Add(root);server.Start();Task.Run(()=>{while(server.IsListening){try{var c=server.GetContext();Task.Run(()=>Serve(c));}catch{break;}}});
 try{
 var preferencesRoot=Path.Combine(Path.GetTempPath(),"asterhub-update-settings-"+Guid.NewGuid().ToString("N"));
 Assert(UpdatePreferences.Load(preferencesRoot).checkOnStartup,"existing users default to startup checking");
 new UpdatePreferences{checkOnStartup=false}.Save(preferencesRoot);Assert(!UpdatePreferences.Load(preferencesRoot).checkOnStartup,"disabled startup preference survives reload");
 new UpdatePreferences{checkOnStartup=true}.Save(preferencesRoot);Assert(UpdatePreferences.Load(preferencesRoot).checkOnStartup,"enabled startup preference survives atomic replacement");
 foreach(var f in Directory.GetFiles(preferencesRoot))File.Delete(f);Directory.Delete(preferencesRoot);
 var once=new StartupUpdatePolicy();Assert(once.TryBegin(true)&&!once.TryBegin(true),"launch check runs once");var disabled=new StartupUpdatePolicy();Assert(!disabled.TryBegin(false)&&!disabled.TryBegin(true),"disabled launch does not check later in same launch");
 Assert(StartupUpdatePolicy.ShouldOffer(AppUpdates.VerifyManifest(valid,rsa.ToXmlString(false)),"0.3.0-preview.6"),"only a newer verified release is offered");
 Assert(!StartupUpdatePolicy.ShouldOffer(AppUpdates.VerifyManifest(valid,rsa.ToXmlString(false)),"0.3.0-preview.7"),"same version stays silent");
 Rejects(()=>AppUpdates.VerifyManifest(Envelope("0.3.0-preview.broken"),rsa.ToXmlString(false)),"reject signed invalid prerelease before reporting a release");
 Rejects(()=>AppUpdates.VerifyManifest(Envelope("0.3.0-preview.7-extra"),rsa.ToXmlString(false)),"reject ambiguous extra prerelease suffix");
 Rejects(()=>AppUpdates.VerifyManifest(Encoding.UTF8.GetBytes("{}"),rsa.ToXmlString(false)),"reject missing signature");
 Rejects(()=>AppUpdates.VerifyManifest(valid,new RSACryptoServiceProvider(2048).ToXmlString(false)),"reject wrong publisher signature");
 try{var r=Candidates(new[]{root+"offline",root+"valid"},300,1200,CancellationToken.None);Assert(r.version=="0.3.0-preview.7","official fallback recovers primary HTTP failure");}catch(Exception e){Console.WriteLine(e.GetType().Name);Assert(false,"official fallback recovers primary HTTP failure");}
 var timer=Stopwatch.StartNew();try{var r=Candidates(new[]{root+"timeout",root+"valid"},200,1400,CancellationToken.None);Assert(r.version=="0.3.0-preview.7"&&timer.ElapsedMilliseconds<1200,"primary deadline leaves time for fallback");}catch{Assert(false,"primary deadline leaves time for fallback");}
 try{Candidates(new[]{root+"invalid",root+"valid"},300,1000,CancellationToken.None);Assert(false,"invalid signature cannot become success via fallback");}catch(InvalidDataException){Assert(true,"invalid signature cannot become success via fallback");}catch{Assert(false,"invalid signature cannot become success via fallback");}
 timer.Restart();using(var cancel=new CancellationTokenSource(120)){try{Candidates(new[]{root+"timeout",root+"valid"},800,1500,cancel.Token);Assert(false,"caller cancellation propagates");}catch(OperationCanceledException){Assert(timer.ElapsedMilliseconds<1000,"caller cancellation propagates");}catch{Assert(false,"caller cancellation propagates");}}
 timer.Restart();try{Candidates(new[]{root+"timeout",root+"timeout"},700,250,CancellationToken.None);Assert(false,"total deadline bounds all attempts");}catch(WebException e){Assert(e.Status==WebExceptionStatus.Timeout&&timer.ElapsedMilliseconds<1100,"total deadline bounds all attempts");}catch{Assert(false,"total deadline bounds all attempts");}
 var downloadFolder=Path.Combine(Path.GetTempPath(),"asterhub-update-download-"+Guid.NewGuid().ToString("N"));
 string hash;using(var digest=SHA256.Create())hash=BitConverter.ToString(digest.ComputeHash(installer)).Replace("-","");
 var downloadRelease=AppUpdates.VerifyManifest(Envelope("0.3.0-preview.7","installer.exe",hash),rsa.ToXmlString(false));
 try{
  var ready=AppUpdates.DownloadTo(downloadRelease,null,CancellationToken.None,downloadFolder).GetAwaiter().GetResult();AppUpdates.VerifyInstaller(ready,downloadRelease);Assert(File.Exists(ready)&&Directory.GetFiles(downloadFolder,"*.part").Length==0,"real HTTP download verifies hash before promotion");File.Delete(ready);
  var wrong=AppUpdates.VerifyManifest(Envelope("0.3.0-preview.7","installer.exe",new string('a',64)),rsa.ToXmlString(false));Rejects(()=>AppUpdates.DownloadTo(wrong,null,CancellationToken.None,downloadFolder).GetAwaiter().GetResult(),"bad download hash is rejected");Assert(Directory.GetFiles(downloadFolder).Length==0,"failed download leaves no partial or executable");
#if !BASELINE
  var slow=AppUpdates.VerifyManifest(Envelope("0.3.0-preview.7","timeout.exe",hash),rsa.ToXmlString(false));timer.Restart();try{AppUpdates.DownloadTo(slow,null,CancellationToken.None,downloadFolder,180).GetAwaiter().GetResult();Assert(false,"download deadline aborts stalled headers");}catch(WebException e){Assert(e.Status==WebExceptionStatus.Timeout&&timer.ElapsedMilliseconds<1000,"download deadline aborts stalled headers");}
#endif
 }finally{foreach(var f in Directory.GetFiles(downloadFolder))File.Delete(f);Directory.Delete(downloadFolder);}
 var file=Path.GetTempFileName();try{File.WriteAllBytes(file,new byte[2048]);var release=AppUpdates.VerifyManifest(valid,rsa.ToXmlString(false));Rejects(()=>AppUpdates.VerifyInstaller(file,release),"installer hash mismatch is rejected");}finally{File.Delete(file);}
 }finally{server.Stop();server.Close();rsa.Dispose();}Console.WriteLine("FAILURES="+failures);Environment.ExitCode=failures==0?0:1;}
 static void Serve(HttpListenerContext c){try{var path=c.Request.Url.AbsolutePath;if(path.StartsWith("/timeout"))Thread.Sleep(2200);if(path=="/offline"){c.Response.StatusCode=503;c.Response.Close();return;}var bytes=path=="/installer.exe"?installer:path=="/invalid"?Encoding.UTF8.GetBytes("{}"):valid;c.Response.ContentLength64=bytes.Length;c.Response.OutputStream.Write(bytes,0,bytes.Length);c.Response.Close();}catch{}}
}


