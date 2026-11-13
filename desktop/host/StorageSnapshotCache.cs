using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Script.Serialization;

// Owned by storageGate. Keep only recently queried graphs; status polling is metadata-only.
internal sealed class StorageSnapshotCache {
 sealed class Entry { internal StorageSnapshot Value; internal DateTime Used; }
 readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>();
 readonly Dictionary<string,DateTime> leases=new Dictionary<string,DateTime>();
 internal void Lease(string id,bool active,DateTime now){if(active)leases[id]=now.AddSeconds(45);else leases.Remove(id);}
 internal readonly TimeSpan Idle=TimeSpan.FromSeconds(60);
 internal bool TryGetValue(string id,out StorageSnapshot value){Entry entry;if(entries.TryGetValue(id,out entry)){value=entry.Value;return true;}value=null;return false;}
 internal void Touch(string id,DateTime now){Entry entry;if(entries.TryGetValue(id,out entry))entry.Used=now;}
 internal void Put(string id,StorageSnapshot value,DateTime now){entries[id]=new Entry{Value=value,Used=now};foreach(var old in entries.Where(x=>x.Key!=id).OrderByDescending(x=>x.Value.Used).Skip(2).Select(x=>x.Key).ToArray())entries.Remove(old);long nodes=value.Nodes.Count;foreach(var old in entries.Where(x=>x.Key!=id).OrderByDescending(x=>x.Value.Used).ToArray()){nodes+=old.Value.Value.Nodes.Count;if(nodes>500000)entries.Remove(old.Key);}}
 internal void Remove(string id){entries.Remove(id);}
 internal long Trim(DateTime now,bool visible=true){long removed=0;foreach(var id in entries.Where(x=>now-x.Value.Used>=Idle&&(!visible||!leases.ContainsKey(x.Key)||leases[x.Key]<=now)).Select(x=>x.Key).ToArray()){removed+=entries[id].Value.Nodes.Count;entries.Remove(id);}foreach(var id in leases.Where(x=>x.Value<=now).Select(x=>x.Key).ToArray())leases.Remove(id);return removed;}
 internal bool Contains(StorageSnapshot value){return entries.Values.Any(x=>ReferenceEquals(x.Value,value));}
}
internal sealed class StorageSummaryRemoval {internal string Path;internal long Bytes;internal bool Directory;}
internal sealed class StorageSnapshotSummary {
 internal StorageSnapshot Metadata;internal long Bytes;internal int? Count;
 internal long FileLength,FileStamp;
 internal object View(){var s=Metadata;return new{id=s.Id,root=s.Root,created=s.Created.ToString("o"),partial=s.Partial,skipped=s.Skipped,accessDenied=s.AccessDenied,entryLimit=s.EntryLimit,entryLimitReached=s.EntryLimitReached,bytes=Bytes,count=Count};}
 internal StorageSnapshotSummary Overlay(IEnumerable<StorageSummaryRemoval> removed){
  var result=new StorageSnapshotSummary{Metadata=Metadata,Bytes=Bytes,Count=Count,FileLength=FileLength,FileStamp=FileStamp};
  var comparison=Metadata.IsLocal?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
  Func<string,string> key=p=>(Metadata.IsLocal?(p??"").Replace('\\','/'):(p??"")).TrimEnd('/');string root=key(Metadata.Root);
  var paths=removed.Select(x=>new{Key=key(x.Path),Entry=x}).GroupBy(x=>x.Key,Metadata.IsLocal?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal).Select(x=>x.First()).Where(x=>x.Key.Length>0&&(x.Key.Equals(root,comparison)||x.Key.StartsWith(root+"/",comparison)||root.StartsWith(x.Key+"/",comparison))).ToArray();
  foreach(var x in paths){if(x.Key.Equals(root,comparison)||root.StartsWith(x.Key+"/",comparison)){result.Bytes=0;result.Count=0;return result;}if(paths.Any(y=>y.Key.Length<x.Key.Length&&x.Key.StartsWith(y.Key+"/",comparison)))continue;result.Bytes=Math.Max(0,result.Bytes-Math.Max(0,x.Entry.Bytes));result.Count=x.Entry.Directory?null:result.Count.HasValue?(int?)Math.Max(0,result.Count.Value-1):null;}
  return result;
 }
 internal static StorageSnapshotSummary From(StorageSnapshot s){var n=s.Node("0");return new StorageSnapshotSummary{Metadata=s.MetadataOnly(),Bytes=n.Bytes,Count=n.Count};}
 internal void Stamp(string path){var f=new FileInfo(path);FileLength=f.Exists?f.Length:0;FileStamp=f.Exists?f.LastWriteTimeUtc.Ticks:0;}
 internal bool Matches(string path){var f=new FileInfo(path);return f.Exists&&f.Length==FileLength&&f.LastWriteTimeUtc.Ticks==FileStamp;}
 internal static StorageSnapshotSummary Read(string path){lock(StorageSnapshot.IndexFileGate)return ReadLocked(path);}
 static StorageSnapshotSummary ReadLocked(string path){
  if(!File.Exists(path))return null;
  try{
   using(var input=File.OpenRead(path))using(var zip=new GZipStream(input,CompressionMode.Decompress))using(var reader=new StreamReader(zip)){
    if(reader.Read()=='{'&&reader.Read()=='\n'){
     var json=new JavaScriptSerializer{RecursionLimit=20};string line=reader.ReadLine();
     if(string.IsNullOrEmpty(line)||!line.EndsWith(",",StringComparison.Ordinal)||reader.ReadLine()!="\"nodes\":[")return null;
     var header=json.Deserialize<Dictionary<string,object>>("{"+line.Substring(0,line.Length-1)+"}");var metadata=StorageSnapshot.ReadHeader(header);
     line=reader.ReadLine();if(line==null)return null;if(line.EndsWith(",",StringComparison.Ordinal))line=line.Substring(0,line.Length-1);
     var root=json.Deserialize<Dictionary<string,object>>(line);if(Convert.ToString(root["id"])!="0")return null;
     var result=new StorageSnapshotSummary{Metadata=metadata,Bytes=Convert.ToInt64(root["bytes"]),Count=header.ContainsKey("count")?(int?)Convert.ToInt32(header["count"]):null};result.Stamp(path);return result;
    }
   }
   // Historical single-JSON files keep their existing reader, once per file revision.
   var legacy=StorageSnapshot.Load(path);if(legacy==null)return null;var info=From(legacy);info.Stamp(path);return info;
  }catch(IOException){return null;}catch(ArgumentException){return null;}catch(InvalidOperationException){return null;}catch(FormatException){return null;}catch(InvalidCastException){return null;}catch(KeyNotFoundException){return null;}catch(OverflowException){return null;}
 }
}
