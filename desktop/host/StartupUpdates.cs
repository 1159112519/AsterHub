using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public sealed partial class WorkbenchWindow {
 TaskCompletionSource<bool> startupAnswer;object startupOffer;string startupToken;
 object StartupUpdatePromptState(){return startupOffer;}
 object AnswerStartupUpdate(Dictionary<string,object> p){object token,accepted;if(startupAnswer==null||!p.TryGetValue("token",out token)||!String.Equals(Convert.ToString(token),startupToken,StringComparison.Ordinal)||!p.TryGetValue("accepted",out accepted)||!(accepted is bool))throw new InvalidOperationException("更新提示已过期，请重新检查更新。");var answer=startupAnswer;startupAnswer=null;startupToken=null;startupOffer=null;answer.TrySetResult((bool)accepted);return true;}
 void PublishStartupUpdate(object value){startupOffer=value;Send(new{type="startup-update",value=value});}
 readonly StartupUpdatePolicy startupUpdatePolicy=new StartupUpdatePolicy();
 object UpdatePreferenceRequest(string method,Dictionary<string,object> p){
  var value=UpdatePreferences.Load(WorkbenchHost.DataRoot);
  if(method=="updates.preferences.save"){
   object enabled;if(!p.TryGetValue("checkOnStartup",out enabled)||!(enabled is bool))throw new ArgumentException("启动检查设置无效。");
   value.checkOnStartup=(bool)enabled;value.Save(WorkbenchHost.DataRoot);
  }
  return new{checkOnStartup=value.checkOnStartup};
 }
 // Called by the host only after the trusted workbench has loaded. One attempt per process.
 async void BeginStartupUpdateCheck(){
  if(WorkbenchHost.SmokeRoot!=null||!startupUpdatePolicy.TryBegin(UpdatePreferences.Load(WorkbenchHost.DataRoot).checkOnStartup))return;
  bool confirmed=false;
  try{
   // This uses the same signature check, version gate, download hash and installer restart path as Settings.
   await ServiceRequest("updates.check",new Dictionary<string,object>());
   if(closed||shuttingDown||!StartupUpdatePolicy.ShouldOffer(Updates.Release,AppUpdates.DisplayVersion))return;
   var offeredRelease=Updates.Release;string version=offeredRelease.version;
   startupToken=Guid.NewGuid().ToString("N");startupAnswer=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var decision=startupAnswer.Task;
   PublishStartupUpdate(new{token=startupToken,version=version,state="offer"});
   if(!await decision)return;
   confirmed=true;
   if(!Object.ReferenceEquals(Updates.Release,offeredRelease))throw new InvalidOperationException("版本信息已更新，请在设置中重新检查更新。");
   PublishStartupUpdate(null);
   // A manual check cannot swap the release while this dispatcher continuation starts the download.
   await ServiceRequest("updates.download",new Dictionary<string,object>());

  }catch(Exception error){
   // Background check errors stay silent; an explicitly requested start failure is visible.
   if(confirmed&&!closed&&!shuttingDown)PublishStartupUpdate(new{state="error",message=error.Message});
  }
 }
}
