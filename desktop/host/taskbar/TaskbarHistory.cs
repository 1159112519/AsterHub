using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

// Ephemeral chart samples only. Never serialized with the user's layout.
internal sealed class TaskbarHistory {
 internal struct DetailPoint {
  internal readonly DateTime Time;internal readonly double Value;internal readonly string Unit;internal readonly bool BreakBefore;
  internal DetailPoint(DateTime time,double value,string unit,bool breakBefore=false){Time=time;Value=value;Unit=unit;BreakBefore=breakBefore;}
 }
 // This metadata is only needed by detail history. Keep it attached to the
 // sample object without extending the native widget's public snapshot contract.
 sealed class DetailClock {internal double FullSeconds,LightSeconds;internal DateTime FullTime,LightTime;internal bool Disconnected;}
 static readonly ConditionalWeakTable<TaskbarSnapshot,DetailClock> clocks=new ConditionalWeakTable<TaskbarSnapshot,DetailClock>();
 internal static void SetDetailClock(TaskbarSnapshot snapshot,double fullSeconds,DateTime fullTime,double lightSeconds,DateTime lightTime,bool disconnected){
  clocks.Add(snapshot,new DetailClock{FullSeconds=fullSeconds,FullTime=fullTime,LightSeconds=lightSeconds,LightTime=lightTime,Disconnected=disconnected});
 }
 sealed class DetailTrack {
  internal readonly Queue<DetailPoint> Points=new Queue<DetailPoint>();
  internal DateTime Observed,LastGood;
  internal double Cadence=3;
  internal int Misses;
  internal bool Disconnected;
 }
 readonly Dictionary<string,DetailTrack> detail=new Dictionary<string,DetailTrack>();DateTime newest;
 sealed class Track {
  internal DateTime Stamp;internal string Unit;internal double[] Raw=new double[0],To=new double[0];internal double Scale=1,Duration=.75;
  internal long Start;
 }
 readonly Dictionary<string,Track> tracks=new Dictionary<string,Track>();
 internal long Version;
 static double Progress(Track t){return Math.Max(0,Math.Min(1,(Stopwatch.GetTimestamp()-t.Start)/(t.Duration*Stopwatch.Frequency)));}
 internal void Clear(){tracks.Clear();detail.Clear();newest=DateTime.MinValue;Version++;}
 internal void Observe(TaskbarSnapshot snapshot){
  DetailClock clock;clocks.TryGetValue(snapshot,out clock);
  // A missing GPU or interface can disappear from the current value map; use
  // the full acquisition stamp for tracked metrics that did not return a value.
  foreach(var key in detail.Keys.Concat(snapshot.values.Keys).Distinct().Where(TaskbarDetailContent.Plots).ToArray()){
   var v=snapshot.Read(key);var stamp=v.timeUtc;
   bool valid=!v.stale&&v.value.HasValue&&!double.IsNaN(v.value.Value)&&!double.IsInfinity(v.value.Value);
   if(!valid&&clock!=null&&clock.FullTime>stamp)stamp=clock.FullTime;
   if(stamp==DateTime.MinValue)continue;
   DetailTrack track;if(!detail.TryGetValue(key,out track))detail[key]=track=new DetailTrack();
   if(stamp<=track.Observed)continue;
   track.Observed=stamp;if(stamp>newest)newest=stamp;
   if(!valid){track.Misses++;track.Disconnected|=clock!=null&&clock.Disconnected;continue;}
   double cadence=clock==null?3:stamp==clock.LightTime?clock.LightSeconds:clock.FullSeconds;
   bool split=track.LastGood!=DateTime.MinValue&&(track.Disconnected||track.Misses>1||(stamp-track.LastGood).TotalSeconds>TelemetryContinuity.GapSeconds(Math.Max(cadence,track.Cadence),track.Misses>0));
   track.Points.Enqueue(new DetailPoint(stamp,v.value.Value,v.unit,split));
   track.LastGood=stamp;track.Cadence=cadence;track.Misses=0;track.Disconnected=false;
   while(track.Points.Count>0&&(track.Points.Count>640||(newest-track.Points.Peek().Time).TotalSeconds>60))track.Points.Dequeue();
  }
 }
 internal void Add(TaskbarSnapshot snapshot,bool observe=true){if(observe)Observe(snapshot);
  foreach(var entry in snapshot.values){var v=entry.Value;Track t;
   if(v.stale||!v.value.HasValue||double.IsNaN(v.value.Value)||double.IsInfinity(v.value.Value)){if(tracks.Remove(entry.Key))Version++;continue;}
   if(!tracks.TryGetValue(entry.Key,out t)||t.Unit!=v.unit){tracks[entry.Key]=t=new Track{Unit=v.unit};}
   if(v.timeUtc<=t.Stamp&&t.Raw.Length>0)continue;
   if(t.Raw.Length==0)t.Scale=Math.Max(1,Math.Abs(v.value.Value));
   double elapsed=t.Start==0?1:(Stopwatch.GetTimestamp()-t.Start)/(double)Stopwatch.Frequency;
   t.Duration=Math.Min(.75,Math.Max(.12,elapsed*.85));
   t.Raw=t.Raw.Concat(new[]{v.value.Value}).Skip(Math.Max(0,t.Raw.Length-12)).ToArray();
   t.To=Fit(t.Raw,v.unit=="%",t.Scale);
   t.Stamp=v.timeUtc;t.Start=Stopwatch.GetTimestamp();Version++;
  }
 }
 static double[] Fit(double[] values,bool percent,double scale){if(values.Length<2)return new double[0];
  return Enumerable.Repeat(values[0],13-values.Length).Concat(values).Select(v=>percent?Math.Max(0,Math.Min(1,v/100)):Math.Max(0,v)/(Math.Max(0,v)+scale)).ToArray();
 }
 internal double[] Read(string id,bool instant){Track t;return tracks.TryGetValue(id,out t)?t.To:new double[0];}
 internal double Shift(string id,bool instant){Track t;if(instant||!tracks.TryGetValue(id,out t))return 1;return 1-Math.Pow(1-Progress(t),3);}
 internal DetailPoint[] Detail(string id){DetailTrack track;if(!detail.TryGetValue(id,out track))return new DetailPoint[0];return track.Points.Where(point=>(newest-point.Time).TotalSeconds<=60).ToArray();}
 internal DateTime DetailEnd{get{return newest;}}
 internal double[] Values(string id){Track t;return tracks.TryGetValue(id,out t)?t.Raw:new double[0];}
 internal bool Moving(string id){Track t;return tracks.TryGetValue(id,out t)&&t.To.Length>0&&Progress(t)<1;}
}
