using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Web.Script.Serialization;
using System.IO.Compression;


// Storage accepts drive labels only as roots; remote syntax follows the connected SFTP namespace.
public static class StoragePaths {
 public static bool WindowsSftp(string home){return System.Text.RegularExpressions.Regex.IsMatch(home??"",@"^/?[A-Za-z]:[/\\]");}
 public static string Style(bool local,string home){return local?"windows-local":WindowsSftp(home)?"windows-sftp":"sftp";}
 public static string Resolve(bool local,string home,string input){
  string path=string.IsNullOrWhiteSpace(input)?home:input;
  if(local&&System.Text.RegularExpressions.Regex.IsMatch(path,@"^[A-Za-z]:$"))path+="\\";
  if(!local&&WindowsSftp(home)){
   if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^[A-Za-z]:$"))path="/"+path+"/";
   else if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^[A-Za-z]:[/\\]"))path="/"+path.Replace('\\','/');
   else if(System.Text.RegularExpressions.Regex.IsMatch(path,@"^/[A-Za-z]:[/\\]"))path=path.Replace('\\','/');
  }
  string normalized=FileTransferCopy.Normalize(local,path);
  // Keep a trailing slash for a Windows drive root in the SFTP namespace.
  return !local&&System.Text.RegularExpressions.Regex.IsMatch(normalized,@"^/[A-Za-z]:$")?normalized+"/":normalized;
 }
}

// Bounded, immutable scan snapshots. UI reads only current children or paginated files.
public sealed class StorageNode {
 public string Id,Parent,Name;public bool Directory,Removed,Incomplete;public long Bytes;public DateTime Stamp;public int Count;
 string path;internal string LocalParentPath;List<string> children;
 public string Path{get{return LocalParentPath==null?path:System.IO.Path.Combine(LocalParentPath,Name);}set{path=value;LocalParentPath=null;}}
 internal void UseLocalParent(string parent){LocalParentPath=parent;path=null;}
 internal IEnumerable<string> ExistingChildren{get{return (IEnumerable<string>)children??Enumerable.Empty<string>();}}
 public List<string> Children{get{return children??(children=new List<string>());}}
 public object View(){return new{id=Id,parent=Parent,name=Name,path=Path,directory=Directory,bytes=Bytes,modified=Stamp.ToString("o"),count=Count,incomplete=Incomplete};}
}
public sealed class StorageSnapshot {
 internal static readonly object IndexFileGate=new object();
 public string Id,DeviceId,Root,Endpoint,Error="";public bool IsLocal,Partial;public DateTime Created;public readonly Dictionary<string,StorageNode> Nodes=new Dictionary<string,StorageNode>();public string[] Largest=new string[0];public int Skipped,AccessDenied,EntryLimit;public bool EntryLimitReached;
 public static string EndpointFor(Server server){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(server.IsLocal?"local":server.Host+":"+server.Port+":"+server.User)));}
 public StorageNode Node(string id){StorageNode n;if(!Nodes.TryGetValue(id??"0",out n)||n.Removed)throw new IOException("此项目已不在当前扫描结果中，请重新扫描");return n;}
 public StorageSnapshot MetadataOnly(){return new StorageSnapshot{Id=Id,DeviceId=DeviceId,Root=Root,Endpoint=Endpoint,IsLocal=IsLocal,Partial=Partial,Created=Created,Skipped=Skipped,AccessDenied=AccessDenied,EntryLimit=EntryLimit,EntryLimitReached=EntryLimitReached,Error=Error};}
 public void Aggregate(){foreach(var n in Nodes.Values.Where(x=>x.Directory)){n.Bytes=0;n.Count=0;}foreach(var n in Nodes.Values.OrderByDescending(x=>int.Parse(x.Id))){if(n.Removed)continue;StorageNode parent;if(n.Parent!=null&&Nodes.TryGetValue(n.Parent,out parent)){parent.Bytes+=n.Bytes;parent.Count+=n.Directory?n.Count:1;if(n.Incomplete)parent.Incomplete=true;}}foreach(var directory in Nodes.Values.Where(x=>x.Directory))directory.Children.Sort((a,b)=>{int order=Nodes[b].Bytes.CompareTo(Nodes[a].Bytes);return order!=0?order:StringComparer.Ordinal.Compare(a,b);});var files=Nodes.Values.Where(x=>!x.Directory&&!x.Removed).OrderByDescending(x=>x.Bytes);Largest=(IsLocal&&EntryLimit==VolumeEntryLimit?files.ThenBy(x=>x.LocalParentPath,StringComparer.Ordinal).ThenBy(x=>x.Name,StringComparer.Ordinal):files.ThenBy(x=>x.Path,StringComparer.Ordinal)).Select(x=>x.Id).ToArray();}
 // Keep the original JSON schema; newline framing lets large volume snapshots be read and written one node at a time.
 const int LegacyJsonLimit=160000000;
 internal const int VolumeEntryLimit=2500000;
 public void Save(string path,CancellationToken token=default(CancellationToken)){SaveAsync(path,token,null).GetAwaiter().GetResult();}
 internal async Task SaveAsync(string path,CancellationToken token,Func<Action,Task> publish){
  var header=new{id=Id,deviceId=DeviceId,root=Root,endpoint=Endpoint,isLocal=IsLocal,created=Created.ToString("o"),partial=Partial,skipped=Skipped,accessDenied=AccessDenied,entryLimit=EntryLimit,entryLimitReached=EntryLimitReached,count=Node("0").Count};
  var json=new JavaScriptSerializer{RecursionLimit=20};string metadata=json.Serialize(header);
  Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{
   using(var output=File.Create(temp))using(var zip=new GZipStream(output,CompressionLevel.Fastest))using(var writer=new StreamWriter(zip,new System.Text.UTF8Encoding(false),65536)){
    writer.NewLine="\n";writer.WriteLine("{");writer.Write(metadata.Substring(1,metadata.Length-2));writer.WriteLine(",");writer.WriteLine("\"nodes\":[");
    bool first=true;foreach(var n in Nodes.Values){if(n.Removed)continue;token.ThrowIfCancellationRequested();if(!first)writer.WriteLine(",");first=false;
     writer.Write(json.Serialize(new{id=n.Id,parent=n.Parent,name=n.Name,path=n.Path,directory=n.Directory,bytes=n.Bytes,stamp=n.Stamp.ToString("o"),incomplete=n.Incomplete}));}
    writer.WriteLine();writer.WriteLine("]}");
   }
   Action commit=()=>{lock(IndexFileGate){token.ThrowIfCancellationRequested();if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}};
   if(publish==null)commit();else await publish(commit).ConfigureAwait(false);
  }finally{if(File.Exists(temp))try{File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
 }
 internal static StorageSnapshot ReadHeader(Dictionary<string,object> obj){return new StorageSnapshot{Endpoint=obj.ContainsKey("endpoint")?Convert.ToString(obj["endpoint"]):"",Id=Convert.ToString(obj["id"]),DeviceId=Convert.ToString(obj["deviceId"]),Root=Convert.ToString(obj["root"]),IsLocal=Convert.ToBoolean(obj["isLocal"]),Created=DateTime.Parse(Convert.ToString(obj["created"]),null,System.Globalization.DateTimeStyles.RoundtripKind),Partial=Convert.ToBoolean(obj["partial"]),Skipped=Convert.ToInt32(obj["skipped"]),AccessDenied=obj.ContainsKey("accessDenied")?Convert.ToInt32(obj["accessDenied"]):0,EntryLimit=obj.ContainsKey("entryLimit")?Convert.ToInt32(obj["entryLimit"]):0,EntryLimitReached=obj.ContainsKey("entryLimitReached")&&Convert.ToBoolean(obj["entryLimitReached"])};}
 void ReadNode(Dictionary<string,object> n){var node=new StorageNode{Id=Convert.ToString(n["id"]),Parent=n["parent"]==null?null:Convert.ToString(n["parent"]),Name=Convert.ToString(n["name"]),Path=Convert.ToString(n["path"]),Directory=Convert.ToBoolean(n["directory"]),Bytes=Convert.ToInt64(n["bytes"]),Stamp=DateTime.Parse(Convert.ToString(n["stamp"]),null,System.Globalization.DateTimeStyles.RoundtripKind),Incomplete=Convert.ToBoolean(n["incomplete"])};StorageNode parent;if(node.Parent!=null&&Nodes.TryGetValue(node.Parent,out parent)){node.Parent=parent.Id;if(IsLocal&&!node.Directory)node.UseLocalParent(parent.Path);}Nodes.Add(node.Id,node);}
 public static StorageSnapshot Load(string path){
  if(!File.Exists(path))return null;
  try{
   StorageSnapshot s;
   using(var input=File.OpenRead(path))using(var zip=new GZipStream(input,CompressionMode.Decompress))using(var reader=new StreamReader(zip)){
    if(reader.Read()!='{')return null;
    if(reader.Peek()=='\n'){
     reader.Read();var json=new JavaScriptSerializer{RecursionLimit=20};string metadata=reader.ReadLine();
     if(string.IsNullOrEmpty(metadata)||!metadata.EndsWith(",",StringComparison.Ordinal)||reader.ReadLine()!="\"nodes\":[")return null;
     s=ReadHeader(json.Deserialize<Dictionary<string,object>>("{"+metadata.Substring(0,metadata.Length-1)+"}"));
     string line;while((line=reader.ReadLine())!=null&&line!="]}"){
      if(s.Nodes.Count>VolumeEntryLimit)return null;if(line.EndsWith(",",StringComparison.Ordinal))line=line.Substring(0,line.Length-1);
      s.ReadNode(json.Deserialize<Dictionary<string,object>>(line));
     }
     if(line==null)return null;
    }else{
     var buffer=new char[8192];var content=new System.Text.StringBuilder("{");int count;
     while((count=reader.Read(buffer,0,buffer.Length))>0){content.Append(buffer,0,count);if(content.Length>LegacyJsonLimit)return null;}
     var obj=new JavaScriptSerializer{MaxJsonLength=LegacyJsonLimit,RecursionLimit=20}.Deserialize<Dictionary<string,object>>(content.ToString());s=ReadHeader(obj);
     foreach(Dictionary<string,object> n in (System.Collections.IEnumerable)obj["nodes"])s.ReadNode(n);
    }
   }
   if(!s.Nodes.ContainsKey("0"))return null;
   foreach(var n in s.Nodes.Values)if(n.Parent!=null){var parent=s.Nodes[n.Parent];n.Parent=parent.Id;parent.Children.Add(n.Id);if(s.IsLocal&&!n.Directory)n.UseLocalParent(parent.Path);}
   s.Aggregate();return s;
  }catch{return null;}
 }
}
public sealed class StorageScanJob {
 public readonly object Gate=new object();public readonly CancellationTokenSource Cancel=new CancellationTokenSource();public readonly ManualResetEventSlim Resume=new ManualResetEventSlim(true);public readonly Stopwatch Clock=Stopwatch.StartNew();public readonly Server Device;public StorageSnapshot Result;
 public string Status="queued",Current="",Error="";public int Enumerated,Skipped;public long Bytes;public Task Work;
 public StorageScanJob(Server device,string root){Device=device.Copy();Result=new StorageSnapshot{Id=Guid.NewGuid().ToString("N"),DeviceId=DeviceRegistry.Id(device),Root=root,Endpoint=StorageSnapshot.EndpointFor(device),IsLocal=device.IsLocal,Created=DateTime.UtcNow};}
 internal void ReleaseResult(){lock(Gate){if(Work!=null&&Work.IsCompleted&&Result.Nodes.Count>0)Result=Result.MetadataOnly();}}
 public object View(){lock(Gate)return new{id=Result.Id,deviceId=Result.DeviceId,root=Result.Root,status=Status,enumerated=Enumerated,skipped=Skipped,bytes=Bytes,current=Current,seconds=(int)Clock.Elapsed.TotalSeconds,error=Error,partial=Result.Partial};}
 public async Task Run(Func<StorageSnapshot,CancellationToken,Task> publish=null){try{lock(Gate)Status="enumerating";using(var connection=new FileConnection(Device)){await connection.Connect(Cancel.Token).ConfigureAwait(false);string root=StoragePaths.Resolve(Device.IsLocal,connection.Home,Result.Root);FileTransferCopy.SafePath(connection,root);if(!connection.IsDirectory(root))throw new IOException("请选择可读取的文件夹");Result.Root=root;var top=new StorageNode{Id="0",Name=System.IO.Path.GetFileName(root.TrimEnd('/','\\')),Path=root,Directory=true,Stamp=DateTime.UtcNow};if(string.IsNullOrEmpty(top.Name))top.Name=root;Result.Nodes.Add(top.Id,top);bool volume=Device.IsLocal&&string.Equals(root.TrimEnd('\\','/'),Path.GetPathRoot(root).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase);Result.EntryLimit=volume?StorageSnapshot.VolumeEntryLimit:120000;var pending=new LinkedList<Tuple<StorageNode,int>>();pending.AddLast(Tuple.Create(top,0));int sequence=0;while(pending.Count>0){Cancel.Token.ThrowIfCancellationRequested();Resume.Wait(Cancel.Token);var next=pending.First.Value;pending.RemoveFirst();var parent=next.Item1;lock(Gate)Current=parent.Path;if(next.Item2>128){parent.Incomplete=true;Skipped++;continue;}
    try{foreach(var entry in Device.IsLocal?LocalStorageScan.Enumerate(parent.Path,Cancel.Token):connection.Enumerate(parent.Path,Cancel.Token)){Cancel.Token.ThrowIfCancellationRequested();Resume.Wait(Cancel.Token);if(entry.IsLink||Excluded(entry.FullPath,entry.Name)){parent.Incomplete=true;Skipped++;continue;}if(sequence>=Result.EntryLimit){Result.Partial=true;Result.EntryLimitReached=true;parent.Incomplete=true;foreach(var unscanned in pending)unscanned.Item1.Incomplete=true;pending.Clear();break;}var node=new StorageNode{Id=(++sequence).ToString(),Parent=parent.Id,Name=entry.Name,Path=entry.FullPath,Directory=entry.IsDirectory,Bytes=entry.IsDirectory?0:entry.Size,Stamp=entry.Modified.ToUniversalTime()};if(volume&&!node.Directory)node.UseLocalParent(parent.Path);Result.Nodes.Add(node.Id,node);parent.Children.Add(node.Id);if(node.Directory){var entryToScan=Tuple.Create(node,next.Item2+1);if(volume)pending.AddLast(entryToScan);else pending.AddFirst(entryToScan);}lock(Gate){Enumerated++;if(!node.Directory)Bytes+=node.Bytes;}}
    }catch(OperationCanceledException){throw;}catch(Exception ex){if(parent.Id=="0"||!connection.Connected)throw;parent.Incomplete=true;if(Device.IsLocal&&(ex is UnauthorizedAccessException||ex is System.Security.SecurityException||ex.HResult==unchecked((int)0x80070005)))Result.AccessDenied++;else lock(Gate)Skipped++;}}
   }
   Cancel.Token.ThrowIfCancellationRequested();lock(Gate)Status="aggregating";Result.Skipped=Skipped;Result.Partial=Result.Partial||Skipped>0||Result.AccessDenied>0;Result.Aggregate();if(publish!=null)await publish(Result,Cancel.Token).ConfigureAwait(false);lock(Gate){Status="ready";Current="";}
  }catch(OperationCanceledException){lock(Gate){Status="cancelled";Current="";}}catch(Exception ex){lock(Gate){Status="failed";Error=ex.GetBaseException().Message;Current="";}}finally{Clock.Stop();lock(Gate){if(Status=="failed"||Status=="cancelled")Result=Result.MetadataOnly();}}}
 public static bool Excluded(string path,string name){if(name.Equals(".asterhub-trash",StringComparison.OrdinalIgnoreCase)||name.Equals("$RECYCLE.BIN",StringComparison.OrdinalIgnoreCase)||name.Equals("System Volume Information",StringComparison.OrdinalIgnoreCase))return true;string p=path.Replace('\\','/').TrimEnd('/');return new[]{"/proc","/sys","/dev","/run"}.Any(x=>p==x||p.StartsWith(x+"/",StringComparison.Ordinal));}
}
