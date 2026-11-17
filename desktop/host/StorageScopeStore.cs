using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

// One immutable result per endpoint and normalized scope; a small pointer selects the current view.
public sealed class StorageScopeStore {
 readonly string directory;readonly object gate=new object();readonly Dictionary<string,StorageSnapshotSummary> summaries=new Dictionary<string,StorageSnapshotSummary>();
 public StorageScopeStore(string directory){this.directory=directory;}
 static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","");}
 string DeviceFolder(string device){return Path.Combine(directory,Hash(device).Substring(0,16));}
 public string LegacyFile(string device){return Path.Combine(directory,Hash(device)+".json.gz");}
 public string ScopeFile(StorageSnapshot snapshot){string root=StoragePaths.Resolve(snapshot.IsLocal,snapshot.Root,snapshot.Root).Replace('\\','/').TrimEnd('/');if(snapshot.IsLocal||StoragePaths.WindowsSftp(snapshot.Root))root=root.ToUpperInvariant();return Path.Combine(DeviceFolder(snapshot.DeviceId),Hash(snapshot.Endpoint+"\n"+root).Substring(0,32)+".json.gz");}
 public string ActiveFile(string device){lock(gate){string folder=DeviceFolder(device),pointer=Path.Combine(folder,"active.txt");if(File.Exists(pointer)){string name=File.ReadAllText(pointer).Trim();if(System.Text.RegularExpressions.Regex.IsMatch(name,@"^[A-F0-9]{32}\.json\.gz$")&&File.Exists(Path.Combine(folder,name)))return Path.Combine(folder,name);}return LegacyFile(device);}}
 public void Activate(string device,string path){lock(gate){string folder=DeviceFolder(device);Directory.CreateDirectory(folder);string pointer=Path.Combine(folder,"active.txt");if(path==LegacyFile(device)){if(File.Exists(pointer))File.Delete(pointer);return;}if(Path.GetDirectoryName(Path.GetFullPath(path))!=Path.GetFullPath(folder)||!File.Exists(path))throw new IOException("扫描记录不存在");string temp=pointer+".tmp";File.WriteAllText(temp,Path.GetFileName(path));if(File.Exists(pointer))File.Replace(temp,pointer,null);else File.Move(temp,pointer);}}
 internal StorageSnapshotSummary Summary(string path){lock(gate){StorageSnapshotSummary value;if(!summaries.TryGetValue(path,out value)||!value.Matches(path)){value=StorageSnapshotSummary.Read(path);if(value==null)summaries.Remove(path);else summaries[path]=value;}return value;}}
 public string[] Records(string device,string endpoint){lock(gate){string folder=DeviceFolder(device);var files=Directory.Exists(folder)?Directory.GetFiles(folder,"*.json.gz").ToList():new List<string>();string legacy=LegacyFile(device);if(File.Exists(legacy))files.Add(legacy);return files.Select(path=>new{Path=path,Summary=Summary(path)}).Where(x=>x.Summary!=null&&x.Summary.Metadata.DeviceId==device&&x.Summary.Metadata.Endpoint==endpoint).OrderByDescending(x=>x.Summary.Metadata.Created).GroupBy(x=>ScopeFile(x.Summary.Metadata),StringComparer.Ordinal).Select(group=>group.First().Path).ToArray();}}
 public string Find(string device,string endpoint,string id){return Records(device,endpoint).FirstOrDefault(path=>Summary(path).Metadata.Id==id);}
}
