using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
public class ArchiveHeader {public string Kind="AQServerHistory";public int Version=1;public string Name,Host,User;public int Port;public long Start,End;public bool Local;public static ArchiveHeader From(Server s){return new ArchiveHeader{Name=s.Name,Host=s.Host,User=s.User,Port=s.Port,Local=s.IsLocal};}}
public class ArchiveRecord {public List<DiskActivity> Storage;public HardwareInfo Hardware;public NetworkMetrics Network;public List<StorageVolume> Volumes;public string PrimaryStoragePath;
 public string CpuFrequencySource;public double? CpuFrequencyMHz;public int V=1;public long T;public int Interval;public string Cpu,Cores,Mem,Disk,Error;public int[] CoreIds;public double[] CoreUsage;public List<Gpu> Gpus;public List<GpuProcess> Processes;
 public static ArchiveRecord From(Metrics m,DateTime time,int interval){return new ArchiveRecord{Storage=m.Storage==null?null:m.Storage.Select(x=>x.Copy()).ToList(),Hardware=m.Hardware,Network=m.Network==null?null:m.Network.Copy(),Volumes=m.Volumes==null?null:m.Volumes.Select(x=>x.Copy()).ToList(),PrimaryStoragePath=m.PrimaryStoragePath,CpuFrequencySource=m.CpuFrequencySource,CpuFrequencyMHz=m.CpuFrequencyMHz,T=time.ToUniversalTime().Ticks,Interval=interval,Cpu=m.Cpu,Cores=m.Cores,Mem=m.Mem,Disk=m.Disk,Error=m.Error,CoreIds=m.CpuCores.Select(x=>x.Index).ToArray(),CoreUsage=m.CpuCores.Select(x=>x.Util).ToArray(),Gpus=m.Gpus==null?null:m.Gpus.Select(g=>new Gpu{Index=g.Index,Name=g.Name,Util=g.Util,Used=g.Used,Total=g.Total,Temp=g.Temp,Fan=g.Fan,Power=g.Power,Cap=g.Cap,Uuid=g.Uuid,PowerMax=g.PowerMax,CapSource=g.CapSource,Clock=g.Clock,MemoryClock=g.MemoryClock}).ToList(),Processes=m.Processes==null?null:m.Processes.Select(p=>new GpuProcess{Gpu=p.Gpu,Pid=p.Pid,Type=p.Type,Name=p.Name,CommandLine=p.CommandLine,IdentityToken=p.IdentityToken,Memory=p.Memory,User=p.User,SharedMemory=p.SharedMemory,MemorySource=p.MemorySource}).ToList()};}
 public Metrics Metrics(){var m=new Metrics{Storage=Storage==null?new List<DiskActivity>():Storage.Select(x=>x.Copy()).ToList(),Hardware=Hardware,Network=Network==null?null:Network.Copy(),Volumes=Volumes==null?new List<StorageVolume>():Volumes.Select(x=>x.Copy()).ToList(),PrimaryStoragePath=PrimaryStoragePath,CpuFrequencySource=CpuFrequencySource,CpuFrequencyMHz=CpuFrequencyMHz,Cpu=Cpu??"—",Cores=Cores??"—",Mem=Mem??"—",Disk=Disk??"—",Error=Error??"",Gpus=Gpus??new List<Gpu>(),Processes=Processes??new List<GpuProcess>()};if(CoreIds!=null&&CoreUsage!=null)for(int i=0;i<Math.Min(CoreIds.Length,CoreUsage.Length);i++)m.CpuCores.Add(new CpuCore{Index=CoreIds[i],Util=CoreUsage[i]});return m;}
}
public class ArchivePoint {public bool RemoteSummary,GapBefore;public long ByteOffset=-1;public long T;public double Cpu,Memory,Gpu;public int Count,Interval;public string Error,File;public string[] GpuIds;public double[] GpuUsage;public int Line;public double Value(string metric){if(metric.StartsWith("GPU ")){int i=Array.IndexOf(GpuIds??new string[0],metric.Substring(4));return i>=0&&GpuUsage!=null&&i<GpuUsage.Length?GpuUsage[i]:double.NaN;}return metric=="GPU"?Gpu:metric=="内存"?Memory:Cpu;}}
public class ArchiveDot {public long T;public double Value;public bool Break;}
public class ArchiveResult {
 public List<ArchivePoint> Points=new List<ArchivePoint>();public int Skipped;public long From,To;public ArchiveHeader Header;
 public List<ArchiveDot> Plot(string metric,int buckets=900){var output=new List<ArchiveDot>();var bin=new List<ArchivePoint>();int previousBucket=-1;ArchivePoint previous=null;bool pendingBreak=true;
 Action flush=()=>{if(bin.Count==0)return;var first=bin[0];var min=bin.OrderBy(x=>x.Value(metric)).First();var max=bin.OrderByDescending(x=>x.Value(metric)).First();var last=bin[bin.Count-1];bool gap=pendingBreak;foreach(var p in new[]{first,min,max,last}.GroupBy(x=>x.T).Select(x=>x.First()).OrderBy(x=>x.T)){output.Add(new ArchiveDot{T=p.T,Value=p.Value(metric),Break=gap});gap=false;}pendingBreak=false;bin.Clear();};
 foreach(var point in Points){double value=point.Value(metric);if(!string.IsNullOrEmpty(point.Error)||double.IsNaN(value)){flush();pendingBreak=true;previous=null;continue;}bool broken=previous!=null&&(point.T-previous.T)>TimeSpan.FromSeconds(Math.Max(15,Math.Max(point.Interval,previous.Interval)*3)).Ticks;int bucket=(int)((point.T-From)*(double)buckets/Math.Max(1,To-From));if(bucket!=previousBucket||broken){flush();if(broken)pendingBreak=true;previousBucket=bucket;}bin.Add(point);previous=point;}flush();return output;}
}
public class ArchiveInfo {public long Bytes;public DateTime? First,Last;public int Files;}
public class ArchiveStore {
 public static string DefaultRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AQServer","History");public string Root;public int Days=30,LimitMB=1024;readonly object gate=new object();readonly object indexGate=new object();readonly Dictionary<string,IndexedArchiveFile> index=new Dictionary<string,IndexedArchiveFile>(StringComparer.OrdinalIgnoreCase);long indexClock;static readonly Encoding Utf8=new UTF8Encoding(false);DateTime lastCleanup=DateTime.MinValue;
 public ArchiveStore(string root){Root=Path.GetFullPath(root);}
 public static JavaScriptSerializer Json(){return new JavaScriptSerializer{MaxJsonLength=4*1024*1024,RecursionLimit=64};}
 public static string Key(Server s){string id=s.IsLocal?"local:"+s.Host.ToLowerInvariant():s.Host.ToLowerInvariant()+":"+s.Port+"/"+s.User;using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(id))).Replace("-","").Substring(0,32).ToLowerInvariant();}
 public string Folder(Server server){return Path.Combine(Root,Key(server));}
 public void Append(Server server,ArchiveRecord record){lock(gate){var folder=Folder(server);Directory.CreateDirectory(folder);string metadata=Path.Combine(folder,"device.json");var json=Json();if(!File.Exists(metadata))File.WriteAllText(metadata,json.Serialize(ArchiveHeader.From(server)),Utf8);string stem=new DateTime(record.T,DateTimeKind.Utc).ToString("yyyyMMdd-HH",CultureInfo.InvariantCulture);int part=0;string file;do{file=Path.Combine(folder,stem+"-"+part.ToString("D3")+".aqlog");part++;}while(File.Exists(file)&&new FileInfo(file).Length>=8*1024*1024);var bytes=Utf8.GetBytes(json.Serialize(record)+"\n");using(var f=new FileStream(file,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read)){if(f.Length>0){f.Position=f.Length-1;if(f.ReadByte()!=10){f.Position=f.Length;f.WriteByte(10);}}f.Position=f.Length;f.Write(bytes,0,bytes.Length);f.Flush(true);}if((DateTime.UtcNow-lastCleanup).TotalMinutes>=10){Cleanup(file);lastCleanup=DateTime.UtcNow;}}}
 public void Cleanup(string protectedFile=null){lock(gate){if(!Directory.Exists(Root))return;var files=Directory.GetFiles(Root,"*.aqlog",SearchOption.AllDirectories).Select(x=>new FileInfo(x)).OrderBy(x=>x.Name,StringComparer.Ordinal).ToList();long bytes=files.Sum(x=>x.Length);DateTime cutoff=DateTime.UtcNow.AddDays(-Math.Max(1,Days));foreach(var file in files){if(string.Equals(file.FullName,protectedFile,StringComparison.OrdinalIgnoreCase))continue;DateTime hour;bool expired=TryHour(file.Name,out hour)&&hour.AddHours(1)<cutoff;if(expired||bytes>(long)Math.Max(1,LimitMB)*1024*1024){long length=file.Length;try{file.Delete();bytes-=length;}catch(IOException){}}}}}
 static bool TryHour(string file,out DateTime value){return DateTime.TryParseExact(Path.GetFileName(file).Substring(0,Math.Min(11,Path.GetFileName(file).Length)),"yyyyMMdd-HH",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out value);}
 public ArchiveHeader ReadHeader(string path){using(var reader=Open(path)){var header=Json().Deserialize<ArchiveHeader>(reader.ReadLine()??"");if(header==null||header.Kind!="AQServerHistory"||header.Version!=1)throw new InvalidDataException("不是支持的 AQ Server 存档。");return header;}}
 public ArchiveInfo Info(Server server){var info=new ArchiveInfo();string folder=Folder(server);if(!Directory.Exists(folder))return info;foreach(string file in Directory.GetFiles(folder,"*.aqlog")){var f=new FileInfo(file);info.Bytes+=f.Length;info.Files++;DateTime time;if(TryHour(f.Name,out time)){if(!info.First.HasValue||time<info.First)info.First=time;if(!info.Last.HasValue||time.AddHours(1)>info.Last)info.Last=time.AddHours(1);}}return info;}
 public ArchiveResult Query(Server server,DateTime from,DateTime to,CancellationToken cancel,string external=null){long start=from.ToUniversalTime().Ticks,end=to.ToUniversalTime().Ticks;if(end<=start)throw new ArgumentException("结束时间必须晚于开始时间。");var result=new ArchiveResult{From=start,To=end,Header=ArchiveHeader.From(server)};IEnumerable<string> files;
 if(external!=null){files=new[]{external};using(var reader=Open(external)){string header=reader.ReadLine();var metadata=Json().Deserialize<ArchiveHeader>(header??"");if(metadata==null||metadata.Kind!="AQServerHistory"||metadata.Version!=1)throw new InvalidDataException("不是支持的 AQ Server 历史存档。");result.Header=metadata;}}
 else{string folder=Folder(server);if(!Directory.Exists(folder))return result;files=Directory.GetFiles(folder,"*.aqlog").Where(file=>{DateTime hour;return TryHour(file,out hour)&&hour.Ticks<=end&&hour.AddHours(1).Ticks>=start;}).OrderBy(x=>x,StringComparer.Ordinal);}
 foreach(string file in files){cancel.ThrowIfCancellationRequested();try{var cached=IndexFile(file,external!=null,cancel);result.Skipped+=cached.Skipped;foreach(var point in cached.Points){cancel.ThrowIfCancellationRequested();if(point.T<start||point.T>end)continue;result.Points.Add(point);if(result.Points.Count>250000)throw new OverflowException("超过 25 万个采样，请缩小查询时间范围。原始存档仍保留。");}}catch(FileNotFoundException){result.Skipped++;}}

 result.Points=result.Points.OrderBy(x=>x.T).ToList();return result;}
 // A bounded, disposable index of summaries. Full process/hardware records remain on disk.
 sealed class IndexedArchiveFile {public long Length,WriteTicks,CreationTicks,End,Access,Estimate;public int Lines,Skipped;public byte[] Head,Tail;public List<ArchivePoint> Points=new List<ArchivePoint>();}
 static byte[] Marker(string path,long offset,int count){using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){f.Position=Math.Max(0,offset);var b=new byte[count];int n=0,k;while(n<count&&(k=f.Read(b,n,count-n))>0)n+=k;if(n!=count)Array.Resize(ref b,n);return b;}}
 IndexedArchiveFile IndexFile(string file,bool external,CancellationToken cancel){lock(indexGate){
  cancel.ThrowIfCancellationRequested();var info=new FileInfo(file);long length=info.Length,write=info.LastWriteTimeUtc.Ticks,creation=info.CreationTimeUtc.Ticks;IndexedArchiveFile state;
  if(!index.TryGetValue(file,out state)||external)state=null;
  if(state!=null&&state.Length==length&&state.WriteTicks==write&&state.CreationTicks==creation){state.Access=++indexClock;return state;}
  bool append=state!=null&&length>state.Length&&creation==state.CreationTicks&&Marker(file,0,state.Head.Length).SequenceEqual(state.Head)&&Marker(file,Math.Max(0,state.End-state.Tail.Length),state.Tail.Length).SequenceEqual(state.Tail);
  // Stage extensions separately: a cancelled request must never poison the old index.
  var next=new IndexedArchiveFile();if(append){next.Points.AddRange(state.Points);next.Lines=state.Lines;next.Skipped=state.Skipped;next.End=state.End;}
  using(var reader=new ArchiveLineReader(file,next.End)){string line;long offset;int number=next.Lines;while((line=reader.ReadLine(out offset))!=null){cancel.ThrowIfCancellationRequested();number++;
   if((external&&number==1)||string.IsNullOrWhiteSpace(line)){next.Lines=number;next.End=reader.Position;continue;}
   try{var summary=ArchiveSummaryReader.Read(line,cancel);summary.File=file;summary.Line=number;summary.ByteOffset=offset;next.Points.Add(summary);if(next.Points.Count>250000)throw new OverflowException("存档记录过多，请缩小查询范围。");}
   catch(Exception e){if(e is OperationCanceledException||e is OutOfMemoryException||e is OverflowException)throw;if(!reader.Terminated)break;next.Skipped++;}
   next.Lines=number;next.End=reader.Position;
  }}
  cancel.ThrowIfCancellationRequested();next.Access=++indexClock;next.Estimate=next.Points.Sum(x=>256L+(x.GpuIds==null?0:x.GpuIds.Length*48L));info.Refresh();
  // Do not commit a view of a file that changed while it was being read.
  if(!external&&info.Exists&&info.Length==length&&info.LastWriteTimeUtc.Ticks==write){next.Length=length;next.WriteTicks=write;next.CreationTicks=creation;next.Head=Marker(file,0,(int)Math.Min(64,next.End));next.Tail=Marker(file,Math.Max(0,next.End-64),(int)Math.Min(64,next.End));index[file]=next;
   foreach(var gone in index.Keys.Where(k=>!File.Exists(k)).ToArray())index.Remove(gone);
   while(index.Count>128||index.Values.Sum(x=>x.Estimate)>64L*1024*1024||index.Values.Sum(x=>(long)x.Points.Count)>100000){var oldest=index.OrderBy(x=>x.Value.Access).First().Key;index.Remove(oldest);}
  }return next;
 }}
 static double AverageGpu(List<Gpu> list){var values=list.Select(x=>ResourceSample.ParsePercent(x.Util)).Where(x=>!double.IsNaN(x)).ToArray();return values.Length==0?double.NaN:values.Average();}
 static StreamReader Open(string file){return new StreamReader(new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete),Encoding.UTF8,true,65536);}
 public ArchiveRecord Read(ArchivePoint point){using(var stream=new FileStream(point.File,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){if(point.ByteOffset>=0)stream.Seek(point.ByteOffset,SeekOrigin.Begin);using(var reader=new StreamReader(stream,Encoding.UTF8,true,65536)){if(point.ByteOffset<0)for(int i=1;i<point.Line;i++)if(reader.ReadLine()==null)throw new InvalidDataException("记录已不可用，请重新查询。");var value=reader.ReadLine();var record=Json().Deserialize<ArchiveRecord>(value??"");if(record==null||record.T!=point.T)throw new InvalidDataException("存档已变化，请重新查询。");return record;}}}
 public void ExportCsv(ArchiveResult result,string path){using(var writer=new StreamWriter(path,false,new UTF8Encoding(true))){writer.WriteLine("时间(本机),UTC,CPU利用率(%),内存利用率(%),GPU平均利用率(%),GPU数量,状态");foreach(var p in result.Points){writer.WriteLine(string.Join(",",new[]{Csv(new DateTime(p.T,DateTimeKind.Utc).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")),new DateTime(p.T,DateTimeKind.Utc).ToString("o"),Num(p.Cpu),Num(p.Memory),Num(p.Gpu),p.Count.ToString(CultureInfo.InvariantCulture),Csv(string.IsNullOrEmpty(p.Error)?"正常":p.Error)}));}}}
 public void ExportArchive(ArchiveResult result,string path,CancellationToken cancel){var temp=path+".tmp";try{using(var writer=new StreamWriter(temp,false,Utf8)){var header=Json().Deserialize<ArchiveHeader>(Json().Serialize(result.Header));header.Start=result.From;header.End=result.To;writer.WriteLine(Json().Serialize(header));foreach(var group in result.Points.GroupBy(x=>x.File)){cancel.ThrowIfCancellationRequested();var needed=new HashSet<int>(group.Select(x=>x.Line));using(var reader=Open(group.Key)){int i=0;string line;while((line=reader.ReadLine())!=null){i++;cancel.ThrowIfCancellationRequested();if(needed.Contains(i))writer.WriteLine(line);}}}}File.Copy(temp,path,true);}finally{if(File.Exists(temp))File.Delete(temp);}}
 static string Num(double n){return double.IsNaN(n)?"":n.ToString("0.###",CultureInfo.InvariantCulture);}static string Csv(string value){value=value??"";if(value.Length>0&&"=+-@".IndexOf(value[0])>=0)value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
}
public class ArchiveService : IDisposable {
 class Write {public Server Server;public ArchiveRecord Record;}
 readonly BlockingCollection<Write> queue=new BlockingCollection<Write>(512);readonly Thread worker;public readonly ArchiveStore Store;public string Status="等待首次采样";public int WriteFailures,Dropped;public DateTime LastSavedUtc;bool disposed;int pending;
 public ArchiveService(int days,int limit,string root=null){Store=new ArchiveStore(root??ArchiveStore.DefaultRoot){Days=days,LimitMB=limit};worker=new Thread(Work){IsBackground=true,Name="AQServer history writer"};worker.Start();}
 public void Enqueue(Server server,Metrics metrics,int interval,DateTime? timestamp=null){if(disposed)return;var write=new Write{Server=server.Copy(),Record=ArchiveRecord.From(metrics,timestamp??DateTime.UtcNow,interval)};Interlocked.Increment(ref pending);try{if(!queue.TryAdd(write)){Interlocked.Decrement(ref pending);Interlocked.Increment(ref Dropped);Status="存档队列已满，部分采样未保存";}}catch(InvalidOperationException){Interlocked.Decrement(ref pending);}}
 void Work(){foreach(var item in queue.GetConsumingEnumerable()){try{Store.Append(item.Server,item.Record);LastSavedUtc=new DateTime(item.Record.T,DateTimeKind.Utc);Status="存档正常"+((WriteFailures+Dropped)>0?" · 累计失败 "+WriteFailures+" / 丢弃 "+Dropped:"");}catch(Exception e){Interlocked.Increment(ref WriteFailures);Status="存档失败："+e.Message;}finally{Interlocked.Decrement(ref pending);}}}
 public bool Flush(int milliseconds){var until=DateTime.UtcNow.AddMilliseconds(milliseconds);while(Volatile.Read(ref pending)>0&&DateTime.UtcNow<until)Thread.Sleep(10);return Volatile.Read(ref pending)==0;}
 public void Dispose(){if(disposed)return;disposed=true;queue.CompleteAdding();worker.Join(5000);}
}

// Byte-accurate UTF-8 line offsets support both Windows CRLF and imported LF archives.
internal sealed class ArchiveLineReader : IDisposable {
 readonly FileStream stream;readonly byte[] buffer=new byte[65536];int index,length;long consumed;
 public long Position{get{return consumed;}}public bool Terminated{get;private set;}
 public ArchiveLineReader(string file,long offset=0){stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);stream.Position=offset;consumed=offset;}
 public string ReadLine(out long offset){
  offset=consumed;Terminated=false;MemoryStream overflow=null;
  try{while(true){
   if(index==length){length=stream.Read(buffer,0,buffer.Length);index=0;if(length==0){if(overflow==null)return null;return Decode(overflow.ToArray(),0,(int)overflow.Length,offset);}}
   int end=Array.IndexOf(buffer,(byte)10,index,length-index),count=(end<0?length:end)-index;
   if(end>=0&&overflow==null){Terminated=true;string line=Decode(buffer,index,count,offset);index=end+1;consumed+=count+1;return line;}
   if(overflow==null)overflow=new MemoryStream();overflow.Write(buffer,index,count);index+=count;consumed+=count;
   if(end>=0){Terminated=true;index++;consumed++;return Decode(overflow.ToArray(),0,(int)overflow.Length,offset);}
  }}finally{if(overflow!=null)overflow.Dispose();}
 }
 static string Decode(byte[] data,int start,int count,long offset){if(count>0&&data[start+count-1]==13)count--;if(offset==0&&count>=3&&data[start]==239&&data[start+1]==187&&data[start+2]==191){start+=3;count-=3;}return Encoding.UTF8.GetString(data,start,count);}
 public void Dispose(){stream.Dispose();}
}
