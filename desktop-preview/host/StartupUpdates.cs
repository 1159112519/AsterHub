using System;
using System.Collections.Generic;
using System.Windows;

public sealed partial class WorkbenchWindow {
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
   if(closed||shuttingDown||!StartupUpdatePolicy.ShouldOffer(checkedRelease,AppUpdates.DisplayVersion))return;
   var offeredRelease=checkedRelease;string version=offeredRelease.version;
   var answer=MessageBox.Show(this,"发现新版本 "+version+"。是否现在下载并自动安装？安装后 AsterHub 将重新启动，设备配置与历史记录会保留。","AsterHub 更新",MessageBoxButton.YesNo,MessageBoxImage.Information,MessageBoxResult.No);
   if(answer!=MessageBoxResult.Yes)return;
   confirmed=true;
   if(!Object.ReferenceEquals(checkedRelease,offeredRelease))throw new InvalidOperationException("版本信息已更新，请在设置中重新检查更新。");
   // A manual check cannot swap the release while this dispatcher continuation starts the download.
   await ServiceRequest("updates.download",new Dictionary<string,object>());
   if(!closed&&!shuttingDown)await ServiceRequest("updates.install",new Dictionary<string,object>());
  }catch(Exception error){
   // A failed background check is silent; once installation is requested, failures must be visible.
   if(confirmed&&!closed&&!shuttingDown)MessageBox.Show(this,error.Message,"AsterHub 更新未完成",MessageBoxButton.OK,MessageBoxImage.Warning);
  }
 }
}
