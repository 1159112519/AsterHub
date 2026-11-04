using System;
using System.Collections.Generic;
// Only slow GPU descriptor XML is cached. Consumers must not reuse cached live
// values (fan, current power limit, utilization, temperature, memory or processes).
public sealed class LocalGpuMetadataCache {
 sealed class Entry {public string Value;public DateTime Expires;}
 readonly object gate=new object();readonly Dictionary<string,Entry> entries=new Dictionary<string,Entry>(StringComparer.Ordinal);readonly Func<DateTime> clock;readonly int lifetimeMilliseconds;
 public LocalGpuMetadataCache(int lifetimeMilliseconds=30000,Func<DateTime> utcClock=null){this.lifetimeMilliseconds=Math.Max(1000,lifetimeMilliseconds);clock=utcClock??(()=>DateTime.UtcNow);}
 public string Read(string key,Func<string> load,out bool fresh){if(load==null)throw new ArgumentNullException("load");key=key??"";lock(gate){Entry entry;var now=clock();if(entries.TryGetValue(key,out entry)&&entry.Expires>now){fresh=false;return entry.Value;}string value;try{value=load()??"";}catch{value="";}if(entries.Count>=16&&!entries.ContainsKey(key))entries.Clear();entries[key]=new Entry{Value=value,Expires=clock().AddMilliseconds(lifetimeMilliseconds)};fresh=true;return value;}}
}