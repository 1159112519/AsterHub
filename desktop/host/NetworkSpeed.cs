using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class WorkbenchWindow {
 readonly object speedGate=new object();
 SpeedRun speedRun;
 sealed class SpeedRun {
  internal readonly string Id=Guid.NewGuid().ToString("N");
  internal readonly CancellationTokenSource Stop=new CancellationTokenSource();
  internal long Used,Limit;
  internal int Requests,Transfers,Udp;
  internal DateTime Created=DateTime.UtcNow;
 }
 static readonly Dictionary<string,string> SpeedOrigins=new Dictionary<string,string>{
  {"cf","https://speed.cloudflare.com"},{"la","https://la.speedtest.clouvider.net"},
  {"ams","https://ams.speedtest.clouvider.net"},{"den","https://denspeed.sharktech.net"}
 };
 static string SpeedText(Dictionary<string,object> p,string key){object value;return p.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):"";}
 static int SpeedInt(Dictionary<string,object> p,string key,int min,int max){int n;if(!int.TryParse(SpeedText(p,key),out n)||n<min||n>max)throw new ArgumentException("测速参数无效");return n;}
 async Task<object> NetworkSpeedRequest(string method,Dictionary<string,object> p){
  if(SpeedText(p,"deviceId")!="local")throw new InvalidOperationException("当前仅支持本机测速");
  SpeedRun run;
  lock(speedGate){
   if(method=="networkSpeed.start"){
    if(speedRun!=null){if((DateTime.UtcNow-speedRun.Created).TotalMinutes<3)throw new InvalidOperationException("本机正在测速");speedRun.Stop.Cancel();}
    ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
    int cap=SpeedInt(p,"cap",0,2048);speedRun=new SpeedRun{Limit=cap==0?long.MaxValue:(long)cap*1048576-65536};return new{id=speedRun.Id};
   }
   run=speedRun;
   if(run==null||run.Id!=SpeedText(p,"id")){if(method=="networkSpeed.cancel")return false;throw new InvalidOperationException("测速已结束");}
   if(method=="networkSpeed.cancel"){speedRun=null;run.Stop.Cancel();return true;}
   if(method=="networkSpeed.udp"){if(run.Udp++!=0)throw new InvalidOperationException("本次测速已发起 UDP 探测");}
   else if(++run.Requests>180)throw new InvalidOperationException("测速请求已达到上限");
  }
  if(method=="networkSpeed.udp")return await UdpSpeedProbe(run.Stop.Token);
  string origin,node=SpeedText(p,"node"),direction=SpeedText(p,"direction");
  if(!SpeedOrigins.TryGetValue(node,out origin)||(direction!="latency"&&direction!="down"&&direction!="up"))throw new ArgumentException("测速节点或方向无效");
  int size=SpeedInt(p,"size",direction=="latency"?0:1,direction=="latency"?0:16*1048576),timeout=SpeedInt(p,"timeout",1,8500);
  if(node!="cf"&&direction=="down"&&size%1048576!=0)throw new ArgumentException("下载额度不适用于该节点");
  if(size>0)lock(speedGate){if(run.Stop.IsCancellationRequested||run.Used+size>run.Limit||run.Transfers!=0)throw new InvalidOperationException("测速传输额度不足或正在传输");run.Used+=size;run.Transfers++;}
  try{
   string path=node=="cf"?(direction=="up"?"/__up?bytes=":"/__down?bytes=")+size:
    direction=="down"?"/backend/garbage.php?cors=true&ckSize="+(size/1048576)+"&r="+Guid.NewGuid().ToString("N"):"/backend/empty.php?cors=true&r="+Guid.NewGuid().ToString("N");
   using(var cts=CancellationTokenSource.CreateLinkedTokenSource(run.Stop.Token)){
    cts.CancelAfter(timeout);
    var req=(HttpWebRequest)WebRequest.Create(origin+path);req.AllowAutoRedirect=false;req.Method=direction=="up"?"POST":"GET";req.Credentials=null;req.AllowWriteStreamBuffering=false;req.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);req.UserAgent="AsterHub network speed test";req.ServicePoint.Expect100Continue=false;
    var watch=Stopwatch.StartNew();
    using(cts.Token.Register(req.Abort))try{
     if(direction=="up"){req.ContentLength=size;req.ContentType="application/octet-stream";using(var upload=await req.GetRequestStreamAsync()){var data=new byte[Math.Min(size,65536)];using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(data);for(int sent=0;sent<size;){int count=Math.Min(data.Length,size-sent);await upload.WriteAsync(data,0,count,cts.Token);sent+=count;}}}
     using(var res=(HttpWebResponse)await req.GetResponseAsync()){
      double headers=watch.Elapsed.TotalMilliseconds;int status=(int)res.StatusCode;if(status<200||status>=300)throw new IOException("测速节点 HTTP "+status);
      if(direction=="down"){
       if(res.ContentLength>=0&&res.ContentLength!=size)throw new IOException("下载长度与预留流量不符");
       using(var body=res.GetResponseStream()){if(body==null)throw new IOException("下载没有数据");byte[] buffer=new byte[32768];int total=0;while(total<size){int n=await body.ReadAsync(buffer,0,Math.Min(buffer.Length,size-total),cts.Token);if(n==0)throw new IOException("下载提前结束");total+=n;}}
      }
      cts.Token.ThrowIfCancellationRequested();return new{ok=true,ms=watch.Elapsed.TotalMilliseconds,headersMs=headers,colo=res.Headers["cf-meta-colo"]};
     }
    }catch(Exception error){if(run.Stop.IsCancellationRequested)throw new OperationCanceledException();var webError=error as WebException;var response=webError==null?null:webError.Response as HttpWebResponse;int? status=response==null?(int?)null:(int)response.StatusCode;if(response!=null)response.Close();return new{ok=false,reason=cts.IsCancellationRequested?"timeout":status.HasValue?"http":webError!=null?"connection":"response",status,diagnostic=WorkbenchHost.SmokeRoot==null?null:webError!=null?webError.Status.ToString():error.GetType().Name};}
   }
  }finally{if(size>0)lock(speedGate)run.Transfers--;}
 }
 static async Task<object> UdpSpeedProbe(CancellationToken stop){
  const int count=12;
  foreach(string resolver in new[]{"1.1.1.1","8.8.8.8"}){
   if(!await UdpDns(resolver,stop))continue;
   int received=0;
   for(int i=0;i<count;i+=3){var tests=await Task.WhenAll(UdpDns(resolver,stop),UdpDns(resolver,stop),UdpDns(resolver,stop));foreach(bool reply in tests)if(reply)received++;}
   return new{available=true,sent=count,received,value=(count-received)*100.0/count,resolver};
  }
  return new{available=false};
 }
 static async Task<bool> UdpDns(string resolver,CancellationToken stop){
  stop.ThrowIfCancellationRequested();using(var socket=new UdpClient(AddressFamily.InterNetwork)){
   var query=new byte[]{0,0,1,0,0,1,0,0,0,0,0,0,7,101,120,97,109,112,108,101,3,99,111,109,0,0,1,0,1};
   var id=Guid.NewGuid().ToByteArray();query[0]=id[0];query[1]=id[1];
   using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop)){
    timeout.CancelAfter(750);using(timeout.Token.Register(socket.Close))try{
     socket.Connect(IPAddress.Parse(resolver),53);await socket.SendAsync(query,query.Length);var reply=(await socket.ReceiveAsync()).Buffer;
     return reply.Length>=12&&reply[0]==query[0]&&reply[1]==query[1]&&(reply[2]&128)!=0;
    }catch(SocketException){stop.ThrowIfCancellationRequested();return false;}catch(ObjectDisposedException){stop.ThrowIfCancellationRequested();return false;}
   }
  }
 }
}
