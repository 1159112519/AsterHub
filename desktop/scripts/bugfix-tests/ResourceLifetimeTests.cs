using System;using System.IO;using System.IO.Compression;using System.Linq;using System.Collections.Generic;using System.Runtime.CompilerServices;using System.Threading.Tasks;
public static class ResourceLifetimeTests {
 static void Assert(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
 static StorageSnapshot Make(string id){var s=new StorageSnapshot{Id=id,DeviceId="local",Endpoint="local",Root=@"C:\",Created=DateTime.UtcNow,IsLocal=true};s.Nodes.Add("0",new StorageNode{Id="0",Directory=true,Path=@"C:\"});for(int i=1;i<300;i++){string key=i.ToString();s.Nodes.Add(key,new StorageNode{Id=key,Parent="0",Name="data"+key,Path=@"C:\data"+key,Bytes=i});s.Nodes["0"].Children.Add(key);}s.Aggregate();return s;}
 [MethodImpl(MethodImplOptions.NoInlining)]static WeakReference Seed(StorageSnapshotCache cache,StorageScanJob job,DateTime time){var s=Make("old");job.Result=s;job.Status="ready";job.Work=Task.FromResult(0);cache.Put("local",s,time);return new WeakReference(s.Nodes["1"]);}
 public static int Main(){var time=DateTime.UtcNow;var cache=new StorageSnapshotCache();var job=new StorageScanJob(new Server{IsLocal=true},@"C:\");var weak=Seed(cache,job,time);StorageSnapshot current;
  cache.Trim(time.AddSeconds(59));Assert(cache.TryGetValue("local",out current),"graph remains during short idle");current=null;
  cache.Trim(time.AddSeconds(61));job.ReleaseResult();Assert(!cache.TryGetValue("local",out current)&&job.Result.Id=="old"&&job.Result.Nodes.Count==0,"expiry releases graph and completed job retains metadata");GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();Assert(!weak.IsAlive,"expired graph is collectible; task summary does not retain nodes");
  cache.Put("local",Make("new"),time);cache.Touch("local",time.AddSeconds(50));cache.Trim(time.AddSeconds(61));Assert(cache.TryGetValue("local",out current),"real query extends graph lifetime");cache.Trim(time.AddSeconds(111));Assert(!cache.TryGetValue("local",out current),"status lookup does not extend graph lifetime");
  cache.Put("leased",Make("leased"),time);cache.Lease("leased",true,time.AddSeconds(50));cache.Trim(time.AddSeconds(75));Assert(cache.TryGetValue("leased",out current),"active viewer lease prevents a costly reload after sixty seconds");cache.Trim(time.AddSeconds(96));Assert(!cache.TryGetValue("leased",out current),"abandoned viewer lease expires without keeping graph forever");
  cache.Put("hidden",Make("hidden"),time);cache.Lease("hidden",true,time.AddSeconds(50));cache.Trim(time.AddSeconds(75),false);Assert(!cache.TryGetValue("hidden",out current),"hidden window can release an unused graph despite stale viewer lease");
  var baseSummary=StorageSnapshotSummary.From(Make("summary"));long before=baseSummary.Bytes;var moved=new[]{new StorageSummaryRemoval{Path=@"C:\data1",Bytes=1}};
  Assert(baseSummary.Overlay(moved).Bytes==before-1&&baseSummary.Bytes==before,"trash summary updates without graph and never mutates base metadata");Assert(baseSummary.Overlay(new StorageSummaryRemoval[0]).Bytes==before,"restore immediately recovers status summary without index timestamp change");
  var nested=new[]{new StorageSummaryRemoval{Path=@"C:\folder",Bytes=20,Directory=true},new StorageSummaryRemoval{Path=@"C:\folder\child",Bytes=10}};
  Assert(baseSummary.Overlay(nested).Bytes==before-20&&!baseSummary.Overlay(nested).Count.HasValue,"nested trash entries are subtracted only once; unknown folder count stays unknown");
  Assert(baseSummary.Overlay(new[]{new StorageSummaryRemoval{Path=@"D:\other",Bytes=999}}).Bytes==before,"trash from another scan root cannot change summary");
  Assert(baseSummary.Overlay(moved.Concat(moved)).Bytes==before-1,"duplicate trash paths cannot subtract the same file twice");
  string root=Path.Combine(Path.GetTempPath(),"asterhub-lifetime-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);try{
   string file=Path.Combine(root,"index.gz");var s=Make("roundtrip");s.Save(file);var summary=StorageSnapshotSummary.Read(file);Assert(summary.Metadata.Nodes.Count==0&&summary.Bytes==s.Nodes["0"].Bytes&&summary.Count==299,"status summary reads correct bytes/count without building graph");var loaded=StorageSnapshot.Load(file);Assert(loaded.Nodes.Count==s.Nodes.Count&&ReferenceEquals(loaded.Nodes["1"].Parent,loaded.Nodes["0"].Id),"full index roundtrip canonicalizes parent identifiers");
   System.Threading.Tasks.Task save;
   lock(StorageSnapshot.IndexFileGate){using(var held=File.OpenRead(file)){
    save=System.Threading.Tasks.Task.Run(()=>s.Save(file));
    Assert(!save.Wait(75),"scan publication waits while summary owns index read lock");
   }}
   save.GetAwaiter().GetResult();summary=StorageSnapshotSummary.Read(file);
   Assert(summary!=null&&summary.Metadata.Id==s.Id&&summary.Matches(file),"publication and metadata stamp remain consistent after concurrent read");
   string text;using(var f=File.OpenRead(file))using(var zip=new GZipStream(f,CompressionMode.Decompress))using(var reader=new StreamReader(zip))text=reader.ReadToEnd();string old=text.Replace(",\"count\":299","");using(var f=File.Create(file))using(var zip=new GZipStream(f,CompressionMode.Compress))using(var writer=new StreamWriter(zip))writer.Write(old);summary=StorageSnapshotSummary.Read(file);Assert(summary!=null&&!summary.Count.HasValue&&summary.Bytes==s.Nodes["0"].Bytes,"older framed indexes provide honest unknown count without full graph");
   var lines=text.Split('\n');using(var f=File.Create(file))using(var zip=new GZipStream(f,CompressionMode.Compress))using(var writer=new StreamWriter(zip))writer.Write(string.Join("\n",lines.Take(4))+"\ninvalid unread tail");summary=StorageSnapshotSummary.Read(file);Assert(summary!=null&&summary.Count==299,"metadata path does not deserialize the scan body");
   cache.Put("small",Make("a"),time);var big=Make("b");for(int i=300;i<501000;i++)big.Nodes.Add(i.ToString(),new StorageNode());cache.Put("big",big,time.AddSeconds(1));Assert(!cache.TryGetValue("small",out current)&&cache.TryGetValue("big",out current),"node budget evicts older graph and preserves active graph");
  }finally{Directory.Delete(root,true);}Console.WriteLine("RESOURCE_LIFETIME_TESTS=PASS");return 0;
 }
}
