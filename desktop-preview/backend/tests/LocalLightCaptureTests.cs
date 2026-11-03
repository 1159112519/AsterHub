using System;using System.Collections.Generic;using System.Diagnostics;using System.Linq;using System.Threading;using System.Threading.Tasks;using System.Web.Script.Serialization;
public static class LocalLightCaptureTests {
 static int checks;static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
 static LocalLightCounters Counts(int n){return new LocalLightCounters{Idle=(ulong)(n*100),Kernel=(ulong)(n*100+100),User=(ulong)(n*100),TotalBytes=16000000000,AvailableBytes=6000000000,TimeUtc=DateTime.UtcNow};}
 public static int Main(){try{
  var got=new List<LocalLightReading>();int index=0;using(var reader=new LocalLightCapture(x=>got.Add(x),500,()=>Counts(++index),false)){
   reader.RequestRefresh();reader.RequestRefresh();Check(got.Count==2,"manual requests collect");Check(!got[0].Cpu.HasValue,"first CPU needs a baseline");Check(got[1].Cpu==50,"CPU uses counter delta rather than a WMI query");Check(got[1].TotalBytes==16000000000,"memory is direct bytes");Check(reader.LastSuccessful==got[1],"last good retained");
  }
  int active=0,max=0,calls=0;using(var reader=new LocalLightCapture(x=>Interlocked.Increment(ref calls),500,()=>{int n=Interlocked.Increment(ref active);Interlocked.Exchange(ref max,Math.Max(n,max));Thread.Sleep(80);Interlocked.Decrement(ref active);return Counts(1);},false)){
   Parallel.For(0,16,i=>reader.RequestRefresh());Check(max==1,"single flight");Check(calls==1,"overlapping callers skipped");Check(reader.Skipped>=1,"skip diagnostics");
  }
  bool fail=false;LocalLightReading good;using(var reader=new LocalLightCapture(x=>{},500,()=>{if(fail)throw new InvalidOperationException("probe failure");return Counts(1);},false)){
   reader.RequestRefresh();good=reader.LastSuccessful;fail=true;reader.RequestRefresh();Check(reader.LastSuccessful==good,"failure must not clear last successful values");Check(!string.IsNullOrEmpty(reader.Latest.Error),"failure represented without zeroes");Check(!reader.Latest.Cpu.HasValue,"failure CPU unknown");
  }
  using(var reader=new LocalLightCapture(x=>{},500,()=>{var c=Counts(1);c.CpuSupported=false;return c;},false)){reader.RequestRefresh();reader.RequestRefresh();Check(!reader.Latest.Cpu.HasValue&&reader.Latest.TotalBytes>0,"unsupported CPU scope must not publish a partial group as total");}
  int stopped=0;var stopReader=new LocalLightCapture(x=>stopped++,500,()=>Counts(1),false);stopReader.Dispose();stopReader.RequestRefresh();Check(stopped==0,"disposed reader cannot publish");
  var samples=new List<LocalLightReading>();var times=new List<double>();var start=Stopwatch.StartNew();using(var reader=new LocalLightCapture(x=>{lock(samples){samples.Add(x);times.Add(start.Elapsed.TotalMilliseconds);}},500)){Thread.Sleep(20500);}
  lock(samples){Check(samples.Count>=35&&samples.Count<=45,"native 500ms collector cadence");Check(samples.Skip(1).All(x=>x.Cpu.HasValue&&x.Cpu>=0&&x.Cpu<=100),"native CPU valid after baseline");Check(samples.All(x=>x.Error==null&&x.TotalBytes>0),"native memory valid");var cost=samples.Select(x=>x.DurationMs).OrderBy(x=>x).ToArray();var delta=times.Skip(1).Select((x,i)=>x-times[i]).OrderBy(x=>x).ToArray();Console.WriteLine(new JavaScriptSerializer().Serialize(new{passed=checks,source="real Windows kernel32 APIs",intervalMs=500,samples=samples.Count,costMedianMs=cost[cost.Length/2],costP95Ms=cost[(int)Math.Floor((cost.Length-1)*.95)],costMaxMs=cost.Last(),intervalMedianMs=delta[delta.Length/2],intervalP95Ms=delta[(int)Math.Floor((delta.Length-1)*.95)],intervalMinMs=delta.First(),intervalMaxMs=delta.Last()}));}
  return 0;
 }catch(Exception ex){Console.Error.WriteLine(ex.ToString());return 1;}}
}
