using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;

// Each operation owns its SFTP connections; no client is shared across worker threads.
public sealed class PreviewFilePlan {
 public Server Source,Target; public string TargetRoot,Identity,Id=Guid.NewGuid().ToString("N"); public DateTime Expires=DateTime.UtcNow.AddMinutes(3);
 public readonly List<PreviewFileItem> Items=new List<PreviewFileItem>(); public readonly List<string> Names=new List<string>(); public long Bytes; public int Conflicts;
}
public sealed class PreviewFileItem {
 public string Source,Target,Hash;public bool Directory,Existed,Committed;public long Length,OldLength;public DateTime Stamp,OldStamp,NewStamp;
}
public sealed class PreviewFileJob {
 public string Id,Status="queued",Error="",Current="";public long Done;public int FilesDone;public bool CanUndo;public PreviewFilePlan Plan;public readonly CancellationTokenSource Cancel=new CancellationTokenSource();public readonly object Gate=new object();
 public object View(){lock(Gate)return new{id=Id,status=Status,error=Error,current=Current,bytes=Plan.Bytes,done=Done,filesDone=FilesDone,files=Plan.Items.Count(x=>!x.Directory),names=Plan.Names.ToArray(),canUndo=CanUndo};}
}
public static class PreviewFileCopy {
 public static string Normalize(bool local,string path){
  if(string.IsNullOrWhiteSpace(path)||path.Length>32700||path.IndexOf('\0')>=0)throw new IOException("请输入有效的绝对路径");
  if(local){path=path.Replace('/','\\');if(!Path.IsPathRooted(path)||(path.Length>=2&&path[1]==':'&&(path.Length<3||(path[2]!='\\'&&path[2]!='/')))||(path.StartsWith(@"\",StringComparison.Ordinal)&&!path.StartsWith(@"\\",StringComparison.Ordinal))||path.StartsWith(@"\\.\",StringComparison.Ordinal)||path.StartsWith(@"\\?\",StringComparison.Ordinal))throw new IOException("请输入普通的本机绝对路径");return Path.GetFullPath(path);}
  if(!path.StartsWith("/",StringComparison.Ordinal))throw new IOException("远端路径需要以 / 开头");var parts=path.Split(new[]{'/'},StringSplitOptions.RemoveEmptyEntries);if(parts.Any(x=>x==".."))throw new IOException("请使用不含 .. 的绝对路径");return "/"+string.Join("/",parts.Where(x=>x!="."));
 }
 public static void SafePath(FileConnection connection,string path){
  path=Normalize(connection.Device.IsLocal,path);string cursor=path;
  while(!string.IsNullOrEmpty(cursor)){if(connection.Exists(cursor)&&connection.IsLink(cursor))throw new IOException("此路径包含符号链接或链接目录，已停止操作");string parent=FileTools.Parent(connection.Device.IsLocal,cursor);if(parent==cursor)break;cursor=parent;}
 }
 static string BaseName(bool local,string path){return local?Path.GetFileName(path.TrimEnd('\\','/')):path.TrimEnd('/').Split('/').Last();}
 public static async Task<PreviewFilePlan> Prepare(Server source,Server target,string root,IEnumerable<string> paths,CancellationToken token){
  var plan=new PreviewFilePlan{Source=source.Copy(),Target=target.Copy(),TargetRoot=Normalize(target.IsLocal,root)};
  using(var input=new FileConnection(source))using(var output=new FileConnection(target)){
   await input.Connect(token).ConfigureAwait(false);await output.Connect(token).ConfigureAwait(false);SafePath(output,plan.TargetRoot);if(!output.IsDirectory(plan.TargetRoot))throw new IOException("目标文件夹不存在");
   var originals=paths.Distinct(source.IsLocal?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal).ToArray();if(originals.Length==0||originals.Length>256)throw new IOException("每次请选择 1 至 256 个项目");
   var destinations=new HashSet<string>(target.IsLocal?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
   foreach(var raw in originals){token.ThrowIfCancellationRequested();string path=Normalize(source.IsLocal,raw);SafePath(input,path);if(!input.Exists(path))throw new IOException("选中的源项目已不存在");string name=BaseName(source.IsLocal,path);if(string.IsNullOrEmpty(name))throw new IOException("不能复制整个根目录");if(target.IsLocal)FileTools.ValidateWindowsName(name);string destination=FileTools.Join(target.IsLocal,plan.TargetRoot,name);if(!destinations.Add(destination))throw new IOException("所选项目具有重复的目标名称，请分批复制");
    if(FileTools.Key(source)==FileTools.Key(target)){if(source.IsLocal)FileTools.RejectRecursiveLocalCopy(path,destination);else if(destination==path||destination.StartsWith(path.TrimEnd('/')+"/",StringComparison.Ordinal))throw new IOException("不能复制到自身或子目录");}
    plan.Names.Add(name);Add(plan,input,output,path,destination,0,token);
   }
  }return plan;
 }
 static void Add(PreviewFilePlan plan,FileConnection input,FileConnection output,string source,string target,int depth,CancellationToken token){
  token.ThrowIfCancellationRequested();if(depth>64||plan.Items.Count>=10000)throw new IOException("单次复制最多支持 10000 个项目、64 层目录，请分批操作");SafePath(input,source);SafePath(output,target);bool directory=input.IsDirectory(source),exists=output.Exists(target);
  if(exists&&directory!=output.IsDirectory(target))throw new IOException("目标存在不同类型的同名项目，请更换目标目录");
  var item=new PreviewFileItem{Source=source,Target=target,Directory=directory,Existed=exists};
  if(!directory){item.Length=input.Length(source);item.Stamp=input.Stamp(source);checked{plan.Bytes+=item.Length;}if(exists){item.OldLength=output.Length(target);item.OldStamp=output.Stamp(target);plan.Conflicts++;}}
  plan.Items.Add(item);if(directory){foreach(var child in input.List(source,token)){if(child.IsLink)throw new IOException("所选文件夹包含符号链接，请单独处理这些链接");if(plan.Target.IsLocal)FileTools.ValidateWindowsName(child.Name);string next=plan.Target.IsLocal?FileTools.LocalChild(plan.TargetRoot,target,child.Name):FileTools.Join(false,target,child.Name);Add(plan,input,output,child.FullPath,next,depth+1,token);}}
 }
 public static async Task Execute(PreviewFileJob job,bool overwrite){
  var plan=job.Plan;var token=job.Cancel.Token;lock(job.Gate)job.Status="running";
  using(var input=new FileConnection(plan.Source))using(var output=new FileConnection(plan.Target)){
   await input.Connect(token).ConfigureAwait(false);await output.Connect(token).ConfigureAwait(false);
   foreach(var item in plan.Items){token.ThrowIfCancellationRequested();SafePath(input,item.Source);SafePath(output,item.Target);if(item.Directory){if(output.Exists(item.Target)){if(!output.IsDirectory(item.Target))throw new IOException("目标目录已变化，复制已停止");}else output.MakeDirectory(item.Target);continue;}
    lock(job.Gate)job.Current=BaseName(plan.Source.IsLocal,item.Source);if(input.Length(item.Source)!=item.Length||input.Stamp(item.Source)!=item.Stamp)throw new IOException("源文件已变化，请刷新后重新复制");
    CheckTarget(output,item,overwrite);string part=FileTools.Join(plan.Target.IsLocal,FileTools.Parent(plan.Target.IsLocal,item.Target),".asterhub-copy-"+Guid.NewGuid().ToString("N")+".part");bool created=false;
    try{using(var read=input.OpenRead(item.Source))using(var write=output.OpenNew(part))using(var hash=SHA256.Create()){
      created=true;var buffer=new byte[131072];int count;long written=0;
      while((count=await read.ReadAsync(buffer,0,buffer.Length,token).ConfigureAwait(false))>0){token.ThrowIfCancellationRequested();await write.WriteAsync(buffer,0,count,token).ConfigureAwait(false);hash.TransformBlock(buffer,0,count,buffer,0);written+=count;lock(job.Gate)job.Done+=count;}
      hash.TransformFinalBlock(new byte[0],0,0);item.Hash=Convert.ToBase64String(hash.Hash);if(written!=item.Length)throw new IOException("源文件长度发生变化，复制已停止");
     }
     token.ThrowIfCancellationRequested();SafePath(output,item.Target);CheckTarget(output,item,overwrite);try{output.Commit(part,item.Target,item.Existed);}catch(NotSupportedException){throw new IOException("此 SFTP 服务器不支持安全原子覆盖，原文件已保留，请选择其他目标目录。");}created=false;item.NewStamp=output.Stamp(item.Target);item.Committed=true;lock(job.Gate)job.FilesDone++;
    }finally{if(created){try{output.RemoveOwnPart(part);}catch{throw new IOException("无法清理本次复制的临时文件，请检查目标目录中的 .asterhub-copy 文件");}}}
   }
  }lock(job.Gate){job.Status="completed";job.Current="";job.CanUndo=plan.Items.Any(x=>x.Committed&&!x.Existed);}
 }
 static void CheckTarget(FileConnection output,PreviewFileItem item,bool overwrite){bool exists=output.Exists(item.Target);if(exists!=item.Existed)throw new IOException("目标文件已变化，请重新确认复制");if(exists&&(!overwrite||output.IsDirectory(item.Target)||output.Length(item.Target)!=item.OldLength||output.Stamp(item.Target)!=item.OldStamp))throw new IOException("目标同名文件需要重新确认覆盖");}
 public static async Task Undo(PreviewFileJob job,CancellationToken token=default(CancellationToken)){
  using(var output=new FileConnection(job.Plan.Target)){await output.Connect(token).ConfigureAwait(false);foreach(var item in job.Plan.Items.Where(x=>x.Committed&&!x.Existed)){token.ThrowIfCancellationRequested();SafePath(output,item.Target);output.UndoCopy(item.Target,item.Hash,item.NewStamp,item.Length);item.Committed=false;}}
  lock(job.Gate){job.CanUndo=false;job.Status="undone";job.Error="";}
 }
}
public sealed partial class WorkbenchWindow {
 readonly Dictionary<string,PreviewFilePlan> filePlans=new Dictionary<string,PreviewFilePlan>();
 readonly Dictionary<string,PreviewFileJob> fileJobs=new Dictionary<string,PreviewFileJob>();readonly Dictionary<string,Task> fileJobTasks=new Dictionary<string,Task>();bool fileClosing;readonly object fileGate=new object();readonly SemaphoreSlim fileSlots=new SemaphoreSlim(2,2);readonly SemaphoreSlim fileReadSlots=new SemaphoreSlim(2,2);readonly CancellationTokenSource fileLifetime=new CancellationTokenSource();
 static string FileText(Dictionary<string,object> p,string key,string fallback=""){object value;return p.TryGetValue(key,out value)?Convert.ToString(value):fallback;}
 Server FileDevice(Dictionary<string,object> p,string side){if(side=="local")return local.Copy();if(side=="remote")return Requested(p).Copy();throw new ArgumentException("未知文件面板");}
 async Task<object> FilesRequest(string method,Dictionary<string,object> p){
  lock(fileGate){if(fileClosing&&method!="files.transfers"&&method!="files.cancel")throw new IOException("文件传输正在退出清理，请稍后重试");}
  if(method=="files.transfers"){lock(fileGate)return fileJobs.Values.Reverse().Select(x=>x.View()).ToArray();}
  if(method=="files.cancel"){string id=FileText(p,"id");lock(fileGate){PreviewFileJob job;if(fileJobs.TryGetValue(id,out job)){if(job.Status=="queued"||job.Status=="running")job.Cancel.Cancel();}else filePlans.Remove(id);}return true;}
  if(method=="files.undo"){PreviewFileJob job;Task work;lock(fileGate){if(!fileJobs.TryGetValue(FileText(p,"id"),out job)||!job.CanUndo||job.Status=="running"||job.Status=="queued"||job.Status=="undoing")throw new IOException("此传输没有可撤销的新文件");job.Status="undoing";job.CanUndo=false;work=RunFileUndo(job);fileJobTasks[job.Id]=work;}await work;return job.View();}
  if(method=="files.start"){
   PreviewFilePlan plan;bool overwrite=p.ContainsKey("overwrite")&&Convert.ToBoolean(p["overwrite"]);lock(fileGate){if(!filePlans.TryGetValue(FileText(p,"id"),out plan)||plan.Expires<DateTime.UtcNow)throw new IOException("复制确认已过期，请重新选择文件");if(plan.Conflicts>0&&!overwrite)throw new IOException("请先确认覆盖同名文件");if(fileJobs.Values.Count(x=>x.Status=="queued"||x.Status=="running")>=16)throw new IOException("传输队列已满，请等待现有任务完成");filePlans.Remove(plan.Id);}
   var current=Requested(p);if(plan.Identity!=FileTools.Key(current))throw new IOException("设备配置已变化，请重新选择文件");var job=new PreviewFileJob{Id=plan.Id,Plan=plan};lock(fileGate){foreach(var key in fileJobs.Where(x=>x.Value.Status!="running"&&x.Value.Status!="queued"&&x.Value.Status!="undoing").Select(x=>x.Key).Take(Math.Max(0,fileJobs.Count-49)).ToArray()){fileJobs.Remove(key);fileJobTasks.Remove(key);}fileJobs.Add(job.Id,job);fileJobTasks[job.Id]=RunFileJob(job,overwrite);}return job.View();
  }
  if(method=="files.pick"){
   string kind=FileText(p,"kind");if(kind=="folder"){using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="选择本机文件夹";dialog.ShowNewFolderButton=false;var owner=new FileDialogOwner(new System.Windows.Interop.WindowInteropHelper(this).Handle);return dialog.ShowDialog(owner)==System.Windows.Forms.DialogResult.OK?new[]{dialog.SelectedPath}:new string[0];}}
   var picker=new Microsoft.Win32.OpenFileDialog{Title="选择需要复制的本机文件",Multiselect=true,CheckFileExists=true};return picker.ShowDialog(this)==true?picker.FileNames:new string[0];
  }
  var selected=Requested(p);var side=FileText(p,"side","remote");var server=FileDevice(p,side);
  if(method=="files.list"){
   await fileReadSlots.WaitAsync(fileLifetime.Token);try{return await Task.Run(async()=>{using(var connection=new FileConnection(server)){await connection.Connect(fileLifetime.Token).ConfigureAwait(false);string path=FileText(p,"path");path=string.IsNullOrWhiteSpace(path)?connection.Home:PreviewFileCopy.Normalize(server.IsLocal,path);PreviewFileCopy.SafePath(connection,path);var rows=connection.List(path,fileLifetime.Token);return (object)new{path=path,parent=FileTools.Parent(server.IsLocal,path),isLocal=server.IsLocal,items=rows.Select(x=>new{name=x.Name,path=x.FullPath,directory=x.IsDirectory,link=x.IsLink,size=x.Size,modified=x.Modified.ToString("yyyy-MM-dd HH:mm"),permissions=x.Permissions}).ToArray()};}});}finally{fileReadSlots.Release();}
  }
  if(method=="files.prepare"){
   var target=FileDevice(p,FileText(p,"targetSide"));object raw;if(!p.TryGetValue("paths",out raw)||!(raw is IEnumerable)||raw is string)throw new IOException("请选择需要复制的项目");var paths=((IEnumerable)raw).Cast<object>().Select(Convert.ToString).ToArray();
   await fileReadSlots.WaitAsync(fileLifetime.Token);PreviewFilePlan plan;try{plan=await Task.Run(()=>PreviewFileCopy.Prepare(server,target,FileText(p,"targetPath"),paths,fileLifetime.Token));}finally{fileReadSlots.Release();}plan.Identity=FileTools.Key(selected);lock(fileGate){if(fileClosing)throw new OperationCanceledException(fileLifetime.Token);foreach(var key in filePlans.Where(x=>x.Value.Expires<DateTime.UtcNow).Select(x=>x.Key).ToArray())filePlans.Remove(key);if(filePlans.Count>=16)throw new IOException("请先关闭其他复制确认");filePlans.Add(plan.Id,plan);}return new{id=plan.Id,names=plan.Names.ToArray(),files=plan.Items.Count(x=>!x.Directory),folders=plan.Items.Count(x=>x.Directory),bytes=plan.Bytes,conflicts=plan.Conflicts};
  }
  throw new InvalidOperationException("未知文件操作");
 }
 async Task RunFileJob(PreviewFileJob job,bool overwrite){bool entered=false;try{await fileSlots.WaitAsync(job.Cancel.Token);entered=true;await Task.Run(()=>PreviewFileCopy.Execute(job,overwrite));}catch(OperationCanceledException){lock(job.Gate){job.Status="cancelled";job.Error="已取消。已完成的副本保留；可撤销本次新增的文件。";job.CanUndo=job.Plan.Items.Any(x=>x.Committed&&!x.Existed);}}catch(Exception ex){lock(job.Gate){job.Status="failed";job.Error=ex.GetBaseException().Message;job.CanUndo=job.Plan.Items.Any(x=>x.Committed&&!x.Existed);}}finally{if(entered)fileSlots.Release();}}
 async Task RunFileUndo(PreviewFileJob job){bool entered=false;try{await fileSlots.WaitAsync(fileLifetime.Token);entered=true;await Task.Run(()=>PreviewFileCopy.Undo(job,fileLifetime.Token));}catch(Exception ex){lock(job.Gate){job.Status="undo-failed";job.Error="撤销未全部完成；被修改或已覆盖的文件会保留。";job.CanUndo=job.Plan.Items.Any(x=>x.Committed&&!x.Existed);}throw new IOException("撤销未全部完成："+ex.GetBaseException().Message);}finally{if(entered)fileSlots.Release();}}
 async Task CleanupFiles(){
  Task[] pending;lock(fileGate){fileClosing=true;fileLifetime.Cancel();filePlans.Clear();foreach(var job in fileJobs.Values)job.Cancel.Cancel();pending=fileJobTasks.Values.ToArray();}
  // Keep the UI dispatcher alive while async workers close their streams and remove owned .part files.
  // Two 30-second SFTP operation timeouts plus a margin bound cancellation/part cleanup.
  var drained=Task.WhenAll(pending);var deadline=Task.Delay(TimeSpan.FromSeconds(65));
  if(await Task.WhenAny(drained,deadline)!=drained)throw new IOException("文件传输仍在清理临时文件，已暂缓退出；请稍后重试退出。");
  try{await drained;}catch{ /* Each worker records its own outcome; all streams have now exited. */ }
 }
 sealed class FileDialogOwner:System.Windows.Forms.IWin32Window{public IntPtr Handle{get;private set;}public FileDialogOwner(IntPtr handle){Handle=handle;}}
}




