using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
// Shared transport sends only a Base64-encoded, fixed Python program and validated numeric arguments.
internal static class AqModuleSsh {
 internal static string Quote(string x){
  var b=new StringBuilder("\"");int slashes=0;
  foreach(char c in x??""){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);slashes=0;continue;}b.Append('\\',slashes);slashes=0;b.Append(c);}
  b.Append('\\',slashes*2);b.Append('\"');return b.ToString();
 }
 internal static string Run(Server server,string python) {
  var s=server.Copy();
  if(!Regex.IsMatch(s.Host??"",@"^[a-zA-Z0-9._:-]+$")||!Regex.IsMatch(s.User??"",@"^[a-zA-Z0-9._-]*$")||s.Port<1||s.Port>65535)throw new InvalidOperationException("服务器地址、用户或端口无效");
  string program=Convert.ToBase64String(Encoding.UTF8.GetBytes(python));
  string remote="python3 -c 'import base64;exec(base64.b64decode(\""+program+"\"))'";
  if(SshAuthentication.Managed(s))return SshAuthentication.Execute(s,remote,22);
  string args="-n -o BatchMode=yes -o ConnectTimeout=7 -o StrictHostKeyChecking=accept-new "+SshAuthentication.HostOptions+" -p "+s.Port;
  if(!string.IsNullOrWhiteSpace(s.KeyPath))args+=" -i "+Quote(Environment.ExpandEnvironmentVariables(s.KeyPath));
  args+=" -- "+Quote(string.IsNullOrWhiteSpace(s.User)?s.Host:s.User+"@"+s.Host)+" "+Quote(remote);
  using(var p=new Process{StartInfo=new ProcessStartInfo("ssh.exe",args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}}) {
   p.Start();var output=p.StandardOutput.ReadToEndAsync();var error=p.StandardError.ReadToEndAsync();
   if(!p.WaitForExit(22000)){try{p.Kill();}catch{}throw new TimeoutException("SSH 读取超时");}
   string text=output.Result;if(p.ExitCode!=0)throw new InvalidOperationException("SSH / Python 失败："+error.Result.Trim());return text;
  }
 }
}
public static class HardwareInventory {
 class CacheEntry {public HardwareInfo Info;public DateTime Expires;}
 static readonly object Gate=new object();static readonly Dictionary<string,CacheEntry> Cache=new Dictionary<string,CacheEntry>();
 static readonly Dictionary<string,object> EndpointGates=new Dictionary<string,object>();
 [DllImport("kernel32.dll")]static extern uint GetActiveProcessorCount(ushort group);
 static string Endpoint(Server s){return s.IsLocal?"local":(s.User??"")+"@"+(s.Host??"")+":"+s.Port+"|"+(s.KeyPath??"");}
 public static Task<HardwareInfo> GetAsync(Server s){var captured=s.Copy();return Task.Run(()=>Get(captured));}
 public static HardwareInfo Get(Server server){return GetCore(server,s=>s.IsLocal?CollectLocal():CollectRemote(s));}
 internal static HardwareInfo GetCore(Server server,Func<Server,HardwareInfo> collect) {
  var s=server.Copy();string key=Endpoint(s);object endpointGate;
  lock(Gate){CacheEntry e;if(Cache.TryGetValue(key,out e)&&e.Expires>DateTime.UtcNow)return e.Info;
   if(!EndpointGates.TryGetValue(key,out endpointGate))EndpointGates[key]=endpointGate=new object();}
  lock(endpointGate){
   lock(Gate){CacheEntry e;if(Cache.TryGetValue(key,out e)&&e.Expires>DateTime.UtcNow)return e.Info;}
   HardwareInfo info;try{info=collect(s);}catch(Exception ex){info=new HardwareInfo{Error=ex.Message,Source=s.IsLocal?"Windows WMI":"Linux SSH / Python3",CollectedUtc=DateTime.UtcNow};}
   if(info==null)info=new HardwareInfo{Error="未返回硬件信息"};
   lock(Gate){CacheEntry previous;if(!string.IsNullOrEmpty(info.Error)&&Cache.TryGetValue(key,out previous)&&previous.Info!=null&&string.IsNullOrEmpty(previous.Info.Error)){previous.Expires=DateTime.UtcNow.AddMinutes(1);return previous.Info;}Cache[key]=new CacheEntry{Info=info,Expires=DateTime.UtcNow.AddMinutes(string.IsNullOrEmpty(info.Error)?10:1)};}
   return info;
  }
 }
 static string Read(ManagementBaseObject x,string key){try{return Convert.ToString(x[key],CultureInfo.InvariantCulture).Trim();}catch{return "";}}
 static int Int(string x){int n;return int.TryParse(x,out n)?n:0;}
 static long Long(string x){long n;return long.TryParse(x,out n)?n:0;}
 static string Known(string x){return string.IsNullOrWhiteSpace(x)?"未提供":x;}
 public static string MemoryGeneration(int code){switch(code){case 20:return "DDR";case 21:return "DDR2";case 24:return "DDR3";case 26:return "DDR4";case 27:return "LPDDR";case 28:return "LPDDR2";case 29:return "LPDDR3";case 30:return "LPDDR4";case 34:return "DDR5";case 35:return "LPDDR5";default:return "未提供 (SMBIOS "+code+")";}}
 static string Speed(string raw){int value=Int(raw);return value>0?value+" (WMI Speed 原值，单位未校验)":"未提供";}
 static string ConfiguredSpeed(string raw){int value=Int(raw);return value>0?value+" (WMI ConfiguredClockSpeed 原值，单位未校验)":"未提供";}
 static HardwareInfo CollectLocal(){
  var h=new HardwareInfo{Source="Windows WMI / SMBIOS",CollectedUtc=DateTime.UtcNow};try{using(var q=new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))using(var os=q.Get()){foreach(ManagementObject row in os)using(row){h.OperatingSystem=Known(Read(row,"Caption"));break;}}}catch{}
  try{using(var q=new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor"))using(var rows=q.Get()) {
   var names=new List<string>();int sockets=0,cores=0,threads=0;bool coreKnown=true,threadKnown=true;
   foreach(ManagementObject row in rows){using(row){sockets++;names.Add(Read(row,"Name"));int c=Int(Read(row,"NumberOfCores")),t=Int(Read(row,"NumberOfLogicalProcessors"));coreKnown&=c>0;threadKnown&=t>0;cores+=c;threads+=t;}}
   h.CpuModel=Known(string.Join(" / ",names.Distinct()));if(sockets>0)h.PhysicalSockets=sockets;if(coreKnown&&cores>0)h.PhysicalCores=cores;if(threadKnown&&threads>0)h.SystemThreads=threads;
  }}catch(Exception ex){h.Note+="CPU WMI: "+ex.Message+"；";}
  try{uint active=GetActiveProcessorCount(0xffff);if(active>0&&active<=int.MaxValue)h.AvailableThreads=(int)active;}catch{}
  try{using(var q=new ManagementObjectSearcher("SELECT DeviceLocator,Manufacturer,PartNumber,Capacity,SMBIOSMemoryType,Speed,ConfiguredClockSpeed FROM Win32_PhysicalMemory"))using(var rows=q.Get()){
   long total=0;foreach(ManagementObject row in rows){using(row){long bytes=Long(Read(row,"Capacity"));total+=bytes;h.MemoryModules.Add(new MemoryModuleInfo{Slot=Known(Read(row,"DeviceLocator")),Manufacturer=Known(Read(row,"Manufacturer")),PartNumber=Known(Read(row,"PartNumber")),CapacityBytes=bytes,Generation=MemoryGeneration(Int(Read(row,"SMBIOSMemoryType"))),RatedSpeed=Speed(Read(row,"Speed")),ConfiguredSpeed=ConfiguredSpeed(Read(row,"ConfiguredClockSpeed"))});}}if(total>0)h.MemoryBytes=total;
  }}catch(Exception ex){h.Note+="内存模块 WMI: "+ex.Message+"；";}
  h.Note+="可用线程为系统在线逻辑处理器数，不代表空闲线程；硬件缓存约 10 分钟，采集时间见下。";return h;
 }
 internal const string LinuxProgram=@"import os,json,subprocess,re
h={'OperatingSystem':'未提供','CpuModel':'未提供','Source':'Linux lscpu / proc / dmidecode (无 sudo)','Note':'','Error':'','MemoryModules':[]}
def cmd(args):
 try:
  p=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.PIPE,text=True,timeout=6,env=dict(os.environ,LC_ALL='C'))
  return p.returncode,p.stdout,p.stderr
 except Exception as e:return -1,'',str(e)
try:
 with open('/etc/os-release') as f:
  aqos=dict(line.strip().split('=',1) for line in f if '=' in line)
  h['OperatingSystem']=aqos.get('PRETTY_NAME','Linux').strip(chr(34))
except Exception:pass
code,text,err=cmd(['lscpu','-J'])
if code==0:
 try:
  fields={r['field'].rstrip(':'):str(r['data']) for r in json.loads(text)['lscpu']}
  h['CpuModel']=fields.get('Model name','未提供')
  for out,key in [('PhysicalSockets','Socket(s)'),('SystemThreads','CPU(s)')]:
   if fields.get(key,'').isdigit():h[out]=int(fields[key])
 except Exception as e:h['Note']+='lscpu JSON: '+str(e)+'; '
code,text,err=cmd(['lscpu','-p=SOCKET,CORE'])
if code==0:
 pairs=set(tuple(x.split(',')) for x in text.splitlines() if x and not x.startswith('#') and '-' not in x)
 if pairs:h['PhysicalCores']=len(pairs)
try:
 h['AvailableThreads']=len(os.sched_getaffinity(0))
 h['Note']+='可用线程为 SSH 会话允许使用的逻辑 CPU 数，不代表空闲线程；'
except Exception:pass
try:
 with open('/proc/meminfo') as f:
  m=re.search(r'^MemTotal:\s+(\d+) kB',f.read(),re.M)
  if m:h['MemoryBytes']=int(m.group(1))*1024
except Exception as e:h['Note']+='proc/meminfo: '+str(e)+'; '
if h['CpuModel']=='未提供':
 try:
  with open('/proc/cpuinfo') as f:
   m=re.search(r'^(?:model name|Hardware)\s*:\s*(.+)',f.read(),re.M)
   if m:h['CpuModel']=m.group(1)
 except Exception:pass
code,text,err=cmd(['dmidecode','--type','17'])
if code==0:
 for block in re.split(r'\nHandle ',text):
  if 'Memory Device' not in block:continue
  fields={m.group(1).strip():m.group(2).strip() for m in re.finditer(r'^\s+([^:\n]+):\s*([^\n]*)$',block,re.M)}
  size=fields.get('Size','')
  if 'No Module Installed' in size:continue
  m=re.match(r'^(\d+)\s*(GB|MB|KB|TB)$',size)
  cap=int(m.group(1))*({'KB':1024,'MB':1024**2,'GB':1024**3,'TB':1024**4}[m.group(2)]) if m else 0
  def known(s):return s if s and s not in ('Unknown','Not Specified','Other','None') else '未提供'
  h['MemoryModules'].append({'Slot':known(fields.get('Locator','')),'Manufacturer':known(fields.get('Manufacturer','')),'PartNumber':known(fields.get('Part Number','')),'CapacityBytes':cap,'Generation':known(fields.get('Type','')),'RatedSpeed':known(fields.get('Speed','')),'ConfiguredSpeed':known(fields.get('Configured Memory Speed',fields.get('Configured Clock Speed','')))})
 if not h['MemoryModules']:h['Note']+='dmidecode 未提供已安装模块；'
else:h['Note']+='内存模块规格不可读取（dmidecode 不存在或当前用户权限不足）：'+err.strip()[:180]+'；'
h['Note']+='MemTotal 为操作系统可见内存；硬件缓存约 10 分钟。'
print(json.dumps(h,ensure_ascii=False))";
 static HardwareInfo CollectRemote(Server s){var h=new JavaScriptSerializer().Deserialize<HardwareInfo>(AqModuleSsh.Run(s,LinuxProgram));h.CollectedUtc=DateTime.UtcNow;return h;}
}
