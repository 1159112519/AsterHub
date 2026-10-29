using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// Sessions belong to the workbench, never to a React page or an existing console.
public sealed partial class WorkbenchWindow {
 sealed class WorkbenchTerminal {
  internal readonly object Gate=new object();
  internal readonly StringBuilder Buffer=new StringBuilder();
  internal string Id,DeviceId,Name,Kind;internal ITerminalSession Session;
  internal long Base;internal bool Ended;internal int? ExitCode;
  internal object View(){lock(Gate)return new{id=Id,deviceId=DeviceId,name=Name,kind=Kind,state=Ended?"exited":"running",exitCode=ExitCode};}
  internal Task Append(string text){lock(Gate){Buffer.Append(text);if(Buffer.Length>1048576){int remove=Buffer.Length-1048576;Buffer.Remove(0,remove);Base+=remove;}}return Task.FromResult(true);}
  internal object Read(long offset){lock(Gate){long end=Base+Buffer.Length;bool truncated=offset<Base;if(offset>end)offset=end;offset=Math.Max(offset,Base);int count=(int)Math.Min(49152,end-offset);string text=Buffer.ToString((int)(offset-Base),count);return new{data=text,next=offset+count,@base=Base,truncated=truncated,state=Ended?"exited":"running",exitCode=ExitCode};}}
 }
 readonly object terminalGate=new object();
 readonly Dictionary<string,WorkbenchTerminal> terminalSessions=new Dictionary<string,WorkbenchTerminal>();
 readonly Dictionary<Task,string> terminalCreations=new Dictionary<Task,string>();
 readonly Dictionary<string,long> terminalDeviceVersions=new Dictionary<string,long>();
 bool terminalClosing;
 static bool SameTerminalConfiguration(Server a,Server b){return a.IsLocal==b.IsLocal&&a.Name==b.Name&&a.Host==b.Host&&a.User==b.User&&a.Port==b.Port&&a.KeyPath==b.KeyPath&&a.AuthMode==b.AuthMode&&a.ProtectedPassword==b.ProtectedPassword&&a.Password==b.Password;}
 static string TerminalText(Dictionary<string,object> p,string key,string fallback=""){object value;return p.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):fallback;}
 static int TerminalSize(Dictionary<string,object> p,string key,int fallback,int max){int value;return int.TryParse(TerminalText(p,key),out value)?Math.Max(2,Math.Min(max,value)):fallback;}
 async Task<object> TerminalRequest(string method,Dictionary<string,object> p){
  if(method=="terminal.list"){lock(terminalGate)return terminalSessions.Values.Select(s=>s.View()).ToArray();}
  if(method=="terminal.create"){
   var server=devices.Find(TerminalText(p,"deviceId",devices.Selected));
   string deviceId=PreviewDevices.Id(server);long version;var completion=new TaskCompletionSource<bool>();
   int cols=TerminalSize(p,"cols",100,500),rows=TerminalSize(p,"rows",30,300);
   lock(terminalGate){if(terminalClosing)throw new InvalidOperationException("终端正在关闭");if(terminalSessions.Count+terminalCreations.Count>=12)throw new InvalidOperationException("最多同时打开 12 个终端，请先关闭不用的标签");terminalDeviceVersions.TryGetValue(deviceId,out version);terminalCreations.Add(completion.Task,deviceId);}
   ITerminalSession owned=null;WorkbenchTerminal entry=null;Exception failure=null;object result=null;
   try{
    owned=await Task.Run<ITerminalSession>(delegate{
     if(!server.IsLocal)return new SshTerminalSession(server,"stty echo; exec \"${SHELL:-/bin/sh}\" -l",cols,rows);
     string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
     if(!File.Exists(shell))throw new FileNotFoundException("无法找到本机 PowerShell");
     return new ConPtySession(ConPtySession.Quote(shell)+" -NoLogo -NoProfile -NoExit",cols,rows);
    });
    entry=new WorkbenchTerminal{Id=Guid.NewGuid().ToString("N"),DeviceId=deviceId,Name=server.Name,Kind=server.IsLocal?"PowerShell":"SSH",Session=owned};
    owned.Output=entry.Append;owned.Exited+=delegate(int code){lock(entry.Gate){entry.Ended=true;entry.ExitCode=code;}};
    // The creation await permits device edits, removals and app exit. Revalidate
    // immediately before transfer of ownership and Start, without another await.
    lock(terminalGate){
     if(terminalClosing)throw new InvalidOperationException("终端正在关闭");
     long liveVersion;terminalDeviceVersions.TryGetValue(deviceId,out liveVersion);
     var live=devices.Find(deviceId);
     if(version!=liveVersion||!SameTerminalConfiguration(server,live))throw new InvalidOperationException("设备连接配置已变更，请重新打开终端");
     terminalSessions.Add(entry.Id,entry);owned.Start();result=entry.View();
    }
   }catch(Exception error){failure=error;}
   if(failure!=null){
    lock(terminalGate){if(entry!=null)terminalSessions.Remove(entry.Id);}
    if(owned!=null){try{await Task.Run(delegate{owned.Dispose();});}catch(Exception cleanupError){failure=new AggregateException("终端创建失败，清理会话时发生错误",failure,cleanupError);}}
   }
   // Shutdown and device removal await this completion, including failure disposal.
   lock(terminalGate)terminalCreations.Remove(completion.Task);
   completion.TrySetResult(true);
   if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
   return result;
  }
  string id=TerminalText(p,"id");WorkbenchTerminal current;
  lock(terminalGate){if(!terminalSessions.TryGetValue(id,out current)){if(method=="terminal.close")return true;throw new ArgumentException("终端不存在或已关闭");}if(method=="terminal.close")terminalSessions.Remove(id);}
  if(method=="terminal.close"){await Task.Run(delegate{current.Session.Dispose();});return true;}
  // Reject arbitrary host/credential injection: only a configured device can own a session.
  devices.Find(current.DeviceId);
  if(method=="terminal.read"){long offset;if(!long.TryParse(TerminalText(p,"offset","0"),out offset)||offset<0)throw new ArgumentException("终端读取位置无效");return current.Read(offset);}
  if(method=="terminal.write"){
   string data=TerminalText(p,"data");if(data.Length>4096)throw new ArgumentException("终端单次输入过长");lock(current.Gate){if(current.Ended)throw new InvalidOperationException("终端已退出，请新建标签");}
   await Task.Run(delegate{current.Session.Write(data);});return true;
  }
  if(method=="terminal.resize"){int cols=TerminalSize(p,"cols",100,500),rows=TerminalSize(p,"rows",30,300);await Task.Run(delegate{current.Session.Resize(cols,rows);});return true;}
  throw new ArgumentException("不支持的终端操作");
 }
 async Task CloseDeviceTerminals(string deviceId){
  WorkbenchTerminal[] sessions;Task[] creating;
  lock(terminalGate){long version;terminalDeviceVersions.TryGetValue(deviceId,out version);terminalDeviceVersions[deviceId]=version+1;sessions=terminalSessions.Values.Where(s=>s.DeviceId==deviceId).ToArray();foreach(var s in sessions)terminalSessions.Remove(s.Id);creating=terminalCreations.Where(p=>p.Value==deviceId).Select(p=>p.Key).ToArray();}
  await Task.WhenAll(sessions.Select(s=>Task.Run(delegate{s.Session.Dispose();})).Concat(creating));
 }
 async Task DisposeTerminalSessions(){
  WorkbenchTerminal[] sessions;Task[] creating;
  lock(terminalGate){terminalClosing=true;sessions=terminalSessions.Values.ToArray();terminalSessions.Clear();creating=terminalCreations.Keys.ToArray();}
  await Task.WhenAll(sessions.Select(s=>Task.Run(delegate{s.Session.Dispose();})).Concat(creating));
 }
}
