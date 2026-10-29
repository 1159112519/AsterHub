using System;using System.Collections.Generic;using System.IO;using System.Text;
// Static inventory survives transient failures and restarts; no live utilization is fabricated.
public sealed class PreviewInventory {
 readonly string path;readonly object gate=new object();readonly Dictionary<string,HardwareInfo> cache;
 public PreviewInventory(string root){path=Path.Combine(root,"hardware.json");try{cache=ArchiveStore.Json().Deserialize<Dictionary<string,HardwareInfo>>(File.ReadAllText(path));}catch{cache=new Dictionary<string,HardwareInfo>();}if(cache==null)cache=new Dictionary<string,HardwareInfo>();}
 public Metrics Collect(Server server){var metrics=Remote.Collect(server);string id=PreviewDevices.Id(server);lock(gate){HardwareInfo previous;cache.TryGetValue(id,out previous);var current=metrics.Hardware;if(current!=null&&string.IsNullOrEmpty(current.Error)&&!string.IsNullOrWhiteSpace(current.CpuModel)){bool changed=previous==null||previous.CollectedUtc!=current.CollectedUtc;cache[id]=current;if(changed)try{string temp=path+".tmp";File.WriteAllText(temp,ArchiveStore.Json().Serialize(cache),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}catch(IOException){}catch(UnauthorizedAccessException){}}else if(previous!=null)metrics.Hardware=previous;}return metrics;}
}
