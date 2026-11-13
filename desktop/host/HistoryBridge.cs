using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class WorkbenchWindow {
 readonly Dictionary<string,string> historyQueryIds=new Dictionary<string,string>();
 object CancelHistory(Dictionary<string,object> p){var device=Requested(p);string id=DeviceRegistry.Id(device),queryId=p.ContainsKey("queryId")?Convert.ToString(p["queryId"]):null;lock(historyGate){string current;CancellationTokenSource source;if(queryId!=null&&historyQueryIds.TryGetValue(id,out current)&&String.Equals(current,queryId,StringComparison.Ordinal)&&historyQueries.TryGetValue(id,out source))source.Cancel();}return true;}
 async Task<object> QueryHistory(Dictionary<string,object> p){
  var device=Requested(p).Copy();string id=DeviceRegistry.Id(device);var range=HistoryRange.Parse(p,DateTime.UtcNow);string queryId=p.ContainsKey("queryId")?Convert.ToString(p["queryId"]):Guid.NewGuid().ToString("N");if(string.IsNullOrEmpty(queryId)||queryId.Length>100)throw new ArgumentException("历史查询标识无效。");
  var source=new CancellationTokenSource();source.CancelAfter(45000);lock(historyGate){CancellationTokenSource older;if(historyQueries.TryGetValue(id,out older))older.Cancel();historyQueries[id]=source;historyQueryIds[id]=queryId;}bool slot=false;
  try{await reads.WaitAsync(source.Token);slot=true;return await Task.Run<object>(()=>{
   var data=archive.Store.Query(device,range.From,range.To,source.Token);source.Token.ThrowIfCancellationRequested();data.Points=RecorderHistory(device,data.Points,range.From,range.To,source.Token);source.Token.ThrowIfCancellationRequested();
   var displayed=HistoryService.Thin(data.Points,source.Token);var gaps=HistoryService.Gaps(data.Points,displayed);source.Token.ThrowIfCancellationRequested();
   lock(pointGate){foreach(var point in displayed)points[(id=="local"?"":id+":")+point.T.ToString(CultureInfo.InvariantCulture)]=point;if(points.Count>10000){var keep=new HashSet<string>();foreach(var point in displayed)keep.Add((id=="local"?"":id+":")+point.T.ToString(CultureInfo.InvariantCulture));foreach(var key in new List<string>(points.Keys))if(!keep.Contains(key))points.Remove(key);}}
   var rows=new List<object>(displayed.Count);foreach(var point in displayed)rows.Add(new{id=(id=="local"?"":id+":")+point.T.ToString(CultureInfo.InvariantCulture),time=Milliseconds(new DateTime(point.T,DateTimeKind.Utc)),cpu=Finite(point.Cpu),memory=Finite(point.Memory),gpu=Finite(point.Gpu),gapBefore=gaps.Contains(point.T),interval=point.Interval});
   return new{total=data.Points.Count,points=rows.ToArray(),skipped=data.Skipped,fromUtc=Milliseconds(range.From),toUtc=Milliseconds(range.To),queryId=queryId};
  });}finally{if(slot)reads.Release();lock(historyGate){CancellationTokenSource current;if(historyQueries.TryGetValue(id,out current)&&ReferenceEquals(current,source)){historyQueries.Remove(id);historyQueryIds.Remove(id);}}source.Dispose();}
 }
 // A warm renderer range may outlive the bounded native point index. Recover only
 // from this requested device's real archive, never from a client-supplied path.
 async Task<object> ReadHistory(Dictionary<string,object> p){
  var device=Requested(p).Copy();string id=DeviceRegistry.Id(device),key=Convert.ToString(p["key"]),stamp=id=="local"?key:key!=null&&key.StartsWith(id+":",StringComparison.Ordinal)?key.Substring(id.Length+1):null;
  long ticks;if(stamp==null||!long.TryParse(stamp,NumberStyles.None,CultureInfo.InvariantCulture,out ticks)||ticks<DateTime.MinValue.Ticks||ticks>=DateTime.MaxValue.Ticks)throw new InvalidOperationException("快照不在当前设备索引中");
  ArchivePoint selected;lock(pointGate)points.TryGetValue(key,out selected);
  await reads.WaitAsync();try{return await Task.Run<object>(()=>{
   if(selected==null){var time=new DateTime(ticks,DateTimeKind.Utc);var matches=archive.Store.Query(device,time,time.AddTicks(1),CancellationToken.None);selected=matches.Points.Find(point=>point.T==ticks);}
   ArchiveRecord record;if(selected!=null&&(selected.File==null||!selected.File.StartsWith("asterhub-agent:",StringComparison.Ordinal)))record=archive.Store.Read(selected);else if(!device.IsLocal)record=json.Deserialize<ArchiveRecord>(RunRecorder(device,"read",ticks:ticks));else throw new InvalidOperationException("此时间点的原始存档已不可用，请刷新历史。");
   if(record==null||record.T!=ticks)throw new InvalidOperationException("此常驻记录已过期，请刷新历史");return Snapshot(record.Metrics(),new DateTime(record.T,DateTimeKind.Utc),device,true);
  });}finally{reads.Release();}
 }

}
