using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Bounded streaming downloader. Only addresses authenticated by the release signature
// are accepted. Cached ranges are untrusted until the complete signed hash passes.
internal static class UpdateDownload {
 const int ChunkSize=1024*1024,Workers=3,ReadTimeout=10000;
 sealed class RangeUnsupported:Exception {}
 sealed class ProgressState {
  readonly object gate=new object();readonly IProgress<double> target;readonly long size;readonly long[] flowing=new long[Workers];long complete;double shown;Stopwatch clock=Stopwatch.StartNew();
  internal ProgressState(IProgress<double> target,long size,long cached){this.target=target;this.size=size;complete=cached;}
  internal void Set(int worker,long bytes,bool done){lock(gate){flowing[worker]=bytes;if(done){complete+=bytes;flowing[worker]=0;}if(target!=null&&(clock.ElapsedMilliseconds>=150||done)){shown=Math.Max(shown,Math.Min(99,(complete+flowing.Sum())*100.0/size));target.Report(shown);clock.Restart();}}}
 }
 static void Delete(string path){try{if(File.Exists(path))File.Delete(path);}catch{}}
 static void Clear(string folder){if(!Directory.Exists(folder))return;foreach(var p in Directory.GetFiles(folder))Delete(p);try{Directory.Delete(folder);}catch{}}
 static HttpWebRequest Request(string url,CancellationToken token){token.ThrowIfCancellationRequested();var r=AppUpdates.Request(url,ReadTimeout,false);r.ServicePoint.ConnectionLimit=Math.Max(r.ServicePoint.ConnectionLimit,Workers);return r;}
 static void CheckRange(HttpWebResponse response,long start,long end,long size){
  if(response.StatusCode==HttpStatusCode.OK)throw new RangeUnsupported();
  var m=Regex.Match(response.Headers["Content-Range"]??"",@"^bytes ([0-9]+)-([0-9]+)/([0-9]+)$",RegexOptions.IgnoreCase);
  if(response.StatusCode!=HttpStatusCode.PartialContent||!m.Success||m.Groups[1].Value!=start.ToString(CultureInfo.InvariantCulture)||m.Groups[2].Value!=end.ToString(CultureInfo.InvariantCulture)||m.Groups[3].Value!=size.ToString(CultureInfo.InvariantCulture)||(response.ContentLength>=0&&response.ContentLength!=end-start+1))throw new InvalidDataException("下载分段信息不符，已停止更新。");
 }
 internal static bool IsRecoverable(Exception e){var w=e as WebException;if(w==null)return e is IOException;var r=w.Response as HttpWebResponse;if(r!=null){int status=(int)r.StatusCode;r.Close();return status==404||status==408||status==410||status==429||status>=500;}return w.Status!=WebExceptionStatus.TrustFailure&&w.Status!=WebExceptionStatus.SecureChannelFailure;}
 static void WaitRetry(Exception error,CancellationToken token){
  int milliseconds=300;var w=error as WebException;var r=w==null?null:w.Response as HttpWebResponse;
  if(r!=null){string value=r.Headers["Retry-After"];int seconds;DateTimeOffset date;if(int.TryParse(value,out seconds))milliseconds=(int)Math.Min(int.MaxValue,Math.Max(0,(long)seconds*1000));else if(DateTimeOffset.TryParse(value,out date))milliseconds=(int)Math.Min(int.MaxValue,Math.Max(0,(date-DateTimeOffset.UtcNow).TotalMilliseconds));}
  if(token.WaitHandle.WaitOne(milliseconds))token.ThrowIfCancellationRequested();
 }
 static string[] Sources(ReleaseInfo release){return new[]{release.installerUrl}.Concat(release.installerUrls??new string[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();}
 internal static string Run(ReleaseInfo release,IProgress<double> progress,CancellationToken caller,string folder,int timeout){
  if(release==null||!AppUpdates.ValidInstallerAddress(release.installerUrl)||(release.installerUrls??new string[0]).Any(s=>!AppUpdates.ValidInstallerAddress(s)))throw new InvalidDataException("更新下载地址无效。");
  Directory.CreateDirectory(folder);string id=release.installerSha256.ToLowerInvariant(),ready=Path.Combine(folder,"AsterHub-"+id+".exe"),partial=Path.Combine(folder,"partial-"+id),lockPath=Path.Combine(folder,id+".lock");
  using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(caller)){
   deadline.CancelAfter(timeout);
   try{
    using(var owned=new FileStream(lockPath,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){
     if(File.Exists(ready)){try{AppUpdates.VerifyInstaller(ready,release);if(progress!=null)progress.Report(100);return ready;}catch(InvalidDataException){Delete(ready);}}
     Directory.CreateDirectory(partial);
     try{
      if(release.installerBytes>=4L*1024*1024){try{DownloadRanges(release,partial,progress,deadline.Token);}catch(RangeUnsupported){DownloadStream(release,partial,progress,deadline.Token);}}
      else DownloadStream(release,partial,progress,deadline.Token);
      string pending=Path.Combine(partial,"assembled.part");deadline.Token.ThrowIfCancellationRequested();AppUpdates.VerifyInstaller(pending,release);File.Move(pending,ready);Clear(partial);if(progress!=null)progress.Report(100);return ready;
     }catch(InvalidDataException){Clear(partial);throw;}
    }
   }catch{if(caller.IsCancellationRequested)throw new OperationCanceledException(caller);if(deadline.IsCancellationRequested)throw new WebException("安装包下载暂时中断，已保留完成的部分，再次下载可继续。",WebExceptionStatus.Timeout);throw;}
   finally{Delete(lockPath);}
  }
 }
 static void DownloadRanges(ReleaseInfo release,string folder,IProgress<double> progress,CancellationToken token){
  string[] sources=Sources(release);int count=(int)((release.installerBytes+ChunkSize-1)/ChunkSize),next=-1;long cached=0;bool[] present=new bool[count];
  for(int i=0;i<count;i++){string path=Path.Combine(folder,i.ToString("D4")+".chunk");long length=Math.Min(ChunkSize,release.installerBytes-(long)i*ChunkSize);present[i]=File.Exists(path)&&new FileInfo(path).Length==length;if(present[i])cached+=length;else Delete(path);}
  var state=new ProgressState(progress,release.installerBytes,cached);
  using(var stopping=CancellationTokenSource.CreateLinkedTokenSource(token)){
   var tasks=Enumerable.Range(0,Workers).Select(worker=>Task.Run(()=>{
    try{for(;;){stopping.Token.ThrowIfCancellationRequested();int index=Interlocked.Increment(ref next);if(index>=count)return;if(present[index])continue;long start=(long)index*ChunkSize,end=Math.Min(release.installerBytes-1,start+ChunkSize-1);string path=Path.Combine(folder,index.ToString("D4")+".chunk");
     for(int attempt=0;;attempt++){
      string pending=path+".pending";Delete(pending);state.Set(worker,0,false);
      try{
       var request=Request(sources[(worker+attempt)%sources.Length],stopping.Token);request.AddRange(start,end);
       using(stopping.Token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse()){
        try{CheckRange(response,start,end,release.installerBytes);using(var input=response.GetResponseStream())using(var output=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
         var buffer=new byte[65536];long received=0;int n;while((n=input.Read(buffer,0,buffer.Length))>0){stopping.Token.ThrowIfCancellationRequested();received+=n;if(received>end-start+1)throw new InvalidDataException("下载大小与发布信息不符。");output.Write(buffer,0,n);state.Set(worker,received,false);}if(received!=end-start+1)throw new WebException("下载分段连接中断。",WebExceptionStatus.ReceiveFailure);
        }}finally{request.Abort();}
       }
       stopping.Token.ThrowIfCancellationRequested();File.Move(pending,path);state.Set(worker,end-start+1,true);break;
      }catch(Exception e){Delete(pending);stopping.Token.ThrowIfCancellationRequested();if(attempt+1>=Math.Max(3,sources.Length)||!IsRecoverable(e))throw;WaitRetry(e,stopping.Token);}
     }
    }}catch{stopping.Cancel();throw;}
   },stopping.Token)).ToArray();
   try{Task.WhenAll(tasks).GetAwaiter().GetResult();}
   catch{token.ThrowIfCancellationRequested();var failures=tasks.Where(t=>t.IsFaulted).SelectMany(t=>t.Exception.Flatten().InnerExceptions).ToArray();Exception failure=failures.FirstOrDefault(e=>e is InvalidDataException)??failures.FirstOrDefault(e=>e is RangeUnsupported)??failures.FirstOrDefault(e=>!(e is OperationCanceledException));if(failure!=null)throw failure;throw;}
  }
  token.ThrowIfCancellationRequested();string assembled=Path.Combine(folder,"assembled.part");Delete(assembled);
  using(var output=new FileStream(assembled,FileMode.CreateNew,FileAccess.Write,FileShare.None))for(int i=0;i<count;i++){token.ThrowIfCancellationRequested();using(var input=File.OpenRead(Path.Combine(folder,i.ToString("D4")+".chunk")))input.CopyTo(output,65536);}
 }
 static void DownloadStream(ReleaseInfo release,string folder,IProgress<double> progress,CancellationToken token){
  string pending=Path.Combine(folder,"assembled.part");string[] sources=Sources(release);
  for(int attempt=0;;attempt++){
   var request=Request(sources[attempt%sources.Length],token);long existing=File.Exists(pending)?new FileInfo(pending).Length:0;if(existing==release.installerBytes)return;if(existing>release.installerBytes){Delete(pending);existing=0;}if(existing>0)request.AddRange(existing,release.installerBytes-1);
   try{
    using(token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse()){
     if(response.StatusCode==HttpStatusCode.OK)existing=0;else CheckRange(response,existing,release.installerBytes-1,release.installerBytes);
     using(var input=response.GetResponseStream())using(var output=new FileStream(pending,existing==0?FileMode.Create:FileMode.Append,FileAccess.Write,FileShare.None)){
      long total=existing;int n;var buffer=new byte[65536];var clock=Stopwatch.StartNew();while((n=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=n;if(total>release.installerBytes)throw new InvalidDataException("下载大小与发布信息不符。");output.Write(buffer,0,n);if(progress!=null&&clock.ElapsedMilliseconds>=150){progress.Report(Math.Min(99,total*100.0/release.installerBytes));clock.Restart();}}if(total!=release.installerBytes)throw new WebException("安装包连接中断。",WebExceptionStatus.ReceiveFailure);
     }
    }return;
   }catch(Exception e){token.ThrowIfCancellationRequested();if(attempt+1>=Math.Max(3,sources.Length)||!IsRecoverable(e))throw;WaitRetry(e,token);}
   finally{request.Abort();}
  }
 }
}
