using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
public sealed partial class WorkbenchWindow {
 ReleaseInfo checkedRelease;string downloadedUpdate;bool updateBusy,feedbackBusy;
 async Task<object> ServiceRequest(string method,Dictionary<string,object> p){
  if(method=="updates.check"){
   if(updateBusy)throw new InvalidOperationException("正在检查或下载，请稍候");updateBusy=true;
   try{var value=await AppUpdates.Check(CancellationToken.None);checkedRelease=value;return new{current=AppUpdates.DisplayVersion,latest=value.version,available=AppUpdates.CompareVersions(value.version,AppUpdates.DisplayVersion)>0,notes=value.notes,publishedUtc=value.publishedUtc};}catch(Exception ex){throw new InvalidOperationException(AppUpdates.FriendlyError(ex));}finally{updateBusy=false;}
  }
  if(method=="updates.download"){
   if(updateBusy)throw new InvalidOperationException("正在检查或下载，请稍候");if(checkedRelease==null||AppUpdates.CompareVersions(checkedRelease.version,AppUpdates.DisplayVersion)<=0)throw new InvalidOperationException("请先检查是否有可用的新版本");updateBusy=true;
   try{downloadedUpdate=await AppUpdates.DownloadTo(checkedRelease,new Progress<double>(v=>Send(new{type="update-progress",value=v})),CancellationToken.None,System.IO.Path.Combine(WorkbenchHost.DataRoot,"Updates"));return new{ready=true,version=checkedRelease.version};}catch(Exception ex){throw new InvalidOperationException(AppUpdates.FriendlyError(ex));}finally{updateBusy=false;}
  }
  if(method=="updates.install"){
   if(WorkbenchHost.SmokeRoot!=null)throw new InvalidOperationException("隔离验证实例不修改安装。更新安装由独立安装验证覆盖");
   if(updateBusy||checkedRelease==null||downloadedUpdate==null||AppUpdates.CompareVersions(checkedRelease.version,AppUpdates.DisplayVersion)<=0)throw new InvalidOperationException("请先下载并验证可用更新");
   lock(fileGate){foreach(var job in fileJobs.Values){lock(job.Gate){if(job.Status=="queued"||job.Status=="running"||job.Status=="undoing")throw new InvalidOperationException("文件传输尚未结束，请完成或取消传输后安装更新");}}}
   UpdateInstallation.Start(downloadedUpdate,checkedRelease);var scheduledExit=Dispatcher.BeginInvoke(new Action(RequestExit),System.Windows.Threading.DispatcherPriority.Background);return true;
  }
  if(method=="feedback.submit"){
   if(feedbackBusy)throw new InvalidOperationException("反馈正在提交，请稍候");var payload=new FeedbackPayload{appVersion=AppUpdates.DisplayVersion,platform="Windows",category=Convert.ToString(p["category"]),title=Convert.ToString(p["title"]),description=Convert.ToString(p["description"]),contact=Convert.ToString(p["contact"]),environment="Windows · AsterHub Preview"};FeedbackClient.Validate(payload);feedbackBusy=true;
   try{return new{id=await FeedbackClient.Submit(payload,CancellationToken.None)};}catch(Exception ex){throw new InvalidOperationException(FeedbackClient.FriendlyError(ex));}finally{feedbackBusy=false;}
  }
  throw new InvalidOperationException("未知服务操作");
 }
}
