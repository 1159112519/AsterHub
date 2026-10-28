using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
public sealed class ReleaseEnvelope { public string payload{get;set;} public string signature{get;set;} }
public sealed class ReleaseInfo {
 public string appId{get;set;} public string version{get;set;} public string publishedUtc{get;set;} public string notes{get;set;} public string installerUrl{get;set;} public string installerSha256{get;set;} public long installerBytes{get;set;}
 [ScriptIgnore] public string ManifestEndpoint {get;internal set;}
 public Version ParsedVersion {get{var value=new Version(version);return new Version(value.Major,value.Minor,Math.Max(0,value.Build),Math.Max(0,value.Revision));}}
}
public static class AppUpdates {
 public const string Website="http://8.140.49.4/aqserver/";
 public const string Endpoint="https://raw.githubusercontent.com/1159112519/aq-server/main/website/public/releases/stable.json";
 public static Version CurrentVersion{get{return Assembly.GetExecutingAssembly().GetName().Version;}}
 public static string DisplayVersion{get{return CurrentVersion.Major+"."+CurrentVersion.Minor+"."+CurrentVersion.Build;}}
 public static ReleaseInfo VerifyManifest(byte[] envelopeBytes,string publicKey){
  if(envelopeBytes==null||envelopeBytes.Length>65536)throw new InvalidDataException("更新信息过大。");
  var serializer=new JavaScriptSerializer{MaxJsonLength=65536};ReleaseEnvelope envelope;
  try{envelope=serializer.Deserialize<ReleaseEnvelope>(Encoding.UTF8.GetString(envelopeBytes));}catch{throw new InvalidDataException("更新信息格式无效。");}
  byte[] payload,signature;try{payload=Convert.FromBase64String(envelope.payload);signature=Convert.FromBase64String(envelope.signature);}catch{throw new InvalidDataException("更新信息缺少有效签名。");}
  using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKey);if(!rsa.VerifyData(payload,CryptoConfig.MapNameToOID("SHA256"),signature))throw new InvalidDataException("更新签名校验失败，已停止更新。");}
  ReleaseInfo release;try{release=serializer.Deserialize<ReleaseInfo>(Encoding.UTF8.GetString(payload));}catch{throw new InvalidDataException("版本信息格式无效。");}
  Version version;Uri uri;DateTime published;
  if(release==null||release.appId!="AQServer"||!Version.TryParse(release.version,out version)||version.Major<0||release.installerBytes<1024||release.installerBytes>512L*1024*1024||!System.Text.RegularExpressions.Regex.IsMatch(release.installerSha256??"", "^[a-fA-F0-9]{64}$")||!Uri.TryCreate(release.installerUrl,UriKind.Absolute,out uri)||(uri.Scheme!="http"&&uri.Scheme!="https")||!string.IsNullOrEmpty(uri.UserInfo)||!uri.AbsolutePath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!DateTime.TryParse(release.publishedUtc,System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out published))throw new InvalidDataException("更新信息不完整或不受支持。");
  return release;
 }
 static HttpWebRequest Request(string url){var request=(HttpWebRequest)WebRequest.Create(url);request.Timeout=7000;request.ReadWriteTimeout=12000;request.UserAgent="AQServer/"+DisplayVersion;request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);return request;}
 public static Task<ReleaseInfo> Check(CancellationToken token){return CheckAt(UpdateSources.CurrentEndpoint,UpdatePublicKey.Xml,token);}
 internal static Task<ReleaseInfo> CheckAt(string endpoint,string publicKey,CancellationToken token){return Task.Run(()=>{
  ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
  using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){
   deadline.CancelAfter(10000);
   var request=Request(endpoint);
   try{
    using(deadline.Token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse())using(var input=response.GetResponseStream())using(var output=new MemoryStream()){
     var buffer=new byte[8192];int count;
     while((count=input.Read(buffer,0,buffer.Length))>0){deadline.Token.ThrowIfCancellationRequested();if(output.Length+count>65536)throw new InvalidDataException("更新信息过大。");output.Write(buffer,0,count);}
     deadline.Token.ThrowIfCancellationRequested();
     var release=VerifyManifest(output.ToArray(),publicKey);
     release.ManifestEndpoint=endpoint;
     return release;
    }
   }catch(Exception){
    if(token.IsCancellationRequested)throw new OperationCanceledException(token);
    if(deadline.IsCancellationRequested)throw new WebException("Update service timed out",WebExceptionStatus.Timeout);
    throw;
   }
  }
 },token);}
 // Installer URL is covered by the publisher signature; never rebase it to another origin.
 internal static string DownloadAddress(ReleaseInfo release){return release.installerUrl;}
 public static void VerifyInstaller(string path,ReleaseInfo release){var file=new FileInfo(path);if(!file.Exists||file.Length!=release.installerBytes)throw new InvalidDataException("安装包大小校验失败。");string hash;using(var input=File.OpenRead(path))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");if(!string.Equals(hash,release.installerSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("安装包校验失败，请重新下载。");}
 public static Task<string> Download(ReleaseInfo release,IProgress<double> progress,CancellationToken token){return DownloadTo(release,progress,token,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AQServer","Updates"));}
 internal static Task<string> DownloadTo(ReleaseInfo release,IProgress<double> progress,CancellationToken token,string folder){return Task.Run(()=>{
  Directory.CreateDirectory(folder);
  string pending=Path.Combine(folder,"download-"+Guid.NewGuid().ToString("N")+".part"),ready=Path.ChangeExtension(pending,".exe");
  try{var request=Request(DownloadAddress(release));using(token.Register(()=>request.Abort()))using(var response=(HttpWebResponse)request.GetResponse())using(var input=response.GetResponseStream())using(var output=new FileStream(pending,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
   var buffer=new byte[65536];int count;long total=0;var throttle=Stopwatch.StartNew();while((count=input.Read(buffer,0,buffer.Length))>0){token.ThrowIfCancellationRequested();total+=count;if(total>release.installerBytes)throw new InvalidDataException("下载大小与发布信息不符。");output.Write(buffer,0,count);if(progress!=null&&throttle.ElapsedMilliseconds>120){progress.Report(total*100.0/release.installerBytes);throttle.Restart();}}
  }token.ThrowIfCancellationRequested();VerifyInstaller(pending,release);File.Move(pending,ready);if(progress!=null)progress.Report(100);return ready;
  }catch{try{File.Delete(pending);}catch{}if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw;}
 },token);}
 public static string FriendlyError(Exception error){
  if(error is OperationCanceledException)return "已取消，可以重新检查或下载。";
  if(error is InvalidDataException||error is ArgumentException)return error.Message;
  var network=error as WebException;
  if(network!=null){
   var response=network.Response as HttpWebResponse;
   if(response!=null&&response.StatusCode==HttpStatusCode.NotFound)return "官方更新信息暂不可用（404），请稍后重试。";
   if(network.Status==WebExceptionStatus.Timeout)return "连接官方更新服务超时，请检查网络后重试。";
   if(network.Status==WebExceptionStatus.NameResolutionFailure)return "无法解析官方更新服务域名，请检查网络。";
   if(network.Status==WebExceptionStatus.TrustFailure||network.Status==WebExceptionStatus.SecureChannelFailure)return "更新服务的安全连接失败，请检查系统时间和证书。";
   return "无法连接官方更新服务，请检查网络后重试。";
  }
  if(error is UnauthorizedAccessException||error is IOException)return "无法保存安装包，请检查磁盘空间和目录权限。";
  return "检查更新未完成，请稍后重试。";
 }
}
