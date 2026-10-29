using System;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml;
public static class GpuTelemetry {
 // Keep the original NVIDIA identity for archives and process validation; normalize only visible text.
 public static string ProcessName(string name){return string.IsNullOrWhiteSpace(name)||name.Trim().StartsWith("[Not Found]",StringComparison.OrdinalIgnoreCase)||name.Trim().Equals("Not Found",StringComparison.OrdinalIgnoreCase)?"其他进程":name;}
 // Presentation-only filter; never remove unknown rows from raw telemetry or archives.
 public static bool IsKnownProcessName(string name){var value=(name??"").Trim();return value.Length>0&&!value.StartsWith("[Not Found]",StringComparison.OrdinalIgnoreCase)&&!Regex.IsMatch(value,@"^(?:not[ _-]*found|unknown(?: process)?|other processes?|n/a|其他进程|未知进程|未知|[-—–])$",RegexOptions.IgnoreCase);}
 public static bool Available(string text){double n;return double.TryParse((text??"").Trim().Replace(" W","").Replace(" %",""),NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)&&n>=0;}
 public static string Numeric(string text){return Available(text)?Regex.Match(text,@"[0-9]+(?:\.[0-9]+)?").Value:null;}
 public static string Unit(string value,string unit){return Available(value)?value+unit:"未提供";}
 public static string Power(Gpu g){return Available(g.Power)&&Available(g.Cap)?g.Power+" / "+g.Cap+" W":Unit(g.Power," W")+" / "+Unit(g.Cap," W");}
 public static string Memory(string value){return Available((value??"").Replace(" MiB",""))?value:"未提供";}
}
public sealed class GpuMemoryCounter {public string Name;public ulong Dedicated,Shared;}
public static class LocalGpu {
 static readonly object driverGate=new object();
 [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] [DllImport("nvcuda.dll")]static extern int cuInit(uint flags);
 [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] [DllImport("nvcuda.dll")]static extern int cuDeviceGetCount(out int count);
 [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] [DllImport("nvcuda.dll")]static extern int cuDeviceGet(out int device,int ordinal);
 [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] [DllImport("nvcuda.dll")]static extern int cuDeviceGetUuid([Out]byte[] uuid,int device);
 [DefaultDllImportSearchPaths(DllImportSearchPath.System32)] [DllImport("nvcuda.dll")]static extern int cuDeviceGetLuid([Out]byte[] luid,out uint mask,int device);
 public static Dictionary<string,string> AdapterMap(IEnumerable<Gpu> gpus){var map=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);lock(driverGate){try{int count;if(cuInit(0)!=0||cuDeviceGetCount(out count)!=0)return map;for(int i=0;i<count;i++){int device;uint mask;var luid=new byte[8];var bytes=new byte[16];if(cuDeviceGet(out device,i)!=0||cuDeviceGetUuid(bytes,device)!=0||cuDeviceGetLuid(luid,out mask,device)!=0)continue;string hex=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();string uuid="GPU-"+hex.Substring(0,8)+"-"+hex.Substring(8,4)+"-"+hex.Substring(12,4)+"-"+hex.Substring(16,4)+"-"+hex.Substring(20,12);var g=gpus.FirstOrDefault(x=>string.Equals(x.Uuid,uuid,StringComparison.OrdinalIgnoreCase));if(g==null)continue;string id="luid_0x"+BitConverter.ToUInt32(luid,4).ToString("X8")+"_0x"+BitConverter.ToUInt32(luid,0).ToString("X8");for(int node=0;node<32;node++)if((mask&(1u<<node))!=0)map[id+"_phys_"+node]=g.Index;}}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}catch(BadImageFormatException){}}return map;}
 public static void Enrich(Metrics m,Func<string,string> run){try{if(m.Gpus.Any(x=>!GpuTelemetry.Available(x.Cap)||!GpuTelemetry.Available(x.Fan)||string.IsNullOrEmpty(x.PowerMax)))ApplyPowerXml(m,run("-q -x"));}catch{}var map=AdapterMap(m.Gpus);if(map.Count==0){NormalizeProcesses(m);return;}try{var counters=new List<GpuMemoryCounter>();using(var query=new ManagementObjectSearcher("root\\CIMV2","SELECT Name,DedicatedUsage,SharedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory")){query.Options.Timeout=TimeSpan.FromSeconds(4);using(var rows=query.Get())foreach(ManagementObject row in rows)using(row)counters.Add(new GpuMemoryCounter{Name=Convert.ToString(row["Name"]),Dedicated=Convert.ToUInt64(row["DedicatedUsage"]),Shared=Convert.ToUInt64(row["SharedUsage"])});}ApplyCounters(m,map,counters,ProcessName,Owner);}catch{}NormalizeProcesses(m);}
 public static void ApplyPowerXml(Metrics m,string xml){if(string.IsNullOrWhiteSpace(xml)||!xml.TrimStart().StartsWith("<?xml"))return;var settings=new XmlReaderSettings{DtdProcessing=DtdProcessing.Ignore,XmlResolver=null};var document=new XmlDocument{XmlResolver=null};using(var reader=XmlReader.Create(new System.IO.StringReader(xml),settings))document.Load(reader);foreach(XmlNode node in document.SelectNodes("/nvidia_smi_log/gpu")){string uuid=Read(node,"uuid");var g=m.Gpus.Find(x=>x.Uuid==uuid);if(g==null)continue;string cap=GpuTelemetry.Numeric(Read(node,"gpu_power_readings/current_power_limit"));if(cap!=null){g.Cap=cap;g.CapSource="NVIDIA XML · 当前功耗限制（可动态变化）";}g.PowerMax=GpuTelemetry.Numeric(Read(node,"gpu_power_readings/max_power_limit"));if(!GpuTelemetry.Available(g.Fan)){string fan=GpuTelemetry.Numeric(Read(node,"fan_speed"));if(fan!=null)g.Fan=fan;}}}
 static string Read(XmlNode node,string xpath){var value=node.SelectSingleNode(xpath);return value==null?"":value.InnerText;}
 public static void ApplyCounters(Metrics m,Dictionary<string,string> adapters,IEnumerable<GpuMemoryCounter> counters,Func<string,string> name,Func<string,string> owner){var groups=new Dictionary<string,GpuMemoryCounter>();foreach(var row in counters){var match=Regex.Match(row.Name??"",@"^pid_(\d+)_(luid_0x[0-9a-f]+_0x[0-9a-f]+)_phys_(\d+)$",RegexOptions.IgnoreCase);if(!match.Success)continue;string gpu;if(!adapters.TryGetValue(match.Groups[2].Value+"_phys_"+match.Groups[3].Value,out gpu))continue;string pid=match.Groups[1].Value,key=gpu+"/"+pid;GpuMemoryCounter total;if(!groups.TryGetValue(key,out total))groups[key]=total=new GpuMemoryCounter{Name=key};total.Dedicated+=row.Dedicated;total.Shared+=row.Shared;}
 foreach(var row in groups.Values){var ids=row.Name.Split('/');var process=m.Processes.Find(x=>x.Gpu==ids[0]&&x.Pid==ids[1]);if(process==null){if(row.Dedicated==0&&row.Shared==0)continue;process=new GpuProcess{Gpu=ids[0],Pid=ids[1],Name=name(ids[1]),User=owner(ids[1]),Type="C/G"};m.Processes.Add(process);}else{if(string.IsNullOrWhiteSpace(process.Name)||process.Name.Contains("Permissions"))process.Name=name(process.Pid);process.Type="C/G";}
 if(!GpuTelemetry.Available((process.Memory??"").Replace(" MiB",""))){process.Memory=(row.Dedicated/1048576.0).ToString("0.0",CultureInfo.InvariantCulture)+" MiB";process.MemorySource="Windows WDDM · 按 GPU LUID + PID 匹配的专用显存";}else process.MemorySource="NVIDIA · 进程显存";process.SharedMemory=(row.Shared/1048576.0).ToString("0.0",CultureInfo.InvariantCulture)+" MiB";
 }}
 static void NormalizeProcesses(Metrics m){foreach(var p in m.Processes){p.Memory=GpuTelemetry.Memory(p.Memory);if(string.IsNullOrWhiteSpace(p.MemorySource))p.MemorySource=p.Memory=="未提供"?"驱动未提供进程显存；Windows 计数器无匹配数据":"NVIDIA · 进程显存";}}
 static string ProcessName(string pid){int n;try{if(int.TryParse(pid,out n))using(var p=Process.GetProcessById(n))return p.ProcessName+".exe";}catch{}return "进程已退出或无访问权限";}
 static string Owner(string pid){int n;if(!int.TryParse(pid,out n))return "—";try{using(var p=new ManagementObject("Win32_Process.Handle='"+n+"'")){var args=new string[2];if(Convert.ToUInt32(p.InvokeMethod("GetOwner",args))==0)return string.IsNullOrEmpty(args[1])?args[0]:args[1]+"\\"+args[0];}}catch{}return "—";}
}
