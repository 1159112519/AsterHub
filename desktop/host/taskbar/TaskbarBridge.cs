using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
public sealed partial class WorkbenchWindow {
 TaskbarController taskbar;
 void StartTaskbar(){if(WorkbenchHost.SmokeRoot!=null&&!WorkbenchHost.SmokeTaskbar)return;taskbar=new TaskbarController(preferences.taskbar,ReadTaskbar,()=>Dispatcher.BeginInvoke(new Action(()=>{RestoreFromTray();Send(new{type="navigate",value="taskbar-settings"});})),()=>Dispatcher.BeginInvoke(new Action(RestoreFromTray)),()=>Dispatcher.BeginInvoke(new Action(()=>{if(desktop!=null)desktop.ShowTaskbarMenu(delta=>{if(taskbar!=null)taskbar.FlipPage(delta);},()=>{var copy=preferences.Copy();copy.taskbar=copy.taskbar.Normalize();copy.taskbar.enabled=false;copy.taskbar.layout["revision"]=checked(copy.taskbar.Revision+1);copy.Save();preferences=copy;if(taskbar!=null)taskbar.Configure(copy.taskbar);});})),(pageId,revision)=>Dispatcher.BeginInvoke(new Action(()=>{
  if(preferences.taskbar.Revision!=revision||preferences.taskbar.PageId(preferences.taskbar.page)==pageId)return;
  try{var copy=preferences.Copy();copy.taskbar=copy.taskbar.Normalize();if(!copy.taskbar.SetActive(pageId))return;copy.Save();preferences=copy;}
  catch(Exception ex){DiagnosticsLog.Write("taskbar-active-page",ex);}
 })),(metric,device)=>Dispatcher.BeginInvoke(new Action(()=>{
  string page=metric.StartsWith("cpu")?"cpu":metric.StartsWith("gpu")?"gpu":metric.StartsWith("memory")?"memory":metric=="up"||metric=="down"||metric=="ping"||metric=="loss"?"network":metric.StartsWith("storage")||metric=="io"?"storage":"overview";
  try{devices.Find(device);}catch{device="local";}
  RestoreFromTray();Send(new{type="navigate",value=new{target="taskbar-resource",page=page,deviceId=device}});
 })));}
 TaskbarSnapshot ReadTaskbar(){return ReadTaskbar(null);}
 TaskbarSnapshot ReadTaskbar(string previewDeviceId){
  var options=preferences.taskbar;Server server;try{server=devices.Find(string.IsNullOrEmpty(previewDeviceId)?options.deviceId:previewDeviceId);}catch{return new TaskbarSnapshot{device="设备不可用",error="请在任务栏设置中重新选择设备"};}var snap=new TaskbarSnapshot{device=server.Name,timeUtc=DateTime.UtcNow};var reading=capture.Latest(server);
  Metrics metrics=null;DateTime stamp=DateTime.MinValue;double seconds=capture.IntervalSeconds(server);
  if(reading!=null){metrics=reading.Metrics;stamp=reading.TimeUtc;}
  bool stale=metrics==null||!string.IsNullOrEmpty(metrics.Error)||(DateTime.UtcNow-stamp).TotalSeconds>Math.Max(15,seconds*3);
  Action<string,string,double?,string,int> add=(id,label,value,unit,decimals)=>{if(value>=1000&&(unit=="GB"||unit=="W"||id=="connections")){value=value/(unit=="GB"?1024:1000);unit=unit=="GB"?"TB":unit=="W"?"kW":"K";}snap.values[id]=new TaskbarValue{id=id,label=label,value=value,unit=unit,decimals=decimals,timeUtc=stamp,stale=stale,animate=!server.IsLocal||preferences.localIntervalMs>=1000};};
  Action<string,double?,string,int> detail=(id,value,unit,decimals)=>snap.detailValues[id]=new TaskbarValue{id=id,value=value,unit=unit,decimals=decimals,timeUtc=stamp,stale=stale};
  const double gib=1073741824;
  add("cpu","CPU",metrics==null?null:(double?)ResourceSample.ParsePercent(metrics.Cpu),"%",0);
  add("memory","内存",metrics==null?null:(double?)ResourceSample.MemoryPercent(metrics.Mem),"%",0);
  add("cpuFrequency","CPU",metrics==null?null:metrics.CpuFrequencyMHz/1000,"GHz",1);
  if(metrics!=null){
   if(!stale)snap.cpuCores=metrics.CpuCores.OrderBy(core=>core.Index).Select(core=>core.Util).ToArray();
   var memoryParts=System.Text.RegularExpressions.Regex.Matches(metrics.Mem??"",@"\d+(?:\.\d+)?");if(memoryParts.Count>=2){var memoryUsed=TaskbarNumber(memoryParts[0].Value);var memoryTotal=TaskbarNumber(memoryParts[1].Value);add("memoryUsed","已用",memoryUsed/1024,"GB",1);detail("memoryTotal",memoryTotal>0?memoryTotal/1024:null,"GB",1);add("memoryFree","可用",memoryTotal>=memoryUsed?(memoryTotal-memoryUsed)/1024:null,"GB",1);}

   var gpu=metrics.Gpus.OrderByDescending(g=>TaskbarNumber(g.Util)??-1).FirstOrDefault();
   add("gpu","GPU",gpu==null?null:TaskbarNumber(gpu.Util),"%",0);
   var used=gpu==null?null:TaskbarNumber(gpu.Used);var total=gpu==null?null:TaskbarNumber(gpu.Total);
   if(!stale&&gpu!=null)snap.detailGpu=gpu.Name;
   detail("gpuMemoryUsed",used/1024,"GB",1);detail("gpuMemoryTotal",total>0?total/1024:null,"GB",1);
   add("gpuMemory","显存",used.HasValue&&total>0?used/total*100:null,"%",0);
   add("gpuFrequency","GPU",gpu==null?null:TaskbarNumber(gpu.Clock)/1000,"GHz",1);
   add("gpuTemperature","GPU",gpu==null?null:TaskbarNumber(gpu.Temp),"°C",0);
   add("gpuPower","功耗",gpu==null?null:TaskbarNumber(gpu.Power),"W",0);
   add("gpuFan","风扇",gpu==null?null:TaskbarNumber(gpu.Fan),"%",0);
   var volume=metrics.Volumes.FirstOrDefault(v=>v.Path==metrics.PrimaryStoragePath)??metrics.Volumes.FirstOrDefault(v=>v.TotalBytes>0);
   add("storage","存储",volume==null?null:(double?)volume.UsedPercent,"%",0);add("storageFree","可用",volume==null?null:(double?)volume.AvailableBytes/1073741824,"GB",1);
   if(!stale&&volume!=null)snap.detailVolume=volume.Path;
   detail("storageUsed",volume==null?null:(double?)volume.UsedBytes/gib,"GB",1);
   detail("storageTotal",volume==null||volume.TotalBytes<=0?null:(double?)volume.TotalBytes/gib,"GB",1);
   var activeDisks=metrics.Storage.Where(d=>d.ActivePercent.HasValue).ToArray();
   add("io","负载",activeDisks.Length>0?(double?)activeDisks.Max(d=>d.ActivePercent.Value):null,"%",0);
   var diskReads=metrics.Storage.Where(d=>d.ReadBytesPerSecond.HasValue).ToArray();var diskWrites=metrics.Storage.Where(d=>d.WriteBytesPerSecond.HasValue).ToArray();TaskbarRate(add,"storageRead","R",diskReads.Length>0?(double?)diskReads.Sum(d=>d.ReadBytesPerSecond.Value):null);TaskbarRate(add,"storageWrite","W",diskWrites.Length>0?(double?)diskWrites.Sum(d=>d.WriteBytesPerSecond.Value):null);
   if(metrics.Network!=null){
    if(string.IsNullOrEmpty(metrics.Network.Error)&&metrics.Network.Interfaces.Count>0){detail("networkReceived",metrics.Network.Interfaces.Sum(n=>(double)n.ReceivedBytes)/gib,"GB",2);detail("networkSent",metrics.Network.Interfaces.Sum(n=>(double)n.SentBytes)/gib,"GB",2);}
    TaskbarRate(add,"down","↓",metrics.Network.DownloadBytesPerSecond);TaskbarRate(add,"up","↑",metrics.Network.UploadBytesPerSecond);add("connections","连接",metrics.Network.TcpConnections.HasValue?(double?)metrics.Network.TcpConnections.Value:null,"",0);}
  }
  var light=server.IsLocal?localLightLatest:null;
  if(light!=null&&(light.IntervalMs<1000||stale)&&string.IsNullOrEmpty(light.Error)&&(DateTime.UtcNow-light.TimeUtc).TotalSeconds<=Math.Max(15,preferences.localIntervalMs*.003)){
   if(light.Cpu.HasValue)snap.values["cpu"]=new TaskbarValue{id="cpu",label="CPU",value=light.Cpu,unit="%",timeUtc=light.TimeUtc,animate=light.IntervalMs>=1000};
   if(light.TotalBytes>0){snap.detailValues["memoryTotal"]=new TaskbarValue{id="memoryTotal",value=light.TotalBytes/gib,unit="GB",decimals=1,timeUtc=light.TimeUtc};snap.values["memory"]=new TaskbarValue{id="memory",label="内存",value=100.0*(light.TotalBytes-light.AvailableBytes)/light.TotalBytes,unit="%",timeUtc=light.TimeUtc,animate=light.IntervalMs>=1000};snap.values["memoryUsed"]=new TaskbarValue{id="memoryUsed",label="已用",value=(light.TotalBytes-light.AvailableBytes)/1073741824.0,unit="GB",decimals=1,timeUtc=light.TimeUtc,animate=light.IntervalMs>=1000};snap.values["memoryFree"]=new TaskbarValue{id="memoryFree",label="可用",value=light.AvailableBytes/1073741824.0,unit="GB",decimals=1,timeUtc=light.TimeUtc,animate=light.IntervalMs>=1000};}
  }
  TaskbarHistory.SetDetailClock(snap,reading==null?seconds:reading.IntervalMs/1000.0,stamp,light==null?seconds:light.IntervalMs/1000.0,light==null?DateTime.MinValue:light.TimeUtc,metrics!=null&&TelemetryContinuity.IsDisconnect(metrics.Error));
  snap.error=stale?"部分监测数据尚未就绪或已过期":"";return snap;
 }
 static double? TaskbarNumber(string text){double number;var match=System.Text.RegularExpressions.Regex.Match(text??"",@"-?\d+(?:\.\d+)?");return match.Success&&double.TryParse(match.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)?(double?)number:null;}
 static void TaskbarRate(Action<string,string,double?,string,int> add,string id,string label,double? number){double div=number>=1048576?1048576:1024;add(id,label,number/div,div==1048576?"M/s":"K/s",1);}
}
