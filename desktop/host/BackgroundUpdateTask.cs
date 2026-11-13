using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

// Owned by the native window, not by a WebView request or a settings panel.
internal sealed class BackgroundUpdateTask {
 internal sealed class Snapshot {
  public long revision{get;set;} public string state{get;set;} public double progress{get;set;}
  public string current{get;set;} public string latest{get;set;} public bool available{get;set;}
  public string notes{get;set;} public string publishedUtc{get;set;} public string error{get;set;}
  public bool ready{get;set;} public bool canCancel{get;set;}
 }
 sealed class Reporter:IProgress<double> {readonly Action<double> report;internal Reporter(Action<double> report){this.report=report;}public void Report(double value){report(value);}}
 readonly object gate=new object();readonly string current;
 readonly Func<CancellationToken,Task<ReleaseInfo>> check;
 readonly Func<ReleaseInfo,IProgress<double>,CancellationToken,Task<string>> download;
 readonly Action<Snapshot> changed;CancellationTokenSource cancellation;Task running;ReleaseInfo release;string package;
 string state="idle",error;double progress;long revision;
 internal BackgroundUpdateTask(string current,Func<CancellationToken,Task<ReleaseInfo>> check,Func<ReleaseInfo,IProgress<double>,CancellationToken,Task<string>> download,Action<Snapshot> changed){this.current=current;this.check=check;this.download=download;this.changed=changed;}
 bool Active {get{return state=="checking"||state=="downloading"||state=="cancelling"||state=="installing";}}
 Snapshot Copy(){return new Snapshot{revision=revision,state=state,progress=progress,current=current,latest=release==null?null:release.version,available=release!=null&&AppUpdates.CompareVersions(release.version,current)>0,notes=release==null?null:release.notes,publishedUtc=release==null?null:release.publishedUtc,error=error,ready=state=="ready",canCancel=state=="downloading"};}
 internal Snapshot Status(){lock(gate)return Copy();}
 void Publish(){Snapshot value;lock(gate){revision++;value=Copy();}if(changed!=null)changed(value);}
 internal ReleaseInfo Release {get{lock(gate)return release;}}
 internal async Task<Snapshot> Check(){
  lock(gate){if(Active)throw new InvalidOperationException("正在检查或下载，请稍候");state="checking";release=null;package=null;error=null;progress=0;}Publish();
  try{var value=await check(CancellationToken.None);lock(gate){release=value;state="checked";}Publish();return Status();}
  catch(Exception ex){lock(gate){state="error";error=AppUpdates.FriendlyError(ex);}Publish();throw new InvalidOperationException(Status().error);}
 }
 internal Snapshot Start(){
  lock(gate){
   // Repeated requests attach to the same task instead of starting another writer.
   if(state=="downloading"||state=="cancelling"||state=="ready")return Copy();
   if(Active)throw new InvalidOperationException("正在检查或安装，请稍候");
   if(release==null||AppUpdates.CompareVersions(release.version,current)<=0)throw new InvalidOperationException("请先检查是否有可用的新版本");
   state="downloading";error=null;progress=0;package=null;cancellation=new CancellationTokenSource();
   var owned=cancellation;var offered=release;
   running=Task.Run(()=>Run(offered,owned));
  }Publish();return Status();
 }
 async Task Run(ReleaseInfo offered,CancellationTokenSource owned){
  try{
   var path=await download(offered,new Reporter(value=>{bool publish=false;lock(gate){if(ReferenceEquals(cancellation,owned)&&state=="downloading"){progress=Math.Max(progress,Math.Min(99,value));publish=true;}}if(publish)Publish();}),owned.Token);
   lock(gate){owned.Token.ThrowIfCancellationRequested();package=path;progress=100;state="ready";}
  }catch(OperationCanceledException){lock(gate){package=null;state="cancelled";error=null;}}
  catch(Exception ex){lock(gate){package=null;if(owned.IsCancellationRequested){state="cancelled";error=null;}else{state="error";error=!(ex is InvalidDataException)&&UpdateDownload.IsRecoverable(ex)?"下载暂时中断，已完成的部分会保留，重试即可继续下载。":AppUpdates.FriendlyError(ex);}}}
  finally{lock(gate){if(ReferenceEquals(cancellation,owned))cancellation=null;}owned.Dispose();Publish();}
 }
 internal Snapshot Cancel(){
  lock(gate){if(state=="downloading"&&cancellation!=null){state="cancelling";cancellation.Cancel();}}
  Publish();return Status();
 }
 internal Task Stop(){Cancel();lock(gate)return running??Task.FromResult(true);}
 internal void Install(Action<string,ReleaseInfo> install){
  string path;ReleaseInfo value;
  lock(gate){if(state!="ready"||package==null||release==null)throw new InvalidOperationException("请先下载并验证可用更新");path=package;value=release;state="installing";error=null;}Publish();
  try{install(path,value);}catch(Exception ex){lock(gate){state="ready";error=ex.Message;}Publish();throw;}
 }
}
