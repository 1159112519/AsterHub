using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
public sealed class DeviceReading {
 public Server Device;public Metrics Metrics;public DateTime TimeUtc;public int Interval,IntervalMs;
}
public sealed class DeviceCapture : IDisposable {
 readonly LocalResourceProcesses resourceProcesses=new LocalResourceProcesses();
 sealed class State {public Server Device;public int Version,Interval;public bool Busy;public DateTime Due=DateTime.MinValue,Started;public DeviceReading Latest,Good;public int Failures;}
 readonly object gate=new object();readonly HashSet<string> inflight=new HashSet<string>();readonly Dictionary<string,State> states=new Dictionary<string,State>();readonly Func<Server,Metrics> collect;readonly Action<DeviceReading> received;readonly ArchiveService archive;readonly System.Threading.Timer timer;readonly int parallelism;int interval=5,running;bool stopping;
 public int DeviceCount{get{lock(gate)return states.Count;}}public int ConnectedCount{get{lock(gate)return states.Values.Count(x=>x.Latest!=null&&string.IsNullOrEmpty(x.Latest.Metrics.Error));}}public int FailedCount{get{lock(gate)return states.Values.Count(x=>x.Latest!=null&&!string.IsNullOrEmpty(x.Latest.Metrics.Error));}}public int RunningCount{get{lock(gate)return running;}}
 public DeviceCapture(ArchiveService writer,Action<DeviceReading> callback,Func<Server,Metrics> provider=null,int concurrency=4){archive=writer;received=callback;collect=provider??Remote.Collect;parallelism=Math.Max(1,concurrency);timer=new System.Threading.Timer(Tick,null,250,250);}
 public void Configure(IEnumerable<Server> devices,int seconds){int legacy=Math.Max(2,Math.Min(120,seconds));Configure(devices,seconds,legacy*1000);}
 public void Configure(IEnumerable<Server> devices,int seconds,int localMilliseconds){lock(gate){if(stopping)return;interval=Math.Max(2,Math.Min(120,seconds));var desired=devices.Where(x=>x!=null).GroupBy(ArchiveStore.Key).Select(x=>x.First().Copy()).ToList();var keep=new HashSet<string>(desired.Select(ArchiveStore.Key));foreach(string key in states.Keys.Where(x=>!keep.Contains(x)).ToArray())states.Remove(key);foreach(var device in desired){string key=ArchiveStore.Key(device);int next=DeviceSamplingPolicy.FullIntervalMilliseconds(device.IsLocal,interval,localMilliseconds);State state;if(!states.TryGetValue(key,out state)){state=new State{Device=device,Interval=next};states[key]=state;}else{bool credentialsChanged=state.Device.KeyPath!=device.KeyPath||state.Device.AuthMode!=device.AuthMode||state.Device.ProtectedPassword!=device.ProtectedPassword||state.Device.Password!=device.Password||state.Device.IsLocal!=device.IsLocal;if(credentialsChanged){state.Version++;state.Latest=null;state.Good=null;state.Failures=0;state.Due=DateTime.MinValue;}else if(next!=state.Interval)state.Due=DateTime.UtcNow;state.Device=device;state.Interval=next;}}}Tick(null);}
 public int IntervalSeconds(Server device){lock(gate){State state;return device!=null&&states.TryGetValue(ArchiveStore.Key(device),out state)?(int)Math.Ceiling(state.Interval/1000.0):interval;}}
 public int IntervalMilliseconds(Server device){lock(gate){State state;return device!=null&&states.TryGetValue(ArchiveStore.Key(device),out state)?state.Interval:interval*1000;}}
 public DeviceReading Latest(Server device){if(device==null)return null;lock(gate){State state;return states.TryGetValue(ArchiveStore.Key(device),out state)?state.Latest:null;}}
 public DeviceReading LastSuccessful(Server device){if(device==null)return null;lock(gate){State state;return states.TryGetValue(ArchiveStore.Key(device),out state)?state.Good:null;}}
 public void RequestRefresh(){lock(gate){if(stopping)return;foreach(var state in states.Values)if(!state.Busy)state.Due=DateTime.MinValue;}Tick(null);}
 void Tick(object ignored){var jobs=new List<Tuple<string,State,Server,int,int>>();lock(gate){if(stopping)return;DateTime now=DateTime.UtcNow;foreach(var entry in states.Where(x=>!x.Value.Busy&&!inflight.Contains(x.Key)&&x.Value.Due<=now).OrderBy(x=>x.Value.Due).ToArray()){if(running>=parallelism)break;var state=entry.Value;state.Busy=true;state.Started=now;inflight.Add(entry.Key);running++;jobs.Add(Tuple.Create(entry.Key,state,state.Device.Copy(),state.Version,state.Interval));}}
 foreach(var job in jobs)Task.Run(()=>Collect(job));}
 // After a previously healthy connection misses one sample, try again within
 // a short bounded delay; cold and repeated failures keep the bounded backoff.
 public static double RetryDelaySeconds(int intervalMs,int failures,bool hadGood){
  double interval=intervalMs/1000.0;
  if(hadGood&&failures==1)return Math.Max(.75,Math.Min(2,interval/2));
  return Math.Min(120,Math.Max(5,interval)*Math.Pow(2,Math.Min(5,Math.Max(0,failures-1))));
 }
 void Collect(Tuple<string,State,Server,int,int> job){Metrics metrics;try{metrics=collect(job.Item3)??new Metrics{Error="采集未返回数据"};}catch(Exception e){metrics=new Metrics{Error=e.GetBaseException().Message};}if(archive!=null&&job.Item3.IsLocal&&string.IsNullOrEmpty(metrics.Error))metrics.ResourceProcesses=resourceProcesses.Sample();var reading=new DeviceReading{Device=job.Item3,Metrics=metrics,TimeUtc=DateTime.UtcNow,Interval=(int)Math.Ceiling(job.Item5/1000.0),IntervalMs=job.Item5};
 try{lock(gate){State current;bool same=states.TryGetValue(job.Item1,out current)&&object.ReferenceEquals(current,job.Item2);bool valid=same&&current.Version==job.Item4;if(valid){current.Latest=reading;if(string.IsNullOrEmpty(metrics.Error)){PeripheralMetrics.CalculateRates(metrics.Network,current.Good==null?null:current.Good.Metrics.Network);PeripheralMetrics.CalculateStorageRates(metrics,current.Good==null?null:current.Good.Metrics);current.Good=reading;current.Failures=0;}else current.Failures++;if(archive!=null)archive.Enqueue(reading.Device,reading.Metrics,reading.Interval,reading.TimeUtc);if(received!=null)try{received(reading);}catch{}}if(same){current.Busy=false;var due=current.Failures>0?DateTime.UtcNow.AddSeconds(RetryDelaySeconds(current.Interval,current.Failures,current.Good!=null)):current.Started.AddMilliseconds(current.Interval);current.Due=current.Version==job.Item4?(due>DateTime.UtcNow?due:DateTime.UtcNow):DateTime.MinValue;}}}
 finally{lock(gate){running--;inflight.Remove(job.Item1);Monitor.PulseAll(gate);}}
 }
 public bool Stop(int milliseconds=17000){lock(gate){stopping=true;}timer.Dispose();var until=DateTime.UtcNow.AddMilliseconds(milliseconds);lock(gate){while(running>0){int remaining=(int)(until-DateTime.UtcNow).TotalMilliseconds;if(remaining<=0)return false;Monitor.Wait(gate,remaining);}return true;}}
 public void Dispose(){if(Stop())SshAuthentication.CloseMonitoring();}
}

// Full capture already runs on a worker. Record a bounded top list at most once
// every ten seconds; a missing/racing process does not affect host telemetry.
internal sealed class LocalResourceProcesses {
 readonly System.Diagnostics.Stopwatch clock=System.Diagnostics.Stopwatch.StartNew();
 double last=-10;
 Dictionary<string,long> previous=new Dictionary<string,long>();
 internal List<ResourceProcess> Sample(){
  double now=clock.Elapsed.TotalSeconds,elapsed=now-last;if(elapsed<10)return null;
  var next=new Dictionary<string,long>();var rows=new List<ResourceProcess>();
  System.Diagnostics.Process[] processes;
  try{processes=System.Diagnostics.Process.GetProcesses();}catch(System.ComponentModel.Win32Exception){return null;}
  foreach(var process in processes)using(process){
   int pid=process.Id;if(pid==0)continue;
   using(var handle=ProcessMetadata.OpenRead(pid)){
    long created,total,old;if(!ProcessMetadata.TryReadTimes(handle,out created,out total))continue;
    string identity=pid+":"+DateTime.FromFileTimeUtc(created).Ticks;
    double? cpu=elapsed<=130&&previous.TryGetValue(identity,out old)&&total>=old?(double?)Math.Min(100,(total-old)*100/TimeSpan.TicksPerSecond/elapsed/Math.Max(1,Environment.ProcessorCount)):null;
    next[identity]=total;
    long? working=ProcessMetadata.ReadWorkingSet(handle);
    if(!working.HasValue&&!cpu.HasValue)continue;
    string name=null;
    try{name=process.ProcessName;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
    rows.Add(new ResourceProcess{Pid=pid,Name=name,Identity=identity,Cpu=cpu,ResidentBytes=working});
   }
  }
  previous=next;last=now;
  var ranked=rows.Where(p=>p.ResidentBytes.HasValue).OrderByDescending(p=>p.ResidentBytes.Value).Take(32).Concat(rows.Where(p=>p.Cpu.HasValue).OrderByDescending(p=>p.Cpu.Value).Take(32)).GroupBy(p=>p.Identity).Select(g=>g.First()).ToList();
  // Query paths only for retained rows and confirm creation time again in case the PID was reused.
  var buffer=new System.Text.StringBuilder(512);
  foreach(var row in ranked)using(var handle=ProcessMetadata.OpenRead(row.Pid)){
   long created,cpu;
   if(ProcessMetadata.TryReadTimes(handle,out created,out cpu)&&row.Identity==row.Pid+":"+DateTime.FromFileTimeUtc(created).Ticks)row.Path=ProcessMetadata.ReadImage(handle,buffer);
  }
  return ranked;
 }
}