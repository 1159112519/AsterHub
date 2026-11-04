using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Security.Principal;
using System.Web.Script.Serialization;

// Local Windows maintenance only. Plans pin process handles until confirmation;
// Termination is a separate explicit request, never a maintenance/policy action.
// No driver, service, forced elevation or privilege grants.
public sealed class MemoryMaintenance : IDisposable {
 public sealed class Reading {
  public string timeUtc;public long totalBytes,availableBytes,usedBytes;public long? standbyBytes,lowStandbyBytes,freeBytes,cacheBytes,commitUsedBytes,commitLimitBytes,pagedBytes,nonpagedBytes;
 }
 public sealed class ProcessRow {public int pid;public string name,path,reason,startIdentity;public long workingSetBytes,startTicks;public bool canTrim,canTerminate,canProtect,protectedProgram,intrinsicProtection;}
 public sealed class AreaCapability {public bool available;public string reason;}
 public sealed class Policy {public bool enabled;public int threshold=90,durationMinutes=5,cooldownMinutes=30;public string[] areas=new[]{"standby"};}
 public sealed class Outcome {public string area,status,message;public int attempted,succeeded,skipped;public long observedReductionBytes;}
 public sealed class Record {public string id,timeUtc,trigger,status;public double durationMs;public string[] areas;public Reading before,after;public long availableIncreaseBytes;public List<Outcome> results=new List<Outcome>();public string warning;}
 public sealed class State {public int schema=1;public string lastAttemptUtc;public Policy policy=new Policy();public List<Record> records=new List<Record>();public string[] manualAreas;public List<string> protectedPaths=new List<string>();}
 public sealed class Status {public Reading snapshot;public List<ProcessRow> processes;public Dictionary<string,AreaCapability> capabilities;public Policy policy;public List<Record> records;public double heldSeconds,cooldownSeconds;public string policyError;public string[] manualAreas;public List<string> protectedPaths;}
 public sealed class Confirmation {public string token,expiresUtc;public string[] areas;public int processCount;public List<ProcessRow> processes;}
 sealed class Target:IDisposable {public IntPtr handle;public ProcessRow row;public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}}
 sealed class Plan:IDisposable {public DateTime created=DateTime.UtcNow;public string[] areas;public List<Target> targets=new List<Target>();public void Dispose(){foreach(var t in targets)t.Dispose();targets.Clear();}}
 readonly object gate=new object();readonly string stateFile;readonly JavaScriptSerializer serializer=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};readonly Dictionary<string,Plan> plans=new Dictionary<string,Plan>();
 readonly int ownPid,ownSession;readonly string ownDirectory,windowsDirectory;readonly Timer timer;readonly Version windowsVersion=NativeWindowsVersion();State state;bool disposed,privilege,quotaPrivilege,administrator;readonly Dictionary<string,string> unavailable=new Dictionary<string,string>();long highSince;DateTime lastAttempt=DateTime.MinValue;string policyError;
 List<ProcessRow> processCache;DateTime cacheTime=DateTime.MinValue;
 public MemoryMaintenance(string dataRoot){stateFile=Path.Combine(dataRoot,"memory-maintenance.json");ownPid=Process.GetCurrentProcess().Id;ownSession=Process.GetCurrentProcess().SessionId;ownDirectory=AppDomain.CurrentDomain.BaseDirectory;windowsDirectory=Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\')+"\\";state=Load();lastAttempt=RestoreAttempt();privilege=EnableExistingPrivilege("SeProfileSingleProcessPrivilege");quotaPrivilege=EnableExistingPrivilege("SeIncreaseQuotaPrivilege");administrator=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);timer=new Timer(Tick,null,2000,2000);}
 public object Request(string method,Dictionary<string,object> p){
  string device=p.ContainsKey("deviceId")?Convert.ToString(p["deviceId"]):"";if(device!="local")throw new InvalidOperationException("内存维护仅支持这台 Windows 本机");
  lock(gate){if(disposed)throw new ObjectDisposedException("MemoryMaintenance");ExpirePlans();
   if(method=="memory.status")return GetStatus();
   if(method=="memory.prepare")return Prepare(p);
   if(method=="memory.selection.save"){var previous=state.manualAreas;state.manualAreas=ParseAreas(p,true);try{Save();}catch{state.manualAreas=previous;throw;}return GetStatus();}
   if(method=="memory.process.protect")return Protect(p);
   if(method=="memory.process.terminate")return Terminate(p);
   if(method=="memory.cancel"){Plan plan;string token=Text(p,"token");if(plans.TryGetValue(token,out plan)){plans.Remove(token);plan.Dispose();}return true;}
   if(method=="memory.execute"){Plan plan;string token=Text(p,"token");if(!plans.TryGetValue(token,out plan))throw new InvalidOperationException("本次确认已过期，请重新选择");plans.Remove(token);using(plan){ValidateAreas(plan.areas,Read());BeginAttempt();return Execute(plan,"manual");}}
   if(method=="memory.policy.save"){var next=new Policy{enabled=Boolean(p,"enabled"),threshold=Bound(p,"threshold",70,99),durationMinutes=Bound(p,"durationMinutes",1,60),cooldownMinutes=Bound(p,"cooldownMinutes",10,240),areas=ParseAreas(p)};if(next.enabled)ValidateAreas(next.areas,Read());var previous=state.policy;state.policy=next;try{Save();}catch{state.policy=previous;throw;}highSince=0;policyError=null;return GetStatus();}
   throw new ArgumentException("不支持的内存操作");
  }
 }
 Status GetStatus(){var reading=Read();return new Status{snapshot=reading,processes=Processes(false),capabilities=Capabilities(reading),policy=state.policy,records=state.records.ToList(),heldSeconds=highSince==0?0:(Stopwatch.GetTimestamp()-highSince)/(double)Stopwatch.Frequency,cooldownSeconds=Math.Max(0,state.policy.cooldownMinutes*60-(DateTime.UtcNow-lastAttempt).TotalSeconds),policyError=policyError,manualAreas=state.manualAreas,protectedPaths=state.protectedPaths.ToList()};}
 static string Text(Dictionary<string,object> p,string key){object value;return p.TryGetValue(key,out value)?Convert.ToString(value):"";}
 static bool Boolean(Dictionary<string,object> p,string key){bool value;if(!bool.TryParse(Text(p,key),out value))throw new ArgumentException("自动策略状态无效");return value;}
 static int Bound(Dictionary<string,object> p,string key,int min,int max){int value;if(!int.TryParse(Text(p,key),out value)||value<min||value>max)throw new ArgumentException("自动策略数值超出范围");return value;}
 static readonly string[] AreaIds={"standby","allstandby","workset","filecache","modified","registry","combine"};
 static string[] ParseAreas(Dictionary<string,object> p,bool allowEmpty=false){object value;if(!p.TryGetValue("areas",out value)||!(value is System.Collections.IEnumerable)||value is string)throw new ArgumentException("请选择清理区域");var result=new List<string>();foreach(object area in (System.Collections.IEnumerable)value){string id=Convert.ToString(area);if(!AreaIds.Contains(id))throw new ArgumentException("清理区域无效");if(!result.Contains(id))result.Add(id);}if(!allowEmpty&&result.Count==0||result.Contains("standby")&&result.Contains("allstandby"))throw new ArgumentException("请选择有效且不重复的清理区域");return result.ToArray();}
 Dictionary<string,AreaCapability> Capabilities(Reading r){
  var result=new Dictionary<string,AreaCapability>();
  foreach(string id in AreaIds){string reason=null;
   if(id=="standby"||id=="allstandby")reason=r.standbyBytes==null?"当前系统无法读取待机列表":!privilege?"需要管理员权限及性能分析权限":null;
   if(id=="modified"||id=="combine")reason=!privilege?"需要管理员权限及性能分析权限":null;
   if(id=="filecache")reason=!quotaPrivilege?"需要管理员权限及调整内存配额权限":null;
   if(id=="registry")reason=!administrator?"需要以管理员身份运行 AsterHub":null;
   if(id=="registry"&&windowsVersion<new Version(6,3))reason="需要 Windows 8.1 或更新版本";if(id=="combine"&&windowsVersion<new Version(10,0))reason="需要 Windows 10 或更新版本";
   string rejected;if(unavailable.TryGetValue(id,out rejected))reason=rejected;
   result[id]=new AreaCapability{available=reason==null,reason=reason};
  }return result;
 }
 // Identity includes a full executable path and creation time; PIDs can be reused.
 ProcessRow ResolveProcess(Dictionary<string,object> p){int pid;long ticks;string path=Text(p,"path");if(!int.TryParse(Text(p,"pid"),out pid)||!long.TryParse(Text(p,"startTicks"),out ticks)||ticks<=0||!Path.IsPathRooted(path))throw new ArgumentException("进程身份无效，请刷新列表");var row=Processes(true).FirstOrDefault(r=>r.pid==pid&&r.startTicks==ticks&&string.Equals(r.path,path,StringComparison.OrdinalIgnoreCase));if(row==null)throw new InvalidOperationException("进程已退出或身份发生变化，请刷新列表");return row;}
 bool IsProtected(string path){return state.protectedPaths.Any(x=>string.Equals(x,path,StringComparison.OrdinalIgnoreCase));}
 object Protect(Dictionary<string,object> p){var row=ResolveProcess(p);if(!row.canProtect)throw new InvalidOperationException(row.reason??"无法保护此进程");bool keep=Boolean(p,"protected");var previous=state.protectedPaths.ToList();state.protectedPaths.RemoveAll(x=>string.Equals(x,row.path,StringComparison.OrdinalIgnoreCase));if(keep)state.protectedPaths.Add(row.path);try{Save();}catch{state.protectedPaths=previous;throw;}processCache=null;return GetStatus();}
 object Terminate(Dictionary<string,object> p){var row=ResolveProcess(p);if(!row.canTerminate||IsProtected(row.path))throw new InvalidOperationException(row.reason??"保留的程序不能结束");IntPtr handle=OpenProcess(0x101001,false,row.pid);if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error());try{long created;bool critical;if(!CreationTime(handle,out created)||created!=row.startTicks||!IsProcessCritical(handle,out critical)||critical)throw new InvalidOperationException("进程身份或安全状态已变化");if(!TerminateProcess(handle,1))throw new Win32Exception(Marshal.GetLastWin32Error());WaitForSingleObject(handle,3000);}finally{CloseHandle(handle);}processCache=null;return GetStatus();}
 void ValidateAreas(string[] areas,Reading r){foreach(string id in areas){AreaCapability c;if(!Capabilities(r).TryGetValue(id,out c)||!c.available)throw new InvalidOperationException(c==null?"清理区域无效":c.reason);}}
 Confirmation Prepare(Dictionary<string,object> p){var areas=ParseAreas(p);ValidateAreas(areas,Read());var plan=CreatePlan(areas,p);if(plans.Count>=3){plan.Dispose();throw new InvalidOperationException("请先关闭已有的清理确认窗口");}string token=Guid.NewGuid().ToString("N");plans.Add(token,plan);return new Confirmation{token=token,expiresUtc=plan.created.AddSeconds(60).ToString("o"),areas=areas,processCount=plan.targets.Count,processes=plan.targets.Select(t=>t.row).ToList()};}
 Plan CreatePlan(string[] areas,Dictionary<string,object> p){var plan=new Plan{areas=areas};try{
  if(areas.Contains("workset")){HashSet<int> only=null;object raw;if(p.TryGetValue("pids",out raw)){if(!(raw is System.Collections.IEnumerable)||raw is string)throw new ArgumentException("进程选择无效");only=new HashSet<int>();foreach(object v in (System.Collections.IEnumerable)raw){int id;if(!int.TryParse(Convert.ToString(v),out id)||id<=0)throw new ArgumentException("进程选择无效");only.Add(id);}if(only.Count==0)throw new ArgumentException("请选择进程");}
   foreach(var row in Processes(true).Where(r=>r.canTrim&&(only==null||only.Contains(r.pid)))){IntPtr handle=OpenProcess(0x510,false,row.pid);if(handle==IntPtr.Zero)continue;long created;if(!CreationTime(handle,out created)||created!=row.startTicks){CloseHandle(handle);continue;}plan.targets.Add(new Target{handle=handle,row=row});}
   if(plan.targets.Count==0)throw new InvalidOperationException("当前没有可处理的进程，进程可能已退出或权限发生变化");
  }return plan;
 }catch{plan.Dispose();throw;}}
 Record Execute(Plan plan,string trigger){
  var clock=Stopwatch.StartNew();var record=new Record{id=Guid.NewGuid().ToString("N"),timeUtc=DateTime.UtcNow.ToString("o"),trigger=trigger,areas=plan.areas,before=Read()};
  foreach(string area in plan.areas){var outcome=new Outcome{area=area,status="success"};
   try{if(area=="workset"){foreach(var target in plan.targets){outcome.attempted++;if(IsProtected(target.row.path)){outcome.skipped++;continue;}try{long before=WorkingSet(target.handle);if(!EmptyWorkingSet(target.handle))throw new Win32Exception(Marshal.GetLastWin32Error());long after=WorkingSet(target.handle);outcome.observedReductionBytes+=Math.Max(0,before-after);outcome.succeeded++;}catch{outcome.skipped++;}}if(outcome.succeeded==0){outcome.status="failed";outcome.message="进程已保留、退出或当前权限不足";}else if(outcome.skipped>0){outcome.status="partial";outcome.message="部分进程已保留、退出或无法处理";}}
   else{var before=Read();int result=0;
    if(area=="filecache"){if(!SetSystemFileCacheSize(UIntPtr.Size==8?new UIntPtr(ulong.MaxValue):new UIntPtr(uint.MaxValue),UIntPtr.Size==8?new UIntPtr(ulong.MaxValue):new UIntPtr(uint.MaxValue),0))throw new Win32Exception(Marshal.GetLastWin32Error());}
    else if(area=="registry")result=NtSetSystemInformationBuffer(155,IntPtr.Zero,0);
    else if(area=="combine"){var info=new CombineInfo();result=NtSetSystemInformationCombine(130,ref info,Marshal.SizeOf(typeof(CombineInfo)));}
    else{int command=area=="allstandby"?4:area=="modified"?3:5;result=NtSetSystemInformation(80,ref command,4);}
    if(result<0){string reason="Windows 拒绝了此区域操作 (0x"+unchecked((uint)result).ToString("X8")+")";if(result==unchecked((int)0xc0000003)||result==unchecked((int)0xc0000061)||result==unchecked((int)0xc0000022))unavailable[area]=reason;throw new InvalidOperationException(reason);}
    var after=Read();outcome.attempted=outcome.succeeded=1;outcome.observedReductionBytes=Math.Max(0,area=="standby"?before.lowStandbyBytes.GetValueOrDefault()-after.lowStandbyBytes.GetValueOrDefault():area=="allstandby"?before.standbyBytes.GetValueOrDefault()-after.standbyBytes.GetValueOrDefault():area=="filecache"?before.cacheBytes.GetValueOrDefault()-after.cacheBytes.GetValueOrDefault():after.availableBytes-before.availableBytes);}

   }catch(Exception e){outcome.status="failed";outcome.message=e.Message;}
   record.results.Add(outcome);
  }
  record.after=Read();record.availableIncreaseBytes=Math.Max(0,record.after.availableBytes-record.before.availableBytes);record.durationMs=clock.Elapsed.TotalMilliseconds;record.status=record.results.All(r=>r.status=="success")?"success":record.results.All(r=>r.status=="failed")?"failed":"partial";
  state.records.Insert(0,record);if(state.records.Count>100)state.records.RemoveRange(100,state.records.Count-100);processCache=null;try{Save();}catch{record.warning="操作已完成，但记录无法保存到磁盘";}return record;
 }
 internal static bool PolicyReady(Policy p,double percent,double heldSeconds,double sinceAttemptSeconds){return p.enabled&&percent>=p.threshold&&heldSeconds>=p.durationMinutes*60&&sinceAttemptSeconds>=p.cooldownMinutes*60;}
 DateTime RestoreAttempt(){DateTime latest=DateTime.MinValue,parsed;foreach(string text in new[]{state.lastAttemptUtc}.Concat(state.records.Select(r=>r.timeUtc))){if(DateTime.TryParse(text,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out parsed)&&parsed.ToUniversalTime()>latest)latest=parsed.ToUniversalTime();}return latest>DateTime.UtcNow?DateTime.UtcNow:latest;}
 void BeginAttempt(){lastAttempt=DateTime.UtcNow;state.lastAttemptUtc=lastAttempt.ToString("o");highSince=0;Save();}
 void Tick(object unused){if(!Monitor.TryEnter(gate))return;try{if(disposed)return;ExpirePlans();var policy=state.policy;if(!policy.enabled){highSince=0;return;}var reading=Read();double percent=reading.totalBytes<=0?0:reading.usedBytes*100.0/reading.totalBytes;if(percent<policy.threshold){highSince=0;return;}if(highSince==0)highSince=Stopwatch.GetTimestamp();if(!PolicyReady(policy,percent,(Stopwatch.GetTimestamp()-highSince)/(double)Stopwatch.Frequency,(DateTime.UtcNow-lastAttempt).TotalSeconds))return;
  BeginAttempt();ValidateAreas(policy.areas,reading);using(var plan=CreatePlan(policy.areas,new Dictionary<string,object>())){var record=Execute(plan,"automatic");policyError=record.status=="failed"?"上次自动整理未完成，请查看清理记录":null;}
 }catch(Exception e){policyError=e.Message;highSince=0;}finally{Monitor.Exit(gate);}}
 void ExpirePlans(){foreach(var key in plans.Where(p=>(DateTime.UtcNow-p.Value.created).TotalSeconds>=60).Select(p=>p.Key).ToList()){plans[key].Dispose();plans.Remove(key);}}
 public void Dispose(){lock(gate){if(disposed)return;disposed=true;timer.Dispose();foreach(var p in plans.Values)p.Dispose();plans.Clear();}}
 State Load(){try{if(new FileInfo(stateFile).Length>4*1024*1024)return new State();var result=serializer.Deserialize<State>(File.ReadAllText(stateFile,Encoding.UTF8));if(result==null||result.schema!=1||result.policy==null||result.records==null)return new State();var p=result.policy;if(p.threshold<70||p.threshold>99||p.durationMinutes<1||p.durationMinutes>60||p.cooldownMinutes<10||p.cooldownMinutes>240||p.areas==null)return new State();ParseAreas(new Dictionary<string,object>{{"areas",p.areas}});if(result.manualAreas!=null)ParseAreas(new Dictionary<string,object>{{"areas",result.manualAreas}},true);result.protectedPaths=(result.protectedPaths??new List<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)&&Path.IsPathRooted(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();result.records=result.records.Where(r=>r!=null&&r.before!=null&&r.after!=null&&r.results!=null).Take(100).ToList();return result;}catch{return new State();}}
 void Save(){Directory.CreateDirectory(Path.GetDirectoryName(stateFile));string temp=stateFile+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(var fs=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){byte[] data=Encoding.UTF8.GetBytes(serializer.Serialize(state));fs.Write(data,0,data.Length);fs.Flush(true);}if(File.Exists(stateFile))File.Replace(temp,stateFile,null);else File.Move(temp,stateFile);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static Reading Read(){var mem=new MemoryStatus{length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};if(!GlobalMemoryStatusEx(ref mem))throw new Win32Exception(Marshal.GetLastWin32Error());var r=new Reading{timeUtc=DateTime.UtcNow.ToString("o"),totalBytes=(long)mem.total,availableBytes=(long)mem.available,usedBytes=(long)(mem.total-mem.available)};var pi=new PerformanceInfo{size=(uint)Marshal.SizeOf(typeof(PerformanceInfo))};if(GetPerformanceInfo(ref pi,pi.size)){long page=(long)pi.pageSize.ToUInt64();r.cacheBytes=(long)pi.cache.ToUInt64()*page;r.commitUsedBytes=(long)pi.commitTotal.ToUInt64()*page;r.commitLimitBytes=(long)pi.commitLimit.ToUInt64()*page;r.pagedBytes=(long)pi.kernelPaged.ToUInt64()*page;r.nonpagedBytes=(long)pi.kernelNonpaged.ToUInt64()*page;}
  // phnt ntexapi.h documents the private memory-list ABI. Unsupported layouts stay unavailable.
  int length=22*IntPtr.Size;IntPtr buffer=Marshal.AllocHGlobal(length);try{int returned;int status=NtQuerySystemInformation(80,buffer,length,out returned);if(status>=0&&returned>=13*IntPtr.Size&&returned<=length){long page=Environment.SystemPageSize,sum=0;for(int i=0;i<8;i++)sum+=ReadSize(buffer,(5+i)*IntPtr.Size);r.standbyBytes=sum*page;r.lowStandbyBytes=ReadSize(buffer,5*IntPtr.Size)*page;r.freeBytes=(ReadSize(buffer,0)+ReadSize(buffer,IntPtr.Size))*page;if(r.standbyBytes<0||r.standbyBytes>r.totalBytes||r.lowStandbyBytes>r.standbyBytes){r.standbyBytes=r.lowStandbyBytes=r.freeBytes=null;}}}catch(EntryPointNotFoundException){}finally{Marshal.FreeHGlobal(buffer);}return r;
 }
 static long ReadSize(IntPtr p,int offset){return IntPtr.Size==8?Marshal.ReadInt64(p,offset):(uint)Marshal.ReadInt32(p,offset);}
 List<ProcessRow> Processes(bool force){if(!force&&processCache!=null&&(DateTime.UtcNow-cacheTime).TotalSeconds<3)return processCache;var result=new List<ProcessRow>();foreach(var p in Process.GetProcesses()){using(p){try{long ws=p.WorkingSet64;if(ws<65536)continue;string name=p.ProcessName+".exe",path="",reason=null;long start=0;bool critical=false,criticalKnown=false;IntPtr read=OpenProcess(0x1000,false,p.Id);if(read!=IntPtr.Zero){try{var text=new StringBuilder(32768);int length=text.Capacity;if(QueryFullProcessImageName(read,0,text,ref length))path=text.ToString();CreationTime(read,out start);criticalKnown=IsProcessCritical(read,out critical);}finally{CloseHandle(read);}}bool eligible=criticalKnown&&!critical&&p.Id!=ownPid&&p.SessionId==ownSession&&!string.IsNullOrEmpty(path)&&!path.StartsWith(windowsDirectory,StringComparison.OrdinalIgnoreCase)&&!path.StartsWith(ownDirectory,StringComparison.OrdinalIgnoreCase)&&!name.Equals("msedgewebview2.exe",StringComparison.OrdinalIgnoreCase);
   bool intrinsic=!eligible,canProtect=eligible,canTerminate=false;bool keep=IsProtected(path);if(eligible){IntPtr terminate=OpenProcess(0x1001,false,p.Id);if(terminate!=IntPtr.Zero){bool terminateCritical;try{canTerminate=IsProcessCritical(terminate,out terminateCritical)&&!terminateCritical;}finally{CloseHandle(terminate);}}}
   if(eligible){IntPtr test=OpenProcess(0x510,false,p.Id);if(test==IntPtr.Zero){eligible=false;reason="当前权限不足";}else CloseHandle(test);}else reason=p.Id==ownPid||path.StartsWith(ownDirectory,StringComparison.OrdinalIgnoreCase)||name.Equals("msedgewebview2.exe",StringComparison.OrdinalIgnoreCase)?"本程序保留":"系统或受保护进程";
   if(keep){eligible=false;canTerminate=false;reason="已保留：跳过 AsterHub 工作集清理";}
   result.Add(new ProcessRow{pid=p.Id,name=name,path=path,workingSetBytes=ws,startTicks=start,startIdentity=start.ToString(CultureInfo.InvariantCulture),canTrim=eligible,reason=reason,canProtect=canProtect,canTerminate=canTerminate,protectedProgram=keep,intrinsicProtection=intrinsic});
  }catch(InvalidOperationException){}catch(Win32Exception){}catch(NotSupportedException){}}}processCache=result.OrderByDescending(p=>p.workingSetBytes).Take(512).ToList();cacheTime=DateTime.UtcNow;return processCache;}
 static bool CreationTime(IntPtr p,out long ticks){long created,exit,kernel,user;ticks=0;if(!GetProcessTimes(p,out created,out exit,out kernel,out user))return false;ticks=created;return true;}
 static long WorkingSet(IntPtr p){var c=new ProcessCounters{size=(uint)Marshal.SizeOf(typeof(ProcessCounters))};return GetProcessMemoryInfo(p,ref c,c.size)?(long)c.working.ToUInt64():0;}
 static bool EnableExistingPrivilege(string name){IntPtr token;if(!OpenProcessToken(Process.GetCurrentProcess().Handle,0x28,out token))return false;try{Luid id;if(!LookupPrivilegeValue(null,name,out id))return false;var privileges=new TokenPrivileges{count=1,luid=id,attributes=2};return AdjustTokenPrivileges(token,false,ref privileges,0,IntPtr.Zero,IntPtr.Zero)&&Marshal.GetLastWin32Error()==0;}finally{CloseHandle(token);}}
 static Version NativeWindowsVersion(){var info=new OsVersion{size=Marshal.SizeOf(typeof(OsVersion))};return RtlGetVersion(ref info)>=0?new Version((int)info.major,(int)info.minor):new Version(0,0);}
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct OsVersion{public int size;public uint major,minor,build,platform;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string servicePack;}
 [DllImport("ntdll.dll",CharSet=CharSet.Unicode)]static extern int RtlGetVersion(ref OsVersion version);
 [StructLayout(LayoutKind.Sequential)]struct CombineInfo{public IntPtr handle;public UIntPtr pagesCombined;public uint flags;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetSystemFileCacheSize(UIntPtr minimum,UIntPtr maximum,uint flags);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool IsProcessCritical(IntPtr handle,out bool critical);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateProcess(IntPtr handle,uint exitCode);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("ntdll.dll",EntryPoint="NtSetSystemInformation")]static extern int NtSetSystemInformationBuffer(int type,IntPtr buffer,int length);
 [DllImport("ntdll.dll",EntryPoint="NtSetSystemInformation")]static extern int NtSetSystemInformationCombine(int type,ref CombineInfo buffer,int length);
 [StructLayout(LayoutKind.Sequential)]struct MemoryStatus{public uint length,load;public ulong total,available,totalPageFile,availablePageFile,totalVirtual,availableVirtual,extended;}
 [StructLayout(LayoutKind.Sequential)]struct PerformanceInfo{public uint size;public UIntPtr commitTotal,commitLimit,commitPeak,physicalTotal,physicalAvailable,cache,kernelTotal,kernelPaged,kernelNonpaged,pageSize;public uint handles,processes,threads;}
 [StructLayout(LayoutKind.Sequential)]struct ProcessCounters{public uint size,faults;public UIntPtr peakWorking,working,peakPaged,paged,peakNonpaged,nonpaged,pageFile,peakPageFile,privateUsage;}
 [StructLayout(LayoutKind.Sequential)]struct Luid{public uint low;public int high;}
 [StructLayout(LayoutKind.Sequential)]struct TokenPrivileges{public uint count;public Luid luid;public uint attributes;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref MemoryStatus m);
 [DllImport("psapi.dll",SetLastError=true)]static extern bool GetPerformanceInfo(ref PerformanceInfo p,uint size);
 [DllImport("ntdll.dll")]static extern int NtQuerySystemInformation(int type,IntPtr p,int size,out int length);
 [DllImport("ntdll.dll")]static extern int NtSetSystemInformation(int type,ref int command,int length);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int id);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(IntPtr p,out long created,out long exited,out long kernel,out long user);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(IntPtr p,int flags,StringBuilder name,ref int size);
 [DllImport("psapi.dll",SetLastError=true)]static extern bool GetProcessMemoryInfo(IntPtr p,ref ProcessCounters info,uint size);
 [DllImport("psapi.dll",SetLastError=true)]static extern bool EmptyWorkingSet(IntPtr p);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr p,uint access,out IntPtr token);
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool LookupPrivilegeValue(string system,string name,out Luid luid);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool AdjustTokenPrivileges(IntPtr token,bool disable,ref TokenPrivileges privileges,uint length,IntPtr previous,IntPtr returned);
}
