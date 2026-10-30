using System;using System.Collections.Generic;using System.Linq;
// Bound the chart index while retaining actual archived points and peaks. Full records stay on disk.
public static class PreviewHistory {
 // Preserve actual acquisition gaps even after retaining only representative points.
 public static HashSet<long> Gaps(List<ArchivePoint> input,List<ArchivePoint> displayed){
  var selected=new HashSet<long>(displayed.Select(p=>p.T));var result=new HashSet<long>();ArchivePoint previous=null;bool pending=false;
  foreach(var p in input){if(previous!=null&&((p.T-previous.T)>TimeSpan.FromSeconds(Math.Max(15,Math.Max(p.Interval,previous.Interval)*3)).Ticks||!string.IsNullOrEmpty(previous.Error)||!string.IsNullOrEmpty(p.Error)))pending=true;
   if(selected.Contains(p.T)){if(pending)result.Add(p.T);pending=false;}previous=p;
  }return result;
 }
 public static List<ArchivePoint> Thin(List<ArchivePoint> input){
  if(input.Count<=3000)return input;var result=new List<ArchivePoint>();int size=(input.Count+299)/300;
  for(int start=0;start<input.Count;start+=size){var bucket=input.Skip(start).Take(size).ToArray();var chosen=new HashSet<ArchivePoint>{bucket[0],bucket[bucket.Length-1]};
   foreach(var metric in new Func<ArchivePoint,double>[]{p=>p.Cpu,p=>p.Gpu,p=>p.Memory}){var valid=bucket.Where(p=>!double.IsNaN(metric(p))&&!double.IsInfinity(metric(p))).OrderBy(metric).ToArray();if(valid.Length>0){chosen.Add(valid[0]);chosen.Add(valid[valid.Length-1]);}}
   var errors=bucket.Where(p=>!string.IsNullOrEmpty(p.Error)).ToArray();if(errors.Length>0){chosen.Add(errors[0]);chosen.Add(errors[errors.Length-1]);}result.AddRange(chosen.OrderBy(p=>p.T));
  }return result;
 }
}
