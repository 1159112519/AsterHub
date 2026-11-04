using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
public sealed partial class WorkbenchWindow {
 bool feedbackBusy;BackgroundUpdateTask backgroundUpdate;
 BackgroundUpdateTask Updates {
  get {return backgroundUpdate??(backgroundUpdate=new BackgroundUpdateTask(AppUpdates.DisplayVersion,AppUpdates.Check,(release,progress,cancel)=>AppUpdates.DownloadTo(release,progress,cancel,System.IO.Path.Combine(WorkbenchHost.DataRoot,"Updates")),value=>{
   if(!closed&&!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{if(!closed)Send(new{type="update-status",value=value});}));
  }));}
 }
 async Task<object> ServiceRequest(string method,Dictionary<string,object> p){
  if(method=="updates.status")return Updates.Status();
  if(method=="updates.cancel")return Updates.Cancel();
  if(method=="updates.check")return await Updates.Check();
  if(method=="updates.download")return Updates.Start();
  if(method=="updates.install"){
   if(WorkbenchHost.SmokeRoot!=null)throw new InvalidOperationException("隔离验证实例不修改安装。更新安装由独立安装验证覆盖");
   Updates.Install((path,release)=>{
    lock(fileGate){foreach(var job in fileJobs.Values){lock(job.Gate){if(job.Status=="queued"||job.Status=="running"||job.Status=="undoing")throw new InvalidOperationException("文件传输尚未结束，请完成或取消传输后安装更新");}}}
    UpdateInstallation.Start(path,release);Dispatcher.BeginInvoke(new Action(RequestExit),System.Windows.Threading.DispatcherPriority.Background);
   });return true;
  }
  if(method=="feedback.submit"){
   if(feedbackBusy)throw new InvalidOperationException("反馈正在提交，请稍候");var payload=new FeedbackPayload{appVersion=AppUpdates.DisplayVersion,platform="Windows",category=Convert.ToString(p["category"]),title=Convert.ToString(p["title"]),description=Convert.ToString(p["description"]),contact=Convert.ToString(p["contact"]),environment="Windows · AsterHub Preview"};FeedbackClient.Validate(payload);feedbackBusy=true;
   try{return new{id=await FeedbackClient.Submit(payload,CancellationToken.None)};}catch(Exception ex){throw new InvalidOperationException(FeedbackClient.FriendlyError(ex));}finally{feedbackBusy=false;}
  }
  throw new InvalidOperationException("未知服务操作");
 }
}
