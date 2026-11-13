using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

internal static class SamplingContinuityTests {
 static int failures;
 static void Check(bool pass,string name){Console.WriteLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures++;}
 static void CheckSchedule(){
  Check(DeviceCapture.RetryDelaySeconds(120000,1,true)==2,"first miss retries quickly even at the slowest cadence");
  Check(DeviceCapture.RetryDelaySeconds(500,1,true)>=.75,"first miss has a quiet floor");
  Check(DeviceCapture.RetryDelaySeconds(500,1,false)==5&&DeviceCapture.RetryDelaySeconds(500,2,true)==10&&DeviceCapture.RetryDelaySeconds(120000,10,true)==120,"cold start and repeated failures keep bounded backoff");
  int attempt=0,active=0,overlap=0;var observed=new List<DeviceReading>();var stamps=new List<double>();var watch=Stopwatch.StartNew();var five=new ManualResetEventSlim();
  using(var capture=new DeviceCapture(null,r=>{lock(observed){observed.Add(r);stamps.Add(watch.Elapsed.TotalSeconds);if(observed.Count>=5)five.Set();}},device=>{
   if(Interlocked.Increment(ref active)>1)Interlocked.Increment(ref overlap);
   try{int i=Interlocked.Increment(ref attempt);Thread.Sleep(35);return i==1||i==3?new Metrics{Cpu="0%"}:new Metrics{Error="fixture counters temporarily unavailable"};}
   finally{Interlocked.Decrement(ref active);}
  })){
   var device=new Server{Host="localhost",User="fixture",Port=22,IsLocal=true};capture.Configure(new[]{device},3,500);
   Check(five.Wait(TimeSpan.FromSeconds(8)),"actual collector recovers once, then retries one miss before backoff");
   Thread.Sleep(1250);
   lock(observed){
    Check(observed.Count==5&&overlap==0,"persistent failure does not busy-loop or overlap");
    Check(observed.Count>=3&&observed[1].Metrics.Error!=""&&observed[2].Metrics.Cpu=="0%"&&stamps[2]-stamps[1]>=.7&&stamps[2]-stamps[1]<1.8,"one failed attempt is followed by a bounded retry and a real zero reading");
   }
   var good=capture.LastSuccessful(device);Check(good!=null&&good.Metrics.Cpu=="0%","failed attempts preserve last successful reading");
  }
 }
 static readonly DateTime zero=new DateTime(2026,11,10,0,0,0,DateTimeKind.Utc);
 static TaskbarSnapshot Snapshot(int seconds,double? value,bool hard=false){
  var time=zero.AddSeconds(seconds);var snap=new TaskbarSnapshot();
  foreach(var key in new[]{"gpu","down","up","storageRead","storageWrite"})snap.values[key]=new TaskbarValue{id=key,value=value,stale=!value.HasValue,unit=key=="gpu"?"%":"K/s",timeUtc=time};
  TaskbarHistory.SetDetailClock(snap,3,time,3,DateTime.MinValue,hard);return snap;
 }
 static void CheckTaskbar(){
  var h=new TaskbarHistory();h.Observe(Snapshot(0,0));h.Observe(Snapshot(3,null));h.Observe(Snapshot(3,null));h.Observe(Snapshot(5,10));h.Observe(Snapshot(7,null,true));h.Observe(Snapshot(9,20));h.Observe(Snapshot(26,30));h.Observe(Snapshot(27,null));h.Observe(Snapshot(28,null));h.Observe(Snapshot(29,35));
  foreach(var pair in new[]{new[]{"gpu","gpu"},new[]{"down","down","up"},new[]{"storage","storageRead","storageWrite"}}){
   var detail=new TaskbarDetailContent(pair[0],Snapshot(29,35),h);
   foreach(var series in detail.Series){Check(series.Length==5&&series[0].Value==0&&!series[1].BreakBefore&&series.Skip(2).All(p=>p.BreakBefore),pair[0]+" preserves real zero, one miss, explicit disconnect, long gap and repeated misses");}
  }
 }
 static int Main(){CheckSchedule();CheckTaskbar();return failures==0?0:1;}
}
