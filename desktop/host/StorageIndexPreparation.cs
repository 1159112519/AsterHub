using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

// Background work consumes only persisted snapshots; it never scans a device or opens a network connection.
internal sealed class StorageIndexPreparation {
 readonly object gate=new object();readonly CancellationTokenSource cancel;
 internal readonly string DeviceId,Source,Revision,Root,Id="index-"+Guid.NewGuid().ToString("N");
 bool released;string derivedRevision;internal Task Work;string status="queued",phase="等待构建索引",error="";long processed;readonly DateTime created=DateTime.UtcNow;DateTime? finished;
 internal StorageIndexPreparation(string device,string source,string revision,string root,CancellationToken token){DeviceId=device;Source=source;Revision=revision;Root=root;cancel=CancellationTokenSource.CreateLinkedTokenSource(token);}
 internal void Start(SemaphoreSlim slots){Work=Task.Run(async()=>{bool acquired=false;try{await slots.WaitAsync(cancel.Token).ConfigureAwait(false);acquired=true;lock(gate){status="running";phase="正在检查索引";}using(var pages=StoragePagedIndex.Open(Source,cancel.Token,(count,text)=>{lock(gate){processed=count;phase=text;}})){}lock(gate){derivedRevision=FileRevision(Source+".pages.sqlite");status="ready";phase="索引已就绪";}}catch(OperationCanceledException){lock(gate){status="cancelled";phase="已取消索引构建";}}catch(Exception e){DiagnosticsLog.Write("storage-index-prepare",e);lock(gate){status="failed";phase="索引构建未完成";error=e.Message;}}finally{lock(gate){finished=DateTime.UtcNow;released=true;cancel.Dispose();}if(acquired)slots.Release();}});}
 internal static string FileRevision(string path){var info=new FileInfo(path);return info.Exists?info.Length+":"+info.LastWriteTimeUtc.Ticks:"missing";}
 internal bool CompletedReady{get{lock(gate)return status=="ready";}}
 internal bool Ready{get{lock(gate)return status=="ready"&&derivedRevision==FileRevision(Source+".pages.sqlite");}}
 internal void RememberValidatedRevision(){lock(gate)if(status=="ready")derivedRevision=FileRevision(Source+".pages.sqlite");}
 internal void Cancel(){lock(gate)if(!released)cancel.Cancel();}
 internal void ThrowIfFailed(){lock(gate){if(status=="failed")throw new IOException("索引构建失败："+error);if(status=="cancelled")throw new OperationCanceledException("索引构建已取消");}}
 internal object View(){lock(gate)return new{id=Id,deviceId=DeviceId,kind="index",root=Root,status=status,current=phase,enumerated=processed,error=error,created=created.ToString("o"),finished=finished.HasValue?finished.Value.ToString("o"):null,seconds=(int)((finished??DateTime.UtcNow)-created).TotalSeconds};}
}
public sealed partial class WorkbenchWindow {
 readonly Dictionary<string,StorageIndexPreparation> storageIndexPreparations=new Dictionary<string,StorageIndexPreparation>();
 readonly CancellationTokenSource storageIndexLifetime=new CancellationTokenSource();Task storageIndexStartup;
 static string StorageIndexRevision(string path){var info=new FileInfo(path);return info.Length+":"+info.LastWriteTimeUtc.Ticks;}
 StorageIndexPreparation QueueStorageIndex(Server server,string deviceId,bool retry=false){
  string source=StorageFile(deviceId);if(!File.Exists(source))return null;
  StorageSnapshotSummary summary;lock(storageGate)storageSummaries.TryGetValue(deviceId,out summary);if(summary==null||!summary.Matches(source))summary=StorageSnapshotSummary.Read(source);if(summary==null||summary.Metadata.DeviceId!=deviceId||summary.Metadata.Endpoint!=StorageEndpoint(server))return null;
  string revision=StorageIndexRevision(source);
  lock(storageGate){if(storageClosing)return null;StorageIndexPreparation old;if(storageIndexPreparations.TryGetValue(deviceId,out old)&&old.Revision==revision&&(!old.Work.IsCompleted||old.Ready||(old.CompletedReady&&storageQueries.CurrentCount==0)||(!retry&&!old.CompletedReady)))return old;
   if(!StoragePagedIndex.Supported(source))return null;
   if(old!=null&&!old.Work.IsCompleted)old.Cancel();
   foreach(var key in storageIndexPreparations.Where(x=>x.Value.Work.IsCompleted&&x.Key!=deviceId).Select(x=>x.Key).Take(Math.Max(0,storageIndexPreparations.Count-15)).ToArray())storageIndexPreparations.Remove(key);
   var job=new StorageIndexPreparation(deviceId,source,revision,summary.Metadata.Root,storageIndexLifetime.Token);storageIndexPreparations[deviceId]=job;job.Start(storageQueries);return job;
  }
 }
 void NoteStorageIndexRead(string deviceId){lock(storageGate){StorageIndexPreparation job;if(storageIndexPreparations.TryGetValue(deviceId,out job)&&job.Revision==StorageIndexRevision(job.Source))job.RememberValidatedRevision();}}
 object StorageIndexState(string deviceId){lock(storageGate){StorageIndexPreparation job;return storageIndexPreparations.TryGetValue(deviceId,out job)?job.View():null;}}
 void StartStorageIndexWarmup(){if(storageIndexStartup!=null)return;storageIndexStartup=Task.Run(async()=>{try{await Task.Delay(2000,storageIndexLifetime.Token).ConfigureAwait(false);foreach(var server in devices.All()){storageIndexLifetime.Token.ThrowIfCancellationRequested();try{var job=QueueStorageIndex(server.Copy(),DeviceRegistry.Id(server));if(job!=null)await job.Work.ConfigureAwait(false);}catch(OperationCanceledException){throw;}catch(Exception e){DiagnosticsLog.Write("storage-index-startup",e);}}}catch(OperationCanceledException){}catch(Exception e){DiagnosticsLog.Write("storage-index-startup",e);}});}
 async Task PrepareStorageIndex(Server server,string deviceId){var job=await Task.Run(()=>QueueStorageIndex(server,deviceId,true));if(job!=null){await job.Work.ConfigureAwait(false);job.ThrowIfFailed();}}
}
