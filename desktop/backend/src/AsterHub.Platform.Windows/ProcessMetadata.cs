using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

public static class ProcessMetadata {
 const uint QueryLimitedInformation=0x1000;
 [StructLayout(LayoutKind.Sequential)]struct ProcessMemoryCounters {
  public uint Size,PageFaults;
  public UIntPtr PeakWorkingSet,WorkingSet,QuotaPeakPagedPool,QuotaPagedPool,QuotaPeakNonPagedPool,QuotaNonPagedPool,PagefileUsage,PeakPagefileUsage;
 }
 static readonly uint MemoryCounterSize=(uint)Marshal.SizeOf(typeof(ProcessMemoryCounters));
 [DllImport("kernel32.dll",SetLastError=true)]static extern SafeProcessHandle OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetProcessTimes(SafeProcessHandle handle,out long creation,out long exit,out long kernel,out long user);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool QueryFullProcessImageName(SafeProcessHandle handle,int flags,StringBuilder name,ref int size);
 [DllImport("psapi.dll",SetLastError=true)]static extern bool GetProcessMemoryInfo(SafeProcessHandle handle,ref ProcessMemoryCounters counters,uint size);
 internal static SafeProcessHandle OpenRead(int pid){return OpenProcess(QueryLimitedInformation,false,pid);}
 internal static bool TryReadTimes(SafeProcessHandle handle,out long created,out long cpu){
  long exit,kernel,user;cpu=0;
  if(handle.IsInvalid||!GetProcessTimes(handle,out created,out exit,out kernel,out user)||created<=0||exit!=0){created=0;return false;}
  cpu=kernel+user;return true;
 }
 internal static long? ReadWorkingSet(SafeProcessHandle handle){
  var counters=new ProcessMemoryCounters{Size=MemoryCounterSize};
  return GetProcessMemoryInfo(handle,ref counters,MemoryCounterSize)?(long?)counters.WorkingSet.ToUInt64():null;
 }
 internal static string ReadImage(SafeProcessHandle handle,StringBuilder buffer){
  int size=buffer.Capacity;buffer.Length=0;
  if(QueryFullProcessImageName(handle,0,buffer,ref size))return buffer.ToString();
  if(Marshal.GetLastWin32Error()!=122||buffer.Capacity>=32768)return null;
  buffer.EnsureCapacity(32768);size=buffer.Capacity;
  return QueryFullProcessImageName(handle,0,buffer,ref size)?buffer.ToString():null;
 }
 // WMI CreationDate has microsecond precision; normalize the native FILETIME to the same precision.
 internal static string NativeIdentity(string fileTime){long value;if(!long.TryParse(fileTime,NumberStyles.None,CultureInfo.InvariantCulture,out value)||value<=0)throw new InvalidOperationException("进程创建身份无效");return "win:"+(value/10).ToString(CultureInfo.InvariantCulture);}
 static GpuProcess Read(ManagementBaseObject item){
  string created=Convert.ToString(item["CreationDate"],CultureInfo.InvariantCulture);
  string identity=string.IsNullOrWhiteSpace(created)?null:NativeIdentity(ManagementDateTimeConverter.ToDateTime(created).ToUniversalTime().ToFileTimeUtc().ToString(CultureInfo.InvariantCulture));
  string command=Convert.ToString(item["CommandLine"]);
  string path=Convert.ToString(item["ExecutablePath"]);
  return new GpuProcess{Pid=Convert.ToString(item["ProcessId"]),Name=path,Path=string.IsNullOrWhiteSpace(path)?null:path,CommandLine=string.IsNullOrWhiteSpace(command)?null:command,IdentityToken=identity};
 }
 static ManagementObjectSearcher Search(IEnumerable<int> ids){var search=new ManagementObjectSearcher("root\\CIMV2","SELECT ProcessId,ExecutablePath,CommandLine,CreationDate FROM Win32_Process WHERE "+string.Join(" OR ",ids.Select(id=>"ProcessId="+id)));search.Options.Timeout=TimeSpan.FromSeconds(2);return search;}
 internal static GpuProcess ReadCurrent(int pid){using(var search=Search(new[]{pid}))using(var result=search.Get())foreach(ManagementObject item in result)using(item)return Read(item);throw new InvalidOperationException("进程已退出，请刷新后重试");}
 static readonly object ownerGate=new object();static readonly Dictionary<string,Tuple<string,DateTime>> owners=new Dictionary<string,Tuple<string,DateTime>>();
 public static string Owner(string pid){int id;if(!int.TryParse(pid,out id))return "—";string key;try{using(var process=System.Diagnostics.Process.GetProcessById(id))key=pid+"/"+process.StartTime.ToUniversalTime().Ticks;}catch{return "—";}lock(ownerGate){Tuple<string,DateTime> found;if(owners.TryGetValue(key,out found)&&found.Item2>DateTime.UtcNow)return found.Item1;}
  string value="—";try{using(var process=new ManagementObject("Win32_Process.Handle='"+id+"'")){var args=new string[2];if(Convert.ToUInt32(process.InvokeMethod("GetOwner",args))==0)value=string.IsNullOrEmpty(args[1])?args[0]:args[1]+"\\"+args[0];}}catch{}
  lock(ownerGate){if(owners.Count>=1024)owners.Clear();owners[key]=Tuple.Create(value,DateTime.UtcNow.AddMinutes(1));}return value;
 }
 public static void Enrich(List<GpuProcess> rows){
  foreach(var row in rows){row.IdentityToken=null;row.CommandLine=null;row.Path=null;}
  var ids=rows.Select(p=>{int id;return int.TryParse(p.Pid,out id)&&id>4?id:0;}).Where(id=>id>4).Distinct().Take(256).ToArray();
  if(ids.Length==0)return;
  try{using(var search=Search(ids))using(var result=search.Get())foreach(ManagementObject item in result)using(item){
   GpuProcess data;try{data=Read(item);}catch(ArgumentException){continue;}
   foreach(var row in rows.Where(p=>p.Pid==data.Pid)){if(!string.IsNullOrWhiteSpace(data.Name))row.Name=data.Name;row.CommandLine=data.CommandLine;row.IdentityToken=data.IdentityToken;row.Path=data.Path;}
  }}catch(ManagementException){}catch(UnauthorizedAccessException){}catch(COMException){}
  // Drivers can return a PID while WMI cannot read that row. Limited native queries
  // can recover identity/path without turning missing command-line data into a path.
  var buffer=new StringBuilder(512);
  foreach(var group in rows.Where(row=>row.Path==null||row.IdentityToken==null).GroupBy(row=>row.Pid).Take(256)){
   int pid;if(!int.TryParse(group.Key,out pid)||pid<=4)continue;
   using(var handle=OpenRead(pid)){
    long created,cpu;if(!TryReadTimes(handle,out created,out cpu))continue;
    string identity=NativeIdentity(created.ToString(CultureInfo.InvariantCulture));
    string path=ReadImage(handle,buffer);
    foreach(var row in group){
     if(row.IdentityToken!=null&&!string.Equals(row.IdentityToken,identity,StringComparison.Ordinal))continue;
     row.IdentityToken=identity;
     if(row.Path==null&&path!=null){row.Path=path;row.Name=path;}
    }
   }
  }
 }
}
