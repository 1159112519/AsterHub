using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class WorkbenchWindow {
 public sealed class RecorderPoint {public string t{get;set;}public double? cpu{get;set;}public double? memory{get;set;}public double? gpu{get;set;}public int interval{get;set;}public bool gapBefore{get;set;}}
 public sealed class RecorderResult {public int schema{get;set;}public int total{get;set;}public RecorderPoint[] points{get;set;}}
 readonly Dictionary<string,DateTime> recorderRetry=new Dictionary<string,DateTime>();
 readonly Dictionary<string,Tuple<DateTime,string>> recorderCache=new Dictionary<string,Tuple<DateTime,string>>();
 string RunRecorder(Server server,string action,long start=0,long end=0,long ticks=0){
  return RemoteRecorderTransport.Run(server,action,start,end,ticks);
 }
 async Task<object> RecorderStatus(Dictionary<string,object> p){var server=Requested(p).Copy();if(server.IsLocal)return new{installed=false,healthy=false,platform="windows"};return await Task.Run<object>(()=>{try{return json.DeserializeObject(RunRecorder(server,"status"));}catch{return new{installed=false,healthy=false,unreachable=true};}});}
 List<ArchivePoint> RecorderHistory(Server server,List<ArchivePoint> local,DateTime from,DateTime to,CancellationToken cancel){
  if(server.IsLocal)return local;string id=PreviewDevices.Id(server);DateTime retry;lock(recorderRetry)if(recorderRetry.TryGetValue(id,out retry)&&retry>DateTime.UtcNow)return local;
  try{
   cancel.ThrowIfCancellationRequested();string cacheKey=id+":"+from.ToString("yyyyMMddHHmm");string output;Tuple<DateTime,string> cache;
   lock(recorderCache)recorderCache.TryGetValue(cacheKey,out cache);
   if(cache!=null&&(DateTime.UtcNow-cache.Item1).TotalSeconds<15)output=cache.Item2;
   else{output=RunRecorder(server,"query",from.Ticks,to.Ticks);cancel.ThrowIfCancellationRequested();lock(recorderCache){if(recorderCache.Count>=12)recorderCache.Clear();recorderCache[cacheKey]=Tuple.Create(DateTime.UtcNow,output);}}
   var rows=json.Deserialize<RecorderResult>(output);if(rows==null||rows.schema!=1||rows.points==null)return local;
   var remote=rows.points.Select(p=>new ArchivePoint{RemoteSummary=true,GapBefore=p.gapBefore,T=long.Parse(p.t,CultureInfo.InvariantCulture),Cpu=p.cpu??double.NaN,Memory=p.memory??double.NaN,Gpu=p.gpu??double.NaN,Interval=p.interval,File="asterhub-agent:"+id}).Where(p=>p.T>=from.Ticks&&p.T<=to.Ticks).OrderBy(p=>p.T).ToList();
   if(remote.Count==0)return local;
   // Agent records are the authoritative source in their acquisition interval;
   // retain local samples in actual agent gaps, not overlapping duplicate traces.
   var merged=new List<ArchivePoint>(remote);int cursor=0;
   foreach(var point in local.OrderBy(p=>p.T)){while(cursor+1<remote.Count&&remote[cursor+1].T<=point.T)cursor++;var near=remote[cursor];bool covered=Math.Abs(near.T-point.T)<=TimeSpan.FromSeconds(Math.Max(2,near.Interval)).Ticks;if(!covered&&cursor+1<remote.Count)covered=Math.Abs(remote[cursor+1].T-point.T)<=TimeSpan.FromSeconds(Math.Max(2,remote[cursor+1].Interval)).Ticks;if(!covered)merged.Add(point);}
   return merged.OrderBy(p=>p.T).GroupBy(p=>p.T).Select(g=>g.First()).ToList();
  }catch(OperationCanceledException){throw;}catch{lock(recorderRetry)recorderRetry[id]=DateTime.UtcNow.AddSeconds(30);return local;}
 }
}
