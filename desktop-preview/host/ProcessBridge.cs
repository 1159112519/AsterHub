using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
public sealed partial class WorkbenchWindow {
 sealed class PendingProcess:IDisposable{public ProcessTarget Target;public string DeviceId,Fingerprint;public DateTime Expires;public void Dispose(){Target.Dispose();}}
 readonly Dictionary<string,PendingProcess> processTargets=new Dictionary<string,PendingProcess>();
 void PruneProcessTargets(){foreach(var key in processTargets.Where(x=>x.Value.Expires<DateTime.UtcNow).Select(x=>x.Key).ToArray()){processTargets[key].Dispose();processTargets.Remove(key);}}
 void ClearProcessTargets(){foreach(var item in processTargets.Values)item.Dispose();processTargets.Clear();}
 async Task<object> InspectProcess(Dictionary<string,object> p){
  PruneProcessTargets();if(processTargets.Count>=8)throw new InvalidOperationException("请先关闭其他进程确认窗口");
  var server=Requested(p);var reading=capture.Latest(server);string deviceId=PreviewDevices.Id(server),fingerprint=ProbeIdentity(server);
  if(reading==null||!string.IsNullOrEmpty(reading.Metrics.Error)||(DateTime.UtcNow-reading.TimeUtc).TotalSeconds>Math.Max(15,interval*3))throw new InvalidOperationException("设备状态尚未更新，请等待新的采样后重试");
  string pid=Convert.ToString(p["pid"]),gpu=Convert.ToString(p["gpu"]);
  var row=reading.Metrics.Processes.FirstOrDefault(x=>x.Pid==pid&&x.Gpu==gpu);if(row==null)throw new InvalidOperationException("该进程已不在最新 GPU 列表中，请刷新后重试");
  var target=await ProcessActions.InspectAsync(server,row);
  if(closed||shuttingDown||!devices.All().Any(x=>PreviewDevices.Id(x)==deviceId&&ProbeIdentity(x)==fingerprint)){target.Dispose();throw new InvalidOperationException("设备配置已变化，请重新选择进程");}
  string token=Guid.NewGuid().ToString("N");processTargets[token]=new PendingProcess{Target=target,DeviceId=deviceId,Fingerprint=fingerprint,Expires=DateTime.UtcNow.AddSeconds(60)};
  return new{token=token,pid=target.Pid,name=target.Name,user=target.User,device=target.Device,commandLine=target.CommandLine,canTerminate=target.CanTerminate,reason=target.Reason};
 }
 async Task<object> EndProcess(Dictionary<string,object> p){
  PruneProcessTargets();string token=Convert.ToString(p["token"]);PendingProcess pending;if(!processTargets.TryGetValue(token,out pending))throw new InvalidOperationException("确认已过期或已使用，请重新选择进程");processTargets.Remove(token);
  using(pending){if(!devices.All().Any(x=>PreviewDevices.Id(x)==pending.DeviceId&&ProbeIdentity(x)==pending.Fingerprint))throw new InvalidOperationException("设备配置已变化，操作已取消");return await ProcessActions.TerminateConfirmedAsync(pending.Target);}
 }
 object CancelProcess(Dictionary<string,object> p){string token=Convert.ToString(p["token"]);PendingProcess pending;if(processTargets.TryGetValue(token,out pending)){processTargets.Remove(token);pending.Dispose();}return true;}
}
