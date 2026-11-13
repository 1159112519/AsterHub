using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

public sealed class StorageTrashEntry {
 public string Id,DeviceId,Endpoint,Original,Held,Name,State="planned";public long Bytes;public DateTime Stamp,Created;public bool Restored,Directory;
}
public sealed partial class WorkbenchWindow {
 readonly object storageGate=new object();readonly Dictionary<string,StorageScanJob> storageScans=new Dictionary<string,StorageScanJob>();readonly StorageSnapshotCache storageSnapshots=new StorageSnapshotCache();readonly Dictionary<string,StorageSnapshotSummary> storageSummaries=new Dictionary<string,StorageSnapshotSummary>();System.Threading.Timer storageCacheTimer;readonly SemaphoreSlim storageQueries=new SemaphoreSlim(1,1),storageActions=new SemaphoreSlim(1,1);readonly List<Task> storageWrites=new List<Task>();bool storageClosing;
 string StorageFile(string deviceId){using(var sha=SHA256.Create()){string name=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(deviceId))).Replace("-","");return Path.Combine(WorkbenchHost.DataRoot,"Storage",name+".json.gz");}}
 static string StorageEndpoint(Server server){return StorageSnapshot.EndpointFor(server);}
 long ReleaseStorageResults(){long removed=0;foreach(var job in storageScans.Values)if(!storageSnapshots.Contains(job.Result)){int count=job.Result.Nodes.Count;job.ReleaseResult();if(job.Result.Nodes.Count==0)removed+=count;}return removed;}
 volatile bool storageWindowVisible=true;
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 long EvictUnusedStorage(){if(!storageQueries.Wait(0))return 0;try{lock(storageGate){long removed=storageSnapshots.Trim(DateTime.UtcNow,storageWindowVisible);removed+=ReleaseStorageResults();return removed;}}finally{storageQueries.Release();}}
 void TrimStorageCache(){long removed=EvictUnusedStorage();if(removed>=500000)GC.Collect(2,GCCollectionMode.Forced,false,false);}
 void CacheStorageSnapshot(string deviceId,StorageSnapshot snapshot){lock(storageGate){if(snapshot.Nodes.Count<=50000)storageSnapshots.Put(deviceId,snapshot,DateTime.UtcNow);else storageSnapshots.Remove(deviceId);storageSummaries.Remove(deviceId);ReleaseStorageResults();if(storageCacheTimer==null&&!storageClosing)storageCacheTimer=new System.Threading.Timer(_=>TrimStorageCache(),null,15000,15000);}}
 object StorageStatus(Server server,string deviceId){
  StorageSnapshotSummary summary;StorageScanJob job;
  lock(storageGate){storageSummaries.TryGetValue(deviceId,out summary);storageScans.TryGetValue(deviceId,out job);}
  string path=StorageFile(deviceId);
  if(summary==null||!summary.Matches(path)){
   summary=StorageSnapshotSummary.Read(path);
   lock(storageGate){if(summary==null)storageSummaries.Remove(deviceId);else storageSummaries[deviceId]=summary;}
  }
  if(summary!=null){var metadata=summary.Metadata;summary=summary.Overlay(ReadStorageTrash().Where(x=>x.DeviceId==deviceId&&x.Endpoint==metadata.Endpoint&&!x.Restored&&(x.State=="moved"||x.State=="deleted")&&x.Created>=metadata.Created).Select(x=>new StorageSummaryRemoval{Path=x.Original,Bytes=x.Bytes,Directory=x.Directory}));}
  QueueStorageIndex(server,deviceId);return new{deviceId=deviceId,preparation=StorageIndexState(deviceId),job=job==null?null:job.View(),snapshot=summary!=null&&summary.Metadata.DeviceId==deviceId&&summary.Metadata.Endpoint==StorageEndpoint(server)?summary.View():null,isLocal=server.IsLocal};
 }
 object[] StorageDirectoryRoots(Server server,string home,string current){
  if(server.IsLocal){var roots=new List<object>();foreach(var drive in DriveInfo.GetDrives()){try{var drivePath=drive.RootDirectory.FullName;var type=drive.DriveType;string label="";long? total=null,used=null;bool available=true;
   if(type!=DriveType.Network){available=drive.IsReady;if(available){label=drive.VolumeLabel;total=drive.TotalSize;used=Math.Max(0,drive.TotalSize-drive.TotalFreeSpace);}}
   roots.Add(new{name=drive.Name.TrimEnd('\\'),path=drivePath,label=label,kind=type.ToString(),totalBytes=total,usedBytes=used,available=available});}catch(IOException){}catch(UnauthorizedAccessException){}}return roots.ToArray();}
  if(StoragePaths.WindowsSftp(home))return new[]{home,current}.Select(x=>StoragePaths.Resolve(false,home,x)).Where(x=>System.Text.RegularExpressions.Regex.IsMatch(x,@"^/[A-Za-z]:/")).Select(x=>x.Substring(0,4)).Distinct(StringComparer.OrdinalIgnoreCase).Select(x=>(object)new{name=x.Substring(1,2),path=x}).ToArray();
  return new object[]{new{name="远端根目录",path="/"}};
 }
 async Task<object> StorageRequest(string method,Dictionary<string,object> p){
  if(method=="storage.jobs"){lock(storageGate)return storageScans.Values.Select(x=>x.View()).Concat(storageIndexPreparations.Values.Select(x=>x.View())).ToArray();}
  if(method=="storage.cleanup.jobs"){lock(storageGate)return cleanupJobs.Values.Select(x=>x.View()).ToArray();}
  var server=devices.Find(FileText(p,"deviceId",devices.Selected)).Copy();string deviceId=DeviceRegistry.Id(server);
  lock(storageGate)if(storageClosing)throw new IOException("存储任务正在退出");
  if(method=="storage.lease"){lock(storageGate)storageSnapshots.Lease(deviceId,FileText(p,"active").Equals("true",StringComparison.OrdinalIgnoreCase),DateTime.UtcNow);return true;}
  if(method=="storage.trash.list")return await Task.Run(()=>DeviceTrash(server).Select(x=>new{id=x.Id,name=x.Name,path=x.Original,bytes=x.Bytes,directory=x.Directory,created=x.Created.ToString("o"),state=x.State}).ToArray());
  if(method=="storage.trash.delete"||method=="storage.trash.restore"){
   bool deleting=method=="storage.trash.delete";
   if(deleting&&!FileText(p,"confirm").Equals("true",StringComparison.OrdinalIgnoreCase))throw new IOException("请确认永久删除");
   var ids=new HashSet<string>(StorageIds(p,5000),StringComparer.Ordinal);
   var entries=await Task.Run(()=>DeviceTrash(server).Where(x=>ids.Contains(x.Id)).ToArray());
   if(entries.Length!=ids.Count)throw new IOException("回收区项目已变化，请刷新后重新选择");
   return QueueCleanup(server,deleting?"delete":"restore",entries.Select(x=>x.Name).ToArray(),(job,c)=>ChangeTrash(server,c,entries.Select(x=>x.Id).ToArray(),deleting,job),entries.Select(x=>x.Id).ToArray());
  }
  if(method=="storage.locations") {
   await fileReadSlots.WaitAsync(fileLifetime.Token);try{return await Task.Run(async()=>{using(var c=new FileConnection(server)){await c.Connect(fileLifetime.Token).ConfigureAwait(false);return (object)new{deviceId=deviceId,isLocal=server.IsLocal,home=c.Home,pathStyle=StoragePaths.Style(server.IsLocal,c.Home),roots=StorageDirectoryRoots(server,c.Home,FileText(p,"path",c.Home))};}});}finally{fileReadSlots.Release();}
  }
  if(method=="storage.directories") {
   await fileReadSlots.WaitAsync(fileLifetime.Token);try{return await Task.Run(async()=>{using(var c=new FileConnection(server)){await c.Connect(fileLifetime.Token).ConfigureAwait(false);string path=FileText(p,"path");path=StoragePaths.Resolve(server.IsLocal,c.Home,path);FileTransferCopy.SafePath(c,path);int offset;int.TryParse(FileText(p,"offset","0"),out offset);offset=Math.Max(0,Math.Min(100000,offset));var rows=c.Enumerate(path,fileLifetime.Token).Where(x=>x.IsDirectory&&!x.IsLink).Skip(offset).Take(201).ToArray();return (object)new{path=path,parent=FileTools.Parent(server.IsLocal,path),offset=offset,more=rows.Length>200,pathStyle=StoragePaths.Style(server.IsLocal,c.Home),roots=StorageDirectoryRoots(server,c.Home,path),items=rows.Take(200).Select(x=>new{name=x.Name,path=x.FullPath}).ToArray()};}});}finally{fileReadSlots.Release();}
  }
  if(method=="storage.scan"){
   StorageScanJob job;lock(storageGate){StorageScanJob previous;if(storageScans.TryGetValue(deviceId,out previous)&&new[]{"queued","enumerating","paused","aggregating"}.Contains(previous.Status))throw new IOException("此设备已有扫描，可暂停或取消后重试");foreach(var obsolete in storageScans.Where(x=>x.Key!=deviceId&&x.Value.Work!=null&&x.Value.Work.IsCompleted).Select(x=>x.Key).Take(Math.Max(0,storageScans.Count-15)).ToArray()){storageScans.Remove(obsolete);storageSnapshots.Remove(obsolete);}if(storageScans.Values.Count(x=>new[]{"queued","enumerating","paused","aggregating"}.Contains(x.Status))>=2)throw new IOException("最多同时扫描两台设备，请等待或取消现有扫描");job=new StorageScanJob(server,FileText(p,"path"));storageScans[deviceId]=job;job.Work=Task.Run(async()=>{await job.Run(async(snapshot,token)=>{try{await snapshot.SaveAsync(StorageFile(deviceId),token,async commit=>{await storageQueries.WaitAsync(token).ConfigureAwait(false);try{commit();ApplyTrashToSnapshot(snapshot,ReadStorageTrash());CacheStorageSnapshot(deviceId,snapshot);}finally{storageQueries.Release();}}).ConfigureAwait(false);}catch(OperationCanceledException){throw;}catch(Exception error){DiagnosticsLog.Write("storage-index-save",error);throw new IOException("无法保存扫描索引，已保留上次结果",error);}});if(job.Status=="ready")try{QueueStorageIndex(server,deviceId);}catch(Exception e){DiagnosticsLog.Write("storage-index-after-scan",e);}});}return job.View();
  }
  if(method=="storage.control"){StorageScanJob job;lock(storageGate){if(!storageScans.TryGetValue(deviceId,out job)||job.Result.Id!=FileText(p,"id"))throw new IOException("扫描任务已改变");}string action=FileText(p,"action");lock(job.Gate){if(action=="cancel"){job.Cancel.Cancel();job.Resume.Set();}else if(action=="pause"&&job.Status=="enumerating"){job.Resume.Reset();job.Status="paused";}else if(action=="resume"&&job.Status=="paused"){job.Status="enumerating";job.Resume.Set();}else throw new IOException("此任务当前无法执行该操作");}return job.View();}
  if(method=="storage.status")return await Task.Run(()=>StorageStatus(server,deviceId));
  await PrepareStorageIndex(server,deviceId);await storageQueries.WaitAsync(fileLifetime.Token);try{
   StorageSnapshot snapshot;lock(storageGate){storageSnapshots.TryGetValue(deviceId,out snapshot);storageSnapshots.Touch(deviceId,DateTime.UtcNow);}
   string indexFile=StorageFile(deviceId);
   if(snapshot==null&&await Task.Run(()=>StoragePagedIndex.Supported(indexFile))){
    using(var pages=await Task.Run(()=>StoragePagedIndex.Open(indexFile,fileLifetime.Token))){
     var metadata=pages.Metadata;if(metadata.DeviceId!=deviceId||metadata.Endpoint!=StorageEndpoint(server))throw new IOException("请先扫描一个文件夹");if(metadata.Id!=FileText(p,"snapshotId"))throw new IOException("扫描结果已更新，请重新选择项目");
     await Task.Run(()=>pages.ApplyRemoved(ReadStorageTrash().Where(x=>x.DeviceId==deviceId&&x.Endpoint==metadata.Endpoint&&!x.Restored&&(x.State=="moved"||x.State=="deleted")&&x.Created>=metadata.Created).Select(x=>new StorageSummaryRemoval{Path=x.Original,Bytes=x.Bytes,Directory=x.Directory}),fileLifetime.Token));NoteStorageIndexRead(deviceId);
     if(method=="storage.query")return await Task.Run(()=>pages.Query(p,fileLifetime.Token));
     return await StorageAction(server,deviceId,metadata,p,method,pages.Node);
    }
   }
   if(snapshot==null){snapshot=await Task.Run(()=>StorageSnapshot.Load(StorageFile(deviceId)));if(snapshot!=null&&snapshot.DeviceId==deviceId){await Task.Run(()=>ApplyTrashToSnapshot(snapshot,ReadStorageTrash()));CacheStorageSnapshot(deviceId,snapshot);}else snapshot=null;}
   if(snapshot!=null&&snapshot.Endpoint!=StorageEndpoint(server))snapshot=null;
   if(snapshot==null)throw new IOException("请先扫描一个文件夹");if(snapshot.Id!=FileText(p,"snapshotId"))throw new IOException("扫描结果已更新，请重新选择项目");
   if(method=="storage.query")return await Task.Run(()=>QueryStorage(snapshot,p));
   return await StorageAction(server,deviceId,snapshot,p,method,snapshot.Node);
  }finally{storageQueries.Release();}
 }
 async Task<object> StorageAction(Server server,string deviceId,StorageSnapshot snapshot,Dictionary<string,object> p,string method,Func<string,StorageNode> nodeFor){
   // Writes revalidate the selected configured endpoint and scanned identities.
   StorageScanJob owner;lock(storageGate)storageScans.TryGetValue(deviceId,out owner);if(owner!=null&&!SameTerminalConfiguration(owner.Device,server))throw new IOException("设备连接已改变，请重新扫描");
   if(method=="storage.trash"){
    var selected=await Task.Run(()=>StorageIds(p).Select(nodeFor).ToArray());
    if(selected.Any(x=>x.Parent==null))throw new IOException("不能移动扫描根目录");
    var roots=selected.Where(x=>!selected.Any(parent=>parent.Directory&&StorageIsChild(server,parent.Path,x.Path))).ToArray();
    return QueueCleanup(server,"move",roots.Select(x=>x.Name).ToArray(),(job,c)=>MoveToTrash(server,c,snapshot,roots,job),roots.Select(x=>x.Id).ToArray(),snapshot.Id);
   }
   if(method=="storage.copy"){
    var ids=StorageIds(p);var selected=await Task.Run(()=>ids.Select(nodeFor).ToArray());var target=await Task.Run(()=>nodeFor(FileText(p,"targetId","0")));if(!target.Directory)throw new IOException("粘贴目标必须是文件夹");
    await Task.Run(async()=>{using(var connection=new FileConnection(server)){await connection.Connect(fileLifetime.Token).ConfigureAwait(false);foreach(var n in selected)ValidateStorageNode(connection,n);}});
    var args=new Dictionary<string,object>{{"sourceDeviceId",deviceId},{"targetDeviceId",deviceId},{"sourceSide",server.IsLocal?"local":"remote"},{"targetSide",server.IsLocal?"local":"remote"},{"targetPath",target.Path},{"paths",selected.Select(x=>x.Path).ToArray()},{"keepBoth",true}};
    return await FilesRequest("files.prepare",args);
   }
   throw new IOException("未知存储操作");
 }
 static string[] StorageIds(Dictionary<string,object> p,int limit=256){object value;if(!p.TryGetValue("ids",out value)||!(value is IEnumerable))throw new IOException("请选择文件");var ids=((IEnumerable)value).Cast<object>().Select(Convert.ToString).Distinct().ToArray();if(ids.Length==0||ids.Length>limit)throw new IOException("每次请选择 1 至 "+limit+" 项");return ids;}
 static object QueryStorage(StorageSnapshot s,Dictionary<string,object> p){string query=FileText(p,"query").Trim();bool large=FileText(p,"mode")=="large";int offset;int.TryParse(FileText(p,"offset","0"),out offset);offset=Math.Max(0,offset);long min;long.TryParse(FileText(p,"minBytes","0"),out min);string kind=FileText(p,"kind");int days;int.TryParse(FileText(p,"olderThanDays","0"),out days);days=Math.Max(0,Math.Min(36500,days));var before=DateTime.UtcNow.AddDays(-days);bool foldersOnly=FileText(p,"foldersOnly").Equals("true",StringComparison.OrdinalIgnoreCase);var node=s.Node(FileText(p,"nodeId","0"));IEnumerable<StorageNode> items=large?s.Largest.Select(s.Node):node.Children.Select(id=>s.Nodes[id]).Where(n=>!n.Removed);object exclusions;var excluded=p.TryGetValue("excludeIds",out exclusions)?exclusions as System.Collections.IEnumerable:null;var skip=new HashSet<string>(StringComparer.Ordinal);if(excluded!=null&&!(excluded is string)){foreach(var id in excluded){if(skip.Count>=64)break;var key=id as string;if(key!=null)skip.Add(key);}}items=items.Where(n=>!skip.Contains(n.Id)&&(!foldersOnly||n.Directory)&&(days==0||n.Stamp.ToUniversalTime()<before)&&n.Bytes>=min&&(query.Length==0||n.Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||large&&n.Path.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)&&(kind.Length==0||StorageKind(n)==kind));int total=0;long bytes=0;var page=new List<StorageNode>(200);foreach(var n in items){if(total>=offset&&page.Count<200)page.Add(n);total++;bytes+=n.Bytes;}var crumbs=new List<object>();StorageNode cursor=node;while(cursor!=null){crumbs.Insert(0,cursor.View());cursor=cursor.Parent==null?null:s.Node(cursor.Parent);}return new{snapshotId=s.Id,node=node.View(),crumbs=crumbs.ToArray(),total=total,bytes=bytes,offset=offset,limit=200,items=page.Select(n=>new{id=n.Id,parent=n.Parent,name=n.Name,path=n.Path,directory=n.Directory,bytes=n.Bytes,modified=n.Stamp.ToString("o"),count=n.Count,kind=StorageKind(n),incomplete=n.Incomplete}).ToArray(),partial=s.Partial,skipped=s.Skipped};}
 static string StorageKind(StorageNode n){return StoragePagedIndex.Kind(n.Name,n.Directory);}
 static void ValidateStorageNode(FileConnection c,StorageNode n){FileTransferCopy.SafePath(c,n.Path);if(!c.Exists(n.Path)||c.IsDirectory(n.Path)!=n.Directory)throw new IOException("项目已变化，请重新扫描："+n.Name);if(!n.Directory&&(c.Length(n.Path)!=n.Bytes||Math.Abs((c.Stamp(n.Path)-n.Stamp).TotalSeconds)>1))throw new IOException("文件已被修改，已保留："+n.Name);}
 string StorageTrashFile{get{return Path.Combine(WorkbenchHost.DataRoot,"Storage","recycle.json");}}
 internal static readonly object StorageTrashFileGate=new object();
 List<StorageTrashEntry> ReadStorageTrash(){lock(StorageTrashFileGate)return ReadStorageTrashLocked();}
 List<StorageTrashEntry> ReadStorageTrashLocked(){if(!File.Exists(StorageTrashFile))return new List<StorageTrashEntry>();return new JavaScriptSerializer{MaxJsonLength=16000000}.Deserialize<List<StorageTrashEntry>>(File.ReadAllText(StorageTrashFile));}
 void SaveStorageTrash(List<StorageTrashEntry> entries){lock(StorageTrashFileGate)SaveStorageTrashLocked(entries);}
 void SaveStorageTrashLocked(List<StorageTrashEntry> entries){Directory.CreateDirectory(Path.GetDirectoryName(StorageTrashFile));string temp=StorageTrashFile+".tmp";File.WriteAllText(temp,new JavaScriptSerializer{MaxJsonLength=16000000}.Serialize(entries));if(File.Exists(StorageTrashFile))File.Replace(temp,StorageTrashFile,null);else File.Move(temp,StorageTrashFile);}
 sealed class CleanupJob {
  internal readonly object Gate=new object();internal string Id,DeviceId,Action,Status="queued",Error="";internal string[] Names,Targets;internal string Snapshot;internal int Done,Success;internal readonly List<object> Failures=new List<object>();internal DateTime Created=DateTime.UtcNow;internal DateTime? Finished;
  internal void Progress(string id,string name,Exception error){lock(Gate){Done++;if(error==null)Success++;else Failures.Add(new{id,name,error=error.GetBaseException().Message});}}
  internal object View(){lock(Gate)return new{id=Id,deviceId=DeviceId,action=Action,targets=Targets,snapshot=Snapshot,names=Names.Take(2).ToArray(),total=Names.Length,done=Done,succeeded=Success,status=Status,error=Error,failures=Failures.ToArray(),created=Created.ToString("o"),finished=Finished.HasValue?Finished.Value.ToString("o"):null};}
 }
 readonly Dictionary<string,CleanupJob> cleanupJobs=new Dictionary<string,CleanupJob>();
 static bool ActiveTrash(StorageTrashEntry x){return !x.Restored&&x.State!="failed"&&x.State!="deleted"&&x.State!="restored";}
 List<StorageTrashEntry> DeviceTrash(Server server){string device=DeviceRegistry.Id(server),endpoint=StorageEndpoint(server);return ReadStorageTrash().Where(x=>x.DeviceId==device&&x.Endpoint==endpoint&&ActiveTrash(x)).OrderByDescending(x=>x.Created).ToList();}
 object QueueCleanup(Server server,string action,string[] names,Action<CleanupJob,FileConnection> run,string[] targets,string snapshot=null){
  var job=new CleanupJob{Id=Guid.NewGuid().ToString("N"),DeviceId=DeviceRegistry.Id(server),Action=action,Names=names,Targets=targets,Snapshot=snapshot};
  lock(storageGate){
   if(storageClosing)throw new IOException("存储任务正在退出");
   if(cleanupJobs.Values.Count(x=>x.Status=="queued"||x.Status=="running")>=12)throw new IOException("待处理任务过多，请稍后重试");
   foreach(var old in cleanupJobs.Values.Where(x=>x.Finished.HasValue).OrderByDescending(x=>x.Created).Skip(99).ToArray())cleanupJobs.Remove(old.Id);
   cleanupJobs.Add(job.Id,job);storageWrites.RemoveAll(x=>x.IsCompleted);
   storageWrites.Add(Task.Run(async()=>{
    await storageActions.WaitAsync().ConfigureAwait(false);
    try{
     lock(job.Gate)job.Status="running";
     using(var c=new FileConnection(server)){
      await c.Connect(fileLifetime.Token).ConfigureAwait(false);
      run(job,c);
     }
    }catch(Exception error){lock(job.Gate)job.Error=error.GetBaseException().Message;}
    finally{
     lock(job.Gate){job.Status=job.Done==job.Names.Length&&job.Failures.Count==0&&job.Error.Length==0?"completed":job.Success>0?"partial":"failed";job.Finished=DateTime.UtcNow;}
     storageActions.Release();
    }
   }));
  }
  return job.View();
 }
 static bool StorageIsChild(Server server,string parent,string candidate){return candidate.Replace('\\','/').StartsWith(parent.Replace('\\','/').TrimEnd('/')+"/",server.IsLocal?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal);}
 static string PathKey(string path){return path.Replace('\\','/').TrimEnd('/');}
 static void ApplyTrashToSnapshot(StorageSnapshot snapshot,List<StorageTrashEntry> entries){
  var comparison=snapshot.IsLocal?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
  // Only committed moves and deletions are hidden. The underlying scan is kept so restoring can re-show it.
  var blocked=new HashSet<string>(entries.Where(x=>x.DeviceId==snapshot.DeviceId&&x.Endpoint==snapshot.Endpoint&&!x.Restored&&(x.State=="moved"||x.State=="deleted")&&(!string.IsNullOrEmpty(x.Original))&&x.Created>=snapshot.Created).Select(x=>PathKey(x.Original)),comparison);
  if(blocked.Count==0&&!snapshot.Nodes.Values.Any(x=>x.Removed))return;
  var pending=new Stack<StorageNode>();pending.Push(snapshot.Nodes["0"]);bool changed=false;
  while(pending.Count>0){
   var node=pending.Pop();bool hidden=node.Parent!=null&&(snapshot.Nodes[node.Parent].Removed||blocked.Contains(PathKey(node.Path)));
   if(hidden!=node.Removed){node.Removed=hidden;changed=true;}
   foreach(string id in node.ExistingChildren)pending.Push(snapshot.Nodes[id]);
  }
  if(changed)snapshot.Aggregate();
 }
 void RefreshTrashIndex(Server server,List<StorageTrashEntry> entries){
  storageQueries.Wait();try{StorageSnapshot current;lock(storageGate)storageSnapshots.TryGetValue(DeviceRegistry.Id(server),out current);if(current!=null)ApplyTrashToSnapshot(current,entries);}finally{storageQueries.Release();}
 }
 static void ProtectStoragePath(Server server,string path){
  string p=path.Replace('\\','/').TrimEnd('/');
  var roots=server.IsLocal?new[]{Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),AppDomain.CurrentDomain.BaseDirectory,WorkbenchHost.DataRoot}:new[]{"/etc","/usr","/bin","/sbin","/boot","/proc","/sys","/dev","/run","/var/lib","/var/log"};
  var compare=server.IsLocal?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
  foreach(string raw in roots.Where(x=>!string.IsNullOrEmpty(x))){string root=raw.Replace('\\','/').TrimEnd('/');if(p.Equals(root,compare)||p.StartsWith(root+"/",compare)||root.StartsWith(p+"/",compare))throw new IOException("此路径受保护，不能在清理工具中移动");}
  if(p.Split('/').Any(x=>x.Equals(".asterhub-trash",StringComparison.OrdinalIgnoreCase)||x.Equals("$Recycle.Bin",StringComparison.OrdinalIgnoreCase)||x.Equals("System Volume Information",StringComparison.OrdinalIgnoreCase)))throw new IOException("此路径受保护，不能在清理工具中移动");
 }
 void MoveToTrash(Server server,FileConnection c,StorageSnapshot snapshot,StorageNode[] selected,CleanupJob job){
  var manifest=ReadStorageTrash();string endpoint=StorageEndpoint(server);
  foreach(var node in selected){StorageTrashEntry entry=null;Exception failure=null;
   try{
    if(manifest.Count(ActiveTrash)>=5000)throw new IOException("回收区记录已达上限，请先恢复或清理已有项目");
    ValidateStorageNode(c,node);ProtectStoragePath(server,node.Path);
    if(node.Directory&&manifest.Any(x=>x.DeviceId==snapshot.DeviceId&&x.Endpoint==endpoint&&ActiveTrash(x)&&StorageIsChild(server,node.Path,x.Held)))throw new IOException("此文件夹内还有可恢复项目，请先恢复或清理这些项目");
    if(!node.Directory){if(server.IsLocal){using(var handle=new FileStream(node.Path,FileMode.Open,FileAccess.Read,FileShare.None)){} }else if(StorageKind(node)=="data")throw new IOException("数据库或日志文件可能正在使用，已保留");}
    string parent=FileTools.Parent(server.IsLocal,node.Path),trash=FileTools.Join(server.IsLocal,parent,".asterhub-trash");
    if(c.Exists(trash)){FileTransferCopy.SafePath(c,trash);if(!c.IsDirectory(trash))throw new IOException("回收区路径被占用");}else c.MakeDirectory(trash);
    string id=Guid.NewGuid().ToString("N"),folder=FileTools.Join(server.IsLocal,trash,id);c.MakeDirectory(folder);FileTransferCopy.SafePath(c,folder);
    if(server.IsLocal)try{File.SetAttributes(trash,File.GetAttributes(trash)|FileAttributes.Hidden);}catch(IOException){}catch(UnauthorizedAccessException){}
    entry=new StorageTrashEntry{Id=id,DeviceId=snapshot.DeviceId,Endpoint=endpoint,Name=node.Name,Original=node.Path,Held=FileTools.Join(server.IsLocal,folder,node.Name),Bytes=node.Bytes,Stamp=c.Stamp(node.Path),Created=DateTime.UtcNow,Directory=node.Directory};
    manifest.Add(entry);SaveStorageTrash(manifest);ValidateStorageNode(c,node);c.MoveRecoverable(node.Path,entry.Held);entry.State="moved";SaveStorageTrash(manifest);
   }catch(Exception ex){
    failure=ex;
    if(entry!=null){
     if(c.Exists(entry.Held)&&!c.Exists(entry.Original)){entry.State="moved";SaveStorageTrash(manifest);failure=null;}
     else{entry.State="failed";SaveStorageTrash(manifest);}
    }
   }
   job.Progress(node.Id,node.Name,failure);
  }
  RefreshTrashIndex(server,manifest);
 }
 static string CheckTrashEntry(Server server,StorageTrashEntry entry){
  string parent=FileTools.Parent(server.IsLocal,entry.Original),folder=FileTools.Join(server.IsLocal,FileTools.Join(server.IsLocal,parent,".asterhub-trash"),entry.Id);
  if(entry.Name.IndexOfAny(new[]{'/','\\'})>=0||entry.Name=="."||entry.Name==".."||FileTools.Join(server.IsLocal,folder,entry.Name)!=entry.Held||entry.Id.Length!=32||entry.Id.Any(x=>!Uri.IsHexDigit(x)))throw new IOException("恢复记录无效");
  return parent;
 }
 static void VerifyHeld(FileConnection c,StorageTrashEntry entry){
  FileTransferCopy.SafePath(c,entry.Held);
  if(!c.Exists(entry.Held)||c.IsDirectory(entry.Held)!=entry.Directory||(!entry.Directory&&c.Length(entry.Held)!=entry.Bytes)||Math.Abs((c.Stamp(entry.Held)-entry.Stamp).TotalSeconds)>1)throw new IOException("回收区项目已变化，已保留，请手动检查");
 }
 void ChangeTrash(Server server,FileConnection c,string[] ids,bool deleting,CleanupJob job){
  var manifest=ReadStorageTrash();var byId=manifest.Where(x=>x.DeviceId==job.DeviceId&&x.Endpoint==StorageEndpoint(server)&&ActiveTrash(x)).ToDictionary(x=>x.Id,StringComparer.Ordinal);
  foreach(string id in ids){
   StorageTrashEntry entry=null;Exception failure=null;
   try{
    if(!byId.TryGetValue(id,out entry))throw new IOException("回收记录不存在或已处理");
    string parent=CheckTrashEntry(server,entry);VerifyHeld(c,entry);
    if(deleting){
     if(entry.State!="moved")throw new IOException("此项目状态不确定，请先恢复");
     if(entry.Directory)DeleteHeldDirectory(c,entry.Held);else c.RemoveOwnPart(entry.Held);
     entry.State="deleted";
    }else{
     FileTransferCopy.SafePath(c,parent);
     if(!c.Exists(parent)||!c.IsDirectory(parent))throw new IOException("原文件夹已不存在，项目留在回收区");
     if(c.Exists(entry.Original))throw new IOException("原路径已有同名项目，已保留回收区项目");
     c.MoveRecoverable(entry.Held,entry.Original);entry.Restored=true;entry.State="restored";
    }
    SaveStorageTrash(manifest);
    string holder=FileTools.Parent(server.IsLocal,entry.Held);
    try{FileTransferCopy.SafePath(c,holder);c.RemoveEmptyDirectory(holder);}catch(Exception error){DiagnosticsLog.Write("storage-recycle-empty-holder",error);}
   }catch(Exception ex){failure=ex;}
   job.Progress(id,entry==null?"回收区项目":entry.Name,failure);
  }
  RefreshTrashIndex(server,manifest);
 }
 void DeleteHeldDirectory(FileConnection c,string root){
  // Preflight the entire tree before deleting anything. Never follow links or perform a recursive API delete.
  var pending=new Stack<string>();var directories=new List<string>();var files=new List<FileEntry>();pending.Push(root);
  while(pending.Count>0){
   string path=pending.Pop();FileTransferCopy.SafePath(c,path);directories.Add(path);
   foreach(var child in c.Enumerate(path,fileLifetime.Token)){
    if(child.IsLink||!StorageIsChild(c.Device,root,child.FullPath))throw new IOException("文件夹包含链接或异常路径，已保留");
    if(child.IsDirectory)pending.Push(child.FullPath);else files.Add(child);
    if(files.Count+directories.Count+pending.Count>250000)throw new IOException("文件夹项目过多，已保留");
   }
  }
  foreach(var file in files){
   FileTransferCopy.SafePath(c,file.FullPath);
   if(!c.Exists(file.FullPath)||c.IsDirectory(file.FullPath)||c.Length(file.FullPath)!=file.Size||Math.Abs((c.Stamp(file.FullPath)-file.Modified.ToUniversalTime()).TotalSeconds)>1)throw new IOException("文件夹内容发生变化，剩余项目已保留");
   c.RemoveOwnPart(file.FullPath);
  }
  foreach(string path in directories.AsEnumerable().Reverse()){FileTransferCopy.SafePath(c,path);c.RemoveEmptyDirectory(path);}
 }
 void CancelStorageForDevice(string deviceId){
  StorageScanJob job;
  lock(storageGate){storageScans.TryGetValue(deviceId,out job);StorageIndexPreparation preparation;if(storageIndexPreparations.TryGetValue(deviceId,out preparation))preparation.Cancel();}
  if(job==null)return;
  lock(job.Gate){job.Cancel.Cancel();job.Resume.Set();}
 }
 async Task CleanupStorage(){storageIndexLifetime.Cancel();Task[] writes,scans;lock(storageGate){storageClosing=true;if(storageCacheTimer!=null){storageCacheTimer.Dispose();storageCacheTimer=null;}foreach(var job in storageScans.Values){job.Cancel.Cancel();job.Resume.Set();}writes=storageWrites.ToArray();scans=storageScans.Values.Where(x=>x.Work!=null).Select(x=>x.Work).ToArray();}await Task.WhenAll(writes);var preparations=storageIndexPreparations.Values.Select(x=>x.Work).Concat(storageIndexStartup==null?new Task[0]:new[]{storageIndexStartup}).ToArray();await Task.WhenAny(Task.WhenAll(scans.Concat(preparations)),Task.Delay(3000));}
}
