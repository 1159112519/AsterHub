using System;using System.Collections.Generic;using System.Linq;using System.Threading;
// Bound the chart index while retaining actual archived points and peaks. Full records stay on disk.
public static class HistoryService {
 // Preserve actual acquisition gaps even after retaining only representative points.
 public static HashSet<long> Gaps(List<ArchivePoint> input,List<ArchivePoint> displayed){
  var selected=new HashSet<long>(displayed.Select(p=>p.T));var result=new HashSet<long>();ArchivePoint previous=null;bool pending=false;int missed=0;
  foreach(var p in input){
   pending|=p.GapBefore;
   if(!string.IsNullOrEmpty(p.Error)){missed++;pending|=missed>1||TelemetryContinuity.IsDisconnect(p.Error);continue;}
   if(previous!=null&&!(previous.RemoteSummary&&p.RemoteSummary)){
    double cadence=Math.Max(1,Math.Max(p.Interval,previous.Interval));
    double limit=TelemetryContinuity.GapSeconds(cadence,missed>0);
    if(p.T-previous.T>TimeSpan.FromSeconds(limit).Ticks)pending=true;
   }
   if(selected.Contains(p.T)){if(pending)result.Add(p.T);pending=false;}
   previous=p;missed=0;
  }return result;
 }
 // Agent rows are already thinned. Its explicit gap flag describes the original archive,
 // so distance to the nearest retained agent point is not a coverage test.
 public static List<ArchivePoint> MergeRecorder(List<ArchivePoint> local,List<ArchivePoint> remote){
  if(remote.Count==0)return local;
  var result=new List<ArchivePoint>(local.Count+remote.Count);int cursor=0;
  foreach(var next in remote){
   var previous=result.Count==0?null:result[result.Count-1];
   bool first=previous==null,broken=next.GapBefore,hasBridge=false,covered=true;
   while(cursor<local.Count&&local[cursor].T<next.T){
    var point=local[cursor++];if(!first&&!broken)continue;
    if(!first){
     if(!string.IsNullOrEmpty(point.Error)){if(TelemetryContinuity.IsDisconnect(point.Error))covered=false;continue;}
     if(double.IsNaN(point.Cpu)||double.IsInfinity(point.Cpu))continue;
     covered=covered&&WithinFallbackCadence(previous,point,point.Interval);hasBridge=true;
    }
    result.Add(point);previous=point;
   }
   if(cursor<local.Count&&local[cursor].T==next.T)cursor++;
   if(broken&&hasBridge&&covered&&WithinFallbackCadence(previous,next,previous.Interval))next.GapBefore=false;
   result.Add(next);
  }
  while(cursor<local.Count)result.Add(local[cursor++]);
  return result;
 }
 static bool WithinFallbackCadence(ArchivePoint a,ArchivePoint b,int interval){return b.T-a.T<=TimeSpan.FromSeconds(Math.Max(6,interval*3)).Ticks;}
 public static List<ArchivePoint> Thin(List<ArchivePoint> input){return Thin(input,CancellationToken.None);}
 public static List<ArchivePoint> Thin(List<ArchivePoint> input,CancellationToken cancel){
  if(input.Count<=3000){cancel.ThrowIfCancellationRequested();return input;}
  var result=new List<ArchivePoint>(3000);int size=(input.Count+299)/300;
  for(int start=0;start<input.Count;start+=size){
   cancel.ThrowIfCancellationRequested();int end=Math.Min(input.Count,start+size),firstError=-1,lastError=-1;
   int[] low={-1,-1,-1},high={-1,-1,-1};double[] min={double.PositiveInfinity,double.PositiveInfinity,double.PositiveInfinity},max={double.NegativeInfinity,double.NegativeInfinity,double.NegativeInfinity};
   for(int i=start;i<end;i++){var point=input[i];if(!string.IsNullOrEmpty(point.Error)){if(firstError<0)firstError=i;lastError=i;}
    for(int metric=0;metric<3;metric++){double value=metric==0?point.Cpu:metric==1?point.Gpu:point.Memory;if(double.IsNaN(value)||double.IsInfinity(value))continue;if(value<min[metric]){min[metric]=value;low[metric]=i;}if(value>max[metric]){max[metric]=value;high[metric]=i;}}
   }
   var selected=new HashSet<int>{start,end-1};for(int metric=0;metric<3;metric++){if(low[metric]>=0)selected.Add(low[metric]);if(high[metric]>=0)selected.Add(high[metric]);}if(firstError>=0){selected.Add(firstError);selected.Add(lastError);}
   var ordered=selected.ToArray();Array.Sort(ordered);foreach(int i in ordered)result.Add(input[i]);
  }return result;
 }
}
