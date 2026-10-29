using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32.SafeHandles;

public sealed class ProcessTarget : IDisposable {
 public int Pid {get;internal set;} public string Device {get;internal set;} public string Gpu {get;internal set;} public string Name {get;internal set;} public string User {get;internal set;} public string IdentityToken {get;internal set;} public string CommandLine {get;internal set;} public string SessionUser {get;internal set;} public string Reason {get;internal set;} public bool CanTerminate {get;internal set;}
 internal Server Endpoint;internal SafeProcessHandle Handle;internal bool Confirmed;internal bool Disposed;
 public void Dispose(){Disposed=true;if(Handle!=null)Handle.Dispose();}
}
public static class ProcessActions {
 public static Task<string> TerminateConfirmedAsync(ProcessTarget target){if(target==null||target.Disposed)throw new InvalidOperationException("结束目标已失效");target.Confirmed=true;return TerminateAsync(target);}
 const uint TerminateRight=1,QueryRight=0x1000,Synchronize=0x100000;
 [DllImport("kernel32.dll",SetLastError=true)]static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(SafeProcessHandle h,out long creation,out long exit,out long kernel,out long user);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(SafeProcessHandle h,int flags,StringBuilder name,ref int size);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(SafeProcessHandle h,uint code);
 [DllImport("kernel32.dll",SetLastError=true)]static extern uint WaitForSingleObject(SafeProcessHandle h,uint timeout);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(SafeProcessHandle h,uint access,out IntPtr token);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool GetTokenInformation(IntPtr token,int info,IntPtr data,int size,out int required);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 static string Owner(SafeProcessHandle handle){IntPtr token;if(!OpenProcessToken(handle,8,out token))throw new Win32Exception();try{int length;GetTokenInformation(token,1,IntPtr.Zero,0,out length);IntPtr buffer=Marshal.AllocHGlobal(length);try{if(!GetTokenInformation(token,1,buffer,length,out length))throw new Win32Exception();var sid=new SecurityIdentifier(Marshal.ReadIntPtr(buffer));try{return sid.Translate(typeof(NTAccount)).Value;}catch{return sid.Value;}}finally{Marshal.FreeHGlobal(buffer);}}finally{CloseHandle(token);}}
 static string Token(SafeProcessHandle handle){long c,e,k,u;if(!GetProcessTimes(handle,out c,out e,out k,out u))throw new Win32Exception();return c.ToString(System.Globalization.CultureInfo.InvariantCulture);}
 public static Task<ProcessTarget> InspectAsync(Server server,GpuProcess process){if(server==null||process==null)throw new ArgumentNullException();var endpoint=server.Copy();string pid=process.Pid,gpu=process.Gpu,name=process.Name,user=process.User,identity=process.IdentityToken;return Task.Run(()=>Inspect(endpoint,pid,gpu,name,user,identity));}
 static ProcessTarget Inspect(Server server,string textPid,string gpu,string expectedName,string expectedUser,string sampledIdentity){if(string.IsNullOrWhiteSpace(sampledIdentity))throw new InvalidOperationException("采样缺少进程创建身份，请等待新的采样后重试");int pid;if(!int.TryParse(textPid,out pid)||pid<=4)throw new InvalidOperationException("PID 无效或受保护，操作已拒绝");if(server.IsLocal&&pid==Process.GetCurrentProcess().Id)throw new InvalidOperationException("不能结束监控程序自身");
  var target=new ProcessTarget{Pid=pid,Endpoint=server.Copy(),Device=server.Name+" / "+(server.IsLocal?Environment.MachineName:server.Host),Gpu=gpu,CanTerminate=false};
  try{if(server.IsLocal){target.Handle=OpenProcess(QueryRight|TerminateRight|Synchronize,false,pid);if(target.Handle.IsInvalid)throw new Win32Exception(Marshal.GetLastWin32Error(),"无权打开该进程或进程已退出");if(WaitForSingleObject(target.Handle,0)==0)throw new InvalidOperationException("进程已退出");target.IdentityToken=Token(target.Handle);ValidateSampleIdentity(sampledIdentity,ProcessMetadata.NativeIdentity(target.IdentityToken));var metadata=ProcessMetadata.ReadCurrent(pid);ValidateSampleIdentity(sampledIdentity,metadata.IdentityToken);target.CommandLine=metadata.CommandLine;var name=new StringBuilder(32768);int size=name.Capacity;if(!QueryFullProcessImageName(target.Handle,0,name,ref size))throw new Win32Exception();target.Name=name.ToString();target.User=Owner(target.Handle);target.SessionUser=WindowsIdentity.GetCurrent().Name;target.CanTerminate=true;}
   else{var data=new JavaScriptSerializer().Deserialize<RemoteProcessInfo>(AqModuleSsh.Run(server,BuildRemoteInspectProgram(pid,expectedName)));if(!string.IsNullOrEmpty(data.Error))throw new InvalidOperationException(data.Error);if(!string.IsNullOrEmpty(data.ExpectedNameCanonical))expectedName=data.ExpectedNameCanonical;ValidateSampleIdentity(sampledIdentity,data.Token);target.IdentityToken=data.Token;target.CommandLine=data.CommandLine;target.Name=data.Name;target.User=data.User;target.SessionUser=data.SessionUser;target.CanTerminate=data.CanTerminate;target.Reason=data.Reason;if(data.SessionUid==0&&!string.Equals(server.User,"root",StringComparison.Ordinal)){target.CanTerminate=false;target.Reason="SSH 实际会话为 root，配置未明确指定 root，拒绝结束操作";}}
   ValidateRowIdentity(server.IsLocal,expectedName,expectedUser,target.Name,target.User);
   return target;
  }catch{target.Dispose();throw;}
 }
 internal static void ValidateSampleIdentity(string sampled,string actual){if(string.IsNullOrWhiteSpace(sampled)||string.IsNullOrWhiteSpace(actual)||!string.Equals(sampled,actual,StringComparison.Ordinal))throw new InvalidOperationException("进程创建身份与采样不一致，可能已发生 PID 复用，请刷新后重试");}
 internal static bool KnownIdentity(string value){if(string.IsNullOrWhiteSpace(value))return false;string x=value.Trim();return x!="—"&&x!="-"&&x!="?"&&!x.Equals("N/A",StringComparison.OrdinalIgnoreCase)&&!x.Equals("unknown",StringComparison.OrdinalIgnoreCase)&&x!="未提供"&&x!="未知"&&!x.StartsWith("[Not Found]",StringComparison.OrdinalIgnoreCase)&&!x.Contains("Insufficient Permissions");}
 internal static void ValidateRowIdentity(bool local,string expectedName,string expectedUser,string actualName,string actualUser){
  var comparison=local?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
  if(KnownIdentity(expectedName)){
   string expected=expectedName.Trim(),actual=(actualName??"").Trim();
   bool path=expected.Contains("/")||expected.Contains("\\");
   if(!path){actual=actual.Replace('\\','/');actual=actual.Substring(actual.LastIndexOf('/')+1);if(local){expected=expected.EndsWith(".exe",comparison)?expected:expected+".exe";actual=actual.EndsWith(".exe",comparison)?actual:actual+".exe";}}
   if(!string.Equals(expected,actual,comparison))throw new InvalidOperationException("进程名称与所选行不一致，可能已发生 PID 复用。请刷新进程列表后重试");
  }
  if(KnownIdentity(expectedUser)&&!string.Equals(expectedUser.Trim(),(actualUser??"").Trim(),comparison))throw new InvalidOperationException("进程用户与所选行不一致，操作已拒绝。请刷新进程列表后重试");
 }
 public class RemoteProcessInfo {public string Token {get;set;} public string CommandLine {get;set;} public string Name {get;set;} public string ExpectedNameCanonical {get;set;} public string User {get;set;} public string SessionUser {get;set;} public string Reason {get;set;} public string Error {get;set;} public bool CanTerminate {get;set;} public int SessionUid {get;set;}}
 internal static string BuildRemoteInspectProgram(int pid,string expectedName){string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(KnownIdentity(expectedName)?expectedName.Trim():""));return RemoteInspectProgram+"\ninspect("+pid+",base64.b64decode('"+encoded+"').decode('utf-8'))";}
 internal const string RemoteInspectProgram=@"import os,signal,json,pwd,base64

def identity(pid):
 with open('/proc/%d/stat'%pid) as f:s=f.read()
 fields=s[s.rfind(')')+2:].split()
 uid=os.stat('/proc/%d'%pid).st_uid
 with open('/proc/sys/kernel/random/boot_id') as f:boot=f.read().strip()
 return boot+':'+fields[19]+':'+str(uid),uid

def canonical_expected_path(pid,expected_name,actual_name):
 if '/' not in expected_name:return expected_name
 path=expected_name if os.path.isabs(expected_name) else os.path.join('/proc/%d/cwd'%pid,expected_name)
 # stat follows symlinks; executable inode+device identity also handles hardlink aliases.
 expected=os.stat(path)
 actual=os.stat('/proc/%d/exe'%pid)
 if (expected.st_dev,expected.st_ino)!=(actual.st_dev,actual.st_ino):raise RuntimeError('所选进程路径与当前可执行文件不同，请刷新列表')
 return actual_name

def inspect(pid,expected_name=''):
 try:
  token,uid=identity(pid)
  try:name=os.readlink('/proc/%d/exe'%pid)
  except OSError:
   with open('/proc/%d/comm'%pid) as f:name=f.read().strip()
  expected_canonical=canonical_expected_path(pid,expected_name,name)
  command=None
  try:
   with open('/proc/%d/cmdline'%pid,'rb') as f:raw=f.read(32769)
   if len(raw)<=32768:command=raw.replace(b'\x00',b' ').decode('utf-8',errors='replace').strip() or None
  except OSError:pass
  try:user=pwd.getpwuid(uid).pw_name
  except KeyError:user=str(uid)
  euid=os.geteuid()
  try:session=pwd.getpwuid(euid).pw_name
  except KeyError:session=str(euid)
  if identity(pid)[0]!=token:raise RuntimeError('进程身份发生变化，请刷新')
  capable=hasattr(os,'pidfd_open') and hasattr(signal,'pidfd_send_signal')
  reason='' if capable else 'Python / 内核未提供 pidfd 安全信号接口，拒绝无身份绑定的结束操作'
  if uid!=euid:capable=False;reason='目标进程不属于当前 SSH 用户，拒绝操作'
  print(json.dumps(dict(Token=token,CommandLine=command,Name=name,ExpectedNameCanonical=expected_canonical,User=user,SessionUser=session,SessionUid=euid,CanTerminate=capable,Reason=reason),ensure_ascii=False))
 except Exception as e:print(json.dumps({'Error':'无法核验进程身份：'+str(e)},ensure_ascii=False))
";
 static string RemoteTerminateProgram(int pid,string token){if(!System.Text.RegularExpressions.Regex.IsMatch(token??"",@"^[a-fA-F0-9-]+:\d+:\d+$"))throw new InvalidOperationException("无效的进程身份令牌");return RemoteInspectProgram+@"
import select
try:
 pid="+pid+@"
 expected='"+token+@"'
 if not hasattr(os,'pidfd_open') or not hasattr(signal,'pidfd_send_signal'):raise RuntimeError('pidfd 不可用，操作已拒绝')
 fd=os.pidfd_open(pid,0)
 try:
  actual,uid=identity(pid)
  if actual!=expected:raise RuntimeError('PID 身份变化，操作已拒绝')
  if uid!=os.geteuid():raise RuntimeError('用户身份变化，操作已拒绝')
  signal.pidfd_send_signal(fd,signal.SIGTERM,None,0)
  poll=select.poll();poll.register(fd,select.POLLIN)
  stopped=bool(poll.poll(1200))
  print(json.dumps({'Message':'进程已退出（SIGTERM）' if stopped else '已发送 SIGTERM；进程尚未退出，请刷新确认。不会自动发送 SIGKILL。'},ensure_ascii=False))
 finally:os.close(fd)
except Exception as e:print(json.dumps({'Error':'结束操作失败：'+str(e)},ensure_ascii=False))
";}
 public class ActionResponse {public string Message {get;set;} public string Error {get;set;}}
 public static Task<string> TerminateAsync(ProcessTarget target){if(target==null)throw new ArgumentNullException("target");return Task.Run(()=>Terminate(target));}
 static string Terminate(ProcessTarget target){
  if(target.Disposed)throw new ObjectDisposedException("ProcessTarget");
  if(!target.Confirmed)throw new InvalidOperationException("尚未经过明确确认，操作已拒绝");
  target.Confirmed=false;
  if(!target.CanTerminate)throw new InvalidOperationException(target.Reason??"无法安全结束该进程");
  if(target.Endpoint.IsLocal){if(WaitForSingleObject(target.Handle,0)==0)return "进程已退出，无需结束";if(Token(target.Handle)!=target.IdentityToken)throw new InvalidOperationException("进程身份变化，操作已拒绝");if(!TerminateProcess(target.Handle,1))throw new Win32Exception(Marshal.GetLastWin32Error(),"结束进程失败");return WaitForSingleObject(target.Handle,1200)==0?"进程已结束":"已请求结束；请刷新确认";}
  var response=new JavaScriptSerializer().Deserialize<ActionResponse>(AqModuleSsh.Run(target.Endpoint,RemoteTerminateProgram(target.Pid,target.IdentityToken)));if(!string.IsNullOrEmpty(response.Error))throw new InvalidOperationException(response.Error);return response.Message;
 }
}
