using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
internal static class DownloadCancellationScenario {
 internal static bool Run(){
  var bytes=new byte[5*1024*1024+123];new Random(23).NextBytes(bytes);bytes[0]=77;bytes[1]=90;
  string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
  var probe=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);probe.Start();int port=((System.Net.IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();string address="http://127.0.0.1:"+port+"/";
  var server=new HttpListener();server.Prefixes.Add(address);server.Start();var unblock=new ManualResetEventSlim();int paused=1,firstRequests=0;
  var serving=Task.Run(async()=>{while(server.IsListening){HttpListenerContext c;try{c=await server.GetContextAsync();}catch{return;}Task.Run(()=>{try{var match=Regex.Match(c.Request.Headers["Range"]??"",@"bytes=([0-9]+)-([0-9]+)");long start=match.Success?long.Parse(match.Groups[1].Value):0,end=match.Success?long.Parse(match.Groups[2].Value):bytes.Length-1;if(start==0)Interlocked.Increment(ref firstRequests);c.Response.StatusCode=206;c.Response.ContentLength64=end-start+1;c.Response.Headers["Content-Range"]="bytes "+start+"-"+end+"/"+bytes.Length;if(start>0&&Volatile.Read(ref paused)==1){c.Response.OutputStream.Write(bytes,(int)start,65536);c.Response.OutputStream.Flush();unblock.Wait(5000);start+=65536;}c.Response.OutputStream.Write(bytes,(int)start,(int)(end-start+1));c.Response.Close();}catch{try{c.Response.Abort();}catch{}}});}});
  var root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cancel-resume-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
  var release=new ReleaseInfo{version="0.3.0-preview.99",installerUrl=address+"fixture.exe",installerSha256=hash,installerBytes=bytes.Length};
  try{
   using(var cancel=new CancellationTokenSource()){
    var downloading=AppUpdates.DownloadTo(release,null,cancel.Token,root,15000);string partial=Path.Combine(root,"partial-"+hash.ToLowerInvariant());
    if(!SpinWait.SpinUntil(()=>Directory.Exists(partial)&&Directory.GetFiles(partial,"*.chunk").Length>0,5000))throw new Exception("fixture did not complete first range");
    cancel.Cancel();try{downloading.GetAwaiter().GetResult();throw new Exception("cancelled HTTP transfer succeeded");}catch(OperationCanceledException){}
    if(Directory.GetFiles(root,"*.exe").Length!=0||Directory.GetFiles(partial,"*.chunk").Length!=1||Directory.GetFiles(partial,"*.pending").Length!=0)throw new Exception("cancel did not retain only completed ranges");
    Volatile.Write(ref paused,0);unblock.Set();var path=AppUpdates.DownloadTo(release,null,CancellationToken.None,root,15000).GetAwaiter().GetResult();AppUpdates.VerifyInstaller(path,release);
    return firstRequests==1&&!Directory.Exists(partial)&&File.Exists(path);
   }
  }finally{unblock.Set();server.Stop();server.Close();if(!Path.GetFullPath(root).StartsWith(AppDomain.CurrentDomain.BaseDirectory,StringComparison.OrdinalIgnoreCase)||!Path.GetFileName(root).StartsWith("cancel-resume-",StringComparison.Ordinal))throw new Exception("unsafe fixture cleanup");Directory.Delete(root,true);}
 }
}
