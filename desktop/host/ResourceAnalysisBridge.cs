using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

public sealed partial class WorkbenchWindow {
 // Read the app's existing archive, with a separate request from History so a
 // resource page never cancels a history snapshot query. Never fetch N SSH details.
 async Task<object> QueryResourceAnalysis(Dictionary<string,object> args){
  var device=Requested(args).Copy();
  var range=HistoryRange.Parse(args,DateTime.UtcNow);
  if(range.To-range.From>TimeSpan.FromHours(3))throw new ArgumentException("分析时段最多为 3 小时。");
  string metric=args.ContainsKey("metric")?Convert.ToString(args["metric"]):"";
  if(metric!="cpu"&&metric!="memory"&&metric!="gpu"&&metric!="network")throw new ArgumentException("不支持的分析指标。");
  using(var deadline=new CancellationTokenSource(22000)){
   var cancel=deadline.Token;
   await reads.WaitAsync(cancel);
   try{return await Task.Run<object>(()=>{
    var history=archive.Store.Query(device,range.From,range.To,cancel);
    if(!device.IsLocal&&(metric=="cpu"||metric=="memory"))history.Points=RecorderHistory(device,history.Points,range.From,range.To,cancel);
    var raw=history.Points;
    var selected=AnalysisBuckets(raw,range.From.Ticks,range.To.Ticks,metric=="gpu"?360:640);
    var processPoints=(metric=="cpu"||metric=="memory")?AnalysisBuckets(raw.Where(p=>p.HasResourceProcesses).ToList(),range.From.Ticks,range.To.Ticks,1200):new List<ArchivePoint>();
    var processTimes=new HashSet<long>(processPoints.Select(p=>p.T));
    if(processPoints.Count>0)selected=selected.Concat(processPoints).GroupBy(p=>p.T).Select(g=>g.First()).OrderBy(p=>p.T).ToList();
    // The host's JSON serializer is limited to 4 MiB. Thin time points as needed
    // before returning; a retained point always keeps every card's process rows.
    const int payloadLimit=3800000;
    var sizes=new List<int>(selected.Count);var retained=new List<ArchivePoint>(selected.Count);var rows=new List<AnalysisHistoryRow>(selected.Count);
    var sizing=metric=="network"?null:ArchiveStore.Json();int payloadSize=0,unreadable=0;
    foreach(var point in selected){
     cancel.ThrowIfCancellationRequested();
     ArchiveRecord record=null;
     if(string.IsNullOrEmpty(point.Error)&&!string.IsNullOrEmpty(point.File)&&!point.File.StartsWith("asterhub-agent:",StringComparison.Ordinal)){
      try{record=archive.Store.Read(point);}catch(IOException){unreadable++;}catch(UnauthorizedAccessException){unreadable++;}catch(ArgumentException){unreadable++;}catch(InvalidOperationException){unreadable++;}
     }
     var net=record==null?null:record.Network;
     var good=string.IsNullOrEmpty(point.Error);
     var row=new AnalysisHistoryRow{time=Milliseconds(new DateTime(point.T,DateTimeKind.Utc)),interval=point.Interval,cpu=good?AnalysisFinite(point.Cpu):null,memory=good?AnalysisFinite(point.Memory):null,mem=metric=="memory"?(record==null?null:record.Mem):null,
      cores=metric=="cpu"?(record==null?null:record.CoreIds):null,coreValues=metric=="cpu"?(record==null?null:record.CoreUsage):null,
      processes=record!=null&&processTimes.Contains(point.T)&&record.ResourceProcesses!=null?record.ResourceProcesses.Where(p=>metric=="memory"?p.ResidentBytes.HasValue:p.Cpu.HasValue).OrderByDescending(p=>metric=="cpu"?p.Cpu.GetValueOrDefault():p.ResidentBytes.GetValueOrDefault()).Take(24).Select(p=>new{p.Pid,p.Identity,p.Cpu,p.ResidentBytes,p.Path,Name=AnalysisName(p.Name??p.Path)}).ToArray():null,
      gpus=metric=="gpu"&&record!=null&&record.Gpus!=null?record.Gpus.Select(g=>new{g.Index,g.Uuid,g.Name,g.Used,g.Total}).ToArray():null,
      gpuProcesses=metric=="gpu"&&record!=null&&record.Processes!=null?AnalysisGpuProcesses(record).Select(p=>new{p.Gpu,p.Pid,Name=AnalysisName(p.Name??p.Path),p.IdentityToken,p.Memory,Path=GpuProcessPaths.Recorded(p,!device.IsLocal)}).ToArray():null,
      adapters=metric=="network"&&net!=null&&net.Interfaces!=null?record.Network.Interfaces.Select(n=>new{n.Id,n.Name,n.ReceivedBytes,n.SentBytes,n.ReceiveErrors,n.SendErrors,n.ReceiveDiscards,n.SendDiscards,n.DownloadBytesPerSecond,n.UploadBytesPerSecond}).ToArray():null};
     rows.Add(row);retained.Add(point);
     if(sizing!=null){int size=sizing.Serialize(row).Length+1;sizes.Add(size);payloadSize+=size;}
     while(payloadSize>payloadLimit&&retained.Count>2){
      int remove=1;
      for(int i=2;i<retained.Count-1;i++)if(retained[i+1].T-retained[i-1].T<retained[remove+1].T-retained[remove-1].T)remove=i;
      payloadSize-=sizes[remove];sizes.RemoveAt(remove);rows.RemoveAt(remove);retained.RemoveAt(remove);
     }
    }
    if(payloadSize>payloadLimit)throw new InvalidOperationException("分析样本内容过大，请选择更短的时间范围。");
    var gaps=HistoryService.Gaps(raw,retained);
    for(int i=0;i<rows.Count;i++)rows[i].gapBefore=gaps.Contains(retained[i].T)||!string.IsNullOrEmpty(retained[i].Error);
    return new{points=rows,from=Milliseconds(range.From),to=Milliseconds(range.To),thinned=retained.Count<raw.Count,skipped=history.Skipped+unreadable};
   },cancel);}finally{reads.Release();}
  }
 }
 sealed class AnalysisHistoryRow {
  public double time;public int interval;public bool gapBefore;public double? cpu,memory;
  public string mem;public int[] cores;public double[] coreValues;
  public object processes,gpus,gpuProcesses,adapters;
 }
 static List<ArchivePoint> AnalysisBuckets(List<ArchivePoint> points,long from,long to,int limit){
  if(points.Count<=limit)return points;
  var stride=Math.Max(1L,(to-from+limit-1)/limit);
  return points.GroupBy(x=>Math.Min(limit-1,(x.T-from)/stride)).Select(group=>group.Last()).ToList();
 }
 // Keep an independent budget per GPU so later GPUs cannot be dropped by earlier groups.
 static IEnumerable<GpuProcess> AnalysisGpuProcesses(ArchiveRecord record){
  if(record.Gpus==null)return Enumerable.Empty<GpuProcess>();
  var cards=new HashSet<string>(record.Gpus.Select(g=>g.Index),StringComparer.Ordinal);
  return record.Processes.Where(p=>cards.Contains(p.Gpu)&&!string.IsNullOrEmpty(p.IdentityToken))
   .Select(p=>new{Process=p,Memory=AnalysisGpuMemory(p.Memory)}).Where(p=>p.Memory.HasValue)
   .GroupBy(p=>p.Process.Gpu).SelectMany(card=>card.OrderByDescending(p=>p.Memory.Value).Take(16)).Select(p=>p.Process);
 }
 static double? AnalysisFinite(double value){return double.IsNaN(value)||double.IsInfinity(value)?(double?)null:value;}
 static double? AnalysisGpuMemory(string value){
  var raw=(value??"").Trim();
  if(raw.EndsWith(" MiB",StringComparison.Ordinal))raw=raw.Substring(0,raw.Length-4);
  double memory;return double.TryParse(raw,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out memory)&&!double.IsInfinity(memory)&&memory>=0?(double?)memory:null;
 }
 static string AnalysisName(string text){
  if(string.IsNullOrEmpty(text))return text;
  int slash=Math.Max(text.LastIndexOf('/'),text.LastIndexOf('\\'));
  string name=slash<0?text:text.Substring(slash+1);
  return name.Length<=80?name:name.Substring(0,80);
 }
}
