using System;using System.IO;using System.Linq;using System.Text;using System.Threading;using System.Diagnostics;using System.Collections.Generic;
internal static class HistoryTests {
 static int failures;static void Check(bool pass,string name){Console.WriteLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures++;}
 static void Reject(Action action,string name){try{action();Check(false,name);}catch(ArgumentException){Check(true,name);}}
 static void Main(string[] args){
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(root);var server=new Server{Host="history.fixture",User="qa",Port=22};var now=DateTime.UtcNow;var epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);double end=(now-epoch).TotalMilliseconds;
  var preset=HistoryRange.Parse(new Dictionary<string,object>{{"minutes",1440}},now);Check(preset.To==now&&preset.To-preset.From==TimeSpan.FromHours(24),"preset uses one captured end time for exact 24h boundaries");
  var custom=HistoryRange.Parse(new Dictionary<string,object>{{"fromUtc",end-7200000},{"toUtc",end-3600000}},now);Check(custom.To-custom.From==TimeSpan.FromHours(1),"custom start and end epoch values are respected");
  Reject(()=>HistoryRange.Parse(new Dictionary<string,object>{{"fromUtc",end}},now),"reject incomplete custom bounds");Reject(()=>HistoryRange.Parse(new Dictionary<string,object>{{"fromUtc",end},{"toUtc",end}},now),"reject reversed or empty range");Reject(()=>HistoryRange.Parse(new Dictionary<string,object>{{"fromUtc",end},{"toUtc",end+86400000}},now),"reject future range");
  var data=new List<ArchivePoint>();long start=now.AddDays(-1).Ticks;for(int i=0;i<43200;i++)data.Add(new ArchivePoint{T=start+i*TimeSpan.TicksPerSecond*2,Cpu=i%90,Memory=i%30,Gpu=i%70,Interval=2});data[12345].Gpu=100;data[24680].Cpu=100;data[32000].Memory=100;data[20000].GapBefore=true;
  var sw=Stopwatch.StartNew();var thin=HistoryService.Thin(data);Console.WriteLine("THIN_43200_MS="+sw.ElapsedMilliseconds);Check(thin.Count<=3000&&thin.All(data.Contains)&&thin.Contains(data[12345])&&thin.Contains(data[24680])&&thin.Contains(data[32000]),"bounded downsampling retains actual CPU GPU memory extrema");var gaps=HistoryService.Gaps(data,thin);Check(gaps.Count==1&&gaps.Contains(thin.First(point=>point.T>=data[20000].T).T),"downsampling retains acquisition gap boundaries");
  Func<int,bool,bool,ArchivePoint> at=(seconds,remote,gap)=>new ArchivePoint{T=start+seconds*TimeSpan.TicksPerSecond,Interval=2,Cpu=24,RemoteSummary=remote,GapBefore=gap};
  var temporary=at(2,false,false);temporary.Error="采样未返回数据";
  var explicitDisconnect=at(2,false,false);explicitDisconnect.Error="Connection reset by peer";
  var extraMiss=at(3,false,false);extraMiss.Error="未返回数据";
  var restored=at(4,false,false);var gapLong=at(18,false,false);
  var close=new List<ArchivePoint>{at(0,false,false),temporary,restored};
  Check(HistoryService.Gaps(close,close).Count==0,"one close missed archive sample is bridged");
  var disconnected=new List<ArchivePoint>{at(0,false,false),explicitDisconnect,restored};
  Check(HistoryService.Gaps(disconnected,new List<ArchivePoint>{disconnected[0],restored}).Contains(restored.T),"explicit disconnect survives history thinning even inside nominal cadence");
  var multiple=new List<ArchivePoint>{at(0,false,false),temporary,extraMiss,restored};
  Check(HistoryService.Gaps(multiple,multiple).Contains(restored.T),"multiple misses cannot be masked by a fast recovery");
  var wide=new List<ArchivePoint>{at(0,false,false),temporary,gapLong};
  Check(HistoryService.Gaps(wide,wide).Contains(gapLong.T),"long acquisition hole remains a break");
  var backup=Enumerable.Range(1,24).Select(i=>at(i*2,false,false)).ToList();
  var recorder=new List<ArchivePoint>{at(0,true,false),at(22,true,false),at(48,true,true)};
  var merged=HistoryService.MergeRecorder(backup,recorder);
  Check(!merged.Any(point=>!point.RemoteSummary&&point.T<recorder[1].T),"thinned recorder coverage excludes interleaved desktop points");
  Check(merged.Count==15&&HistoryService.Gaps(merged,merged).Count==0,"complete desktop coverage heals recorder gap using real samples");
  var missing=new List<ArchivePoint>{at(0,true,false),at(22,true,false),at(48,true,true)};
  var stillBroken=HistoryService.MergeRecorder(new List<ArchivePoint>{at(24,false,false),at(26,false,false)},missing);
  Check(HistoryService.Gaps(stillBroken,stillBroken).Contains(missing[2].T),"uncovered recorder interruption stays visible");
  var cancelled=new CancellationTokenSource();cancelled.Cancel();bool stopped=false;try{HistoryService.Thin(data,cancelled.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"downsampling honors query cancellation");
  var json=ArchiveStore.Json();var heavy=new ArchiveRecord{T=start,Interval=2,Cpu="42%",Mem="256 / 1024 MiB",Error="quoted \\\" text\n",Gpus=new List<Gpu>{new Gpu{Index="0",Util="20%",Name="GPU"},new Gpu{Index="1",Util="80%"}},Processes=Enumerable.Range(0,160).Select(i=>new GpuProcess{Gpu="0",Pid=i.ToString(),Name="fixture process "+i,CommandLine="test "+new string('x',180),Memory="100 MiB"}).ToList()};
  var store=new ArchiveStore(root);string folder=store.Folder(server);Directory.CreateDirectory(folder);string file=Path.Combine(folder,new DateTime(start,DateTimeKind.Utc).ToString("yyyyMMdd-HH")+"-000.aqlog");
  using(var writer=new StreamWriter(file,false,new UTF8Encoding(false))){for(int i=0;i<1200;i++){heavy.T=start+i*TimeSpan.TicksPerSecond*2;writer.WriteLine(json.Serialize(heavy));}}
  sw.Restart();var result=store.Query(server,new DateTime(start,DateTimeKind.Utc),now,CancellationToken.None);long cold=sw.ElapsedMilliseconds;sw.Restart();var warm=store.Query(server,new DateTime(start,DateTimeKind.Utc),now,CancellationToken.None);long warmed=sw.ElapsedMilliseconds;
  Check(result.Points.Count==1200&&result.Points.All(point=>point.Cpu==42&&point.Memory==25&&point.Gpu==50&&point.Count==2),"GPU heavy archive summaries preserve exact aggregate readings");
  var full=store.Read(result.Points[567]);Check(full.Processes.Count==160&&full.Gpus.Count==2&&full.T==result.Points[567].T,"full process and GPU snapshot remains readable by exact byte offset");
  Check(warm.Points.Count==result.Points.Count,"warm query reuses valid index");Console.WriteLine("HEAVY_ARCHIVE_BYTES="+new FileInfo(file).Length+" COLD_MS="+cold+" WARM_MS="+warmed);
  File.AppendAllText(file,"{\"V\":1,\"T\":"+start+",\"Processes\":[{\"broken\":}] }\n",new UTF8Encoding(false));Check(store.Query(server,new DateTime(start,DateTimeKind.Utc),now,CancellationToken.None).Skipped==1,"malformed skipped process subtree is rejected");
  Console.WriteLine("FAILURES="+failures);Environment.ExitCode=failures==0?0:1;
 }
}
