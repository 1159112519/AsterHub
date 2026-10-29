using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;

public static class ProcessMetadata {
 // WMI CreationDate has microsecond precision; normalize the native FILETIME to the same precision.
 internal static string NativeIdentity(string fileTime){long value;if(!long.TryParse(fileTime,NumberStyles.None,CultureInfo.InvariantCulture,out value)||value<=0)throw new InvalidOperationException("进程创建身份无效");return "win:"+(value/10).ToString(CultureInfo.InvariantCulture);}
 static GpuProcess Read(ManagementBaseObject item){
  string created=Convert.ToString(item["CreationDate"],CultureInfo.InvariantCulture);
  string identity=string.IsNullOrWhiteSpace(created)?null:NativeIdentity(ManagementDateTimeConverter.ToDateTime(created).ToUniversalTime().ToFileTimeUtc().ToString(CultureInfo.InvariantCulture));
  string command=Convert.ToString(item["CommandLine"]);
  return new GpuProcess{Pid=Convert.ToString(item["ProcessId"]),Name=Convert.ToString(item["ExecutablePath"]),CommandLine=string.IsNullOrWhiteSpace(command)?null:command,IdentityToken=identity};
 }
 static ManagementObjectSearcher Search(IEnumerable<int> ids){var search=new ManagementObjectSearcher("root\\CIMV2","SELECT ProcessId,ExecutablePath,CommandLine,CreationDate FROM Win32_Process WHERE "+string.Join(" OR ",ids.Select(id=>"ProcessId="+id)));search.Options.Timeout=TimeSpan.FromSeconds(2);return search;}
 internal static GpuProcess ReadCurrent(int pid){using(var search=Search(new[]{pid}))using(var result=search.Get())foreach(ManagementObject item in result)using(item)return Read(item);throw new InvalidOperationException("进程已退出，请刷新后重试");}
 public static void Enrich(List<GpuProcess> rows){
  foreach(var row in rows){row.IdentityToken=null;row.CommandLine=null;}
  var ids=rows.Select(p=>{int id;return int.TryParse(p.Pid,out id)&&id>4?id:0;}).Where(id=>id>4).Distinct().Take(256).ToArray();
  if(ids.Length==0)return;
  try{using(var search=Search(ids))using(var result=search.Get())foreach(ManagementObject item in result)using(item){
   GpuProcess data;try{data=Read(item);}catch(ArgumentException){continue;}
   foreach(var row in rows.Where(p=>p.Pid==data.Pid)){if(!string.IsNullOrWhiteSpace(data.Name))row.Name=data.Name;row.CommandLine=data.CommandLine;row.IdentityToken=data.IdentityToken;}
  }}catch(ManagementException){}catch(UnauthorizedAccessException){}catch(System.Runtime.InteropServices.COMException){}
 }
}
