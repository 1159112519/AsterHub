using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
public static class Remote {
 internal const string CommandMetadataProgram=@"import os,sys,base64

def identity(pid,boot):
 with open(""/proc/%d/stat""%pid) as f:text=f.read()
 fields=text[text.rfind("")"")+2:].split()
 return boot+"":""+fields[19]+"":""+str(os.stat(""/proc/%d""%pid).st_uid)

try:
 with open(""/proc/sys/kernel/random/boot_id"") as f:boot=f.read().strip()
except OSError:sys.exit(0)
pids=[]
for line in sys.stdin.read(1048576).splitlines():
 parts=line.split("","")
 if len(parts)>1 and parts[1].strip().isdigit():
  pid=int(parts[1].strip())
  if pid>4 and pid not in pids:pids.append(pid)
 if len(pids)>=256:break
budget=1048576
for pid in pids:
 try:
  token=identity(pid,boot)
  command=""-""
  try:
   with open(""/proc/%d/cmdline""%pid,""rb"") as f:raw=f.read(32769)
   if raw and len(raw)<=32768:command=base64.b64encode(raw).decode(""ascii"")
  except OSError:pass
  if identity(pid,boot)!=token:continue
  row=str(pid)+""\t""+token+""\t""+command
  if len(row)+1>budget:break
  print(row)
  budget-=len(row)+1
 except (OSError,ValueError,IndexError):continue
";
 internal static string CommandMetadataCommand {get{return "printf 'AQCOMMANDS\\n'; printf '%s\\n' \"$aq_procs\" | python3 -c "+"'"+CommandMetadataProgram.Replace("'","'\"'\"'")+"' 2>/dev/null; ";}}
 static string Q(string s){return "\""+s.Replace("\"","\\\"")+"\"";}
 public static Metrics Collect(Server s){if(s.IsLocal)return Local.Collect();var m=new Metrics();if(!Regex.IsMatch(s.Host??"",@"^[a-zA-Z0-9._:-]+$")||!Regex.IsMatch(s.User??"",@"^[a-zA-Z0-9._-]*$")||s.Port<1||s.Port>65535){m.Error="服务器地址、用户名或端口无效";return m;}
 // Only fixed diagnostic commands are sent; configuration values become SSH arguments.
 string script=PeripheralCollector.RemoteCommand+"printf 'AQCPU\\n'; awk 'BEGIN{while((getline line < \"/proc/stat\")>0){split(line,a);if(a[1] ~ /^cpu[0-9]*$/){for(i=2;i<=9;i++) t[a[1]]+=a[i];idle[a[1]]=a[5]+a[6]}}close(\"/proc/stat\");system(\"sleep 0.5\");while((getline line < \"/proc/stat\")>0){split(line,a);if(a[1] ~ /^cpu[0-9]*$/){total=0;for(i=2;i<=9;i++)total+=a[i];d=total-t[a[1]];busy=d>0?100*(d-(a[5]+a[6]-idle[a[1]]))/d:0;printf \"%s,%.1f\\n\",a[1],busy}}}'; printf 'AQFREQ\\n'; LC_ALL=C awk -F: '/cpu MHz/ {n++;s+=$2} END {if(n)printf \"%.1f\\n\",s/n}' /proc/cpuinfo 2>/dev/null; printf 'AQCORES\\n'; nproc 2>/dev/null; printf 'AQMEM\\n'; LC_ALL=C free -m 2>/dev/null | awk '/^Mem:/ {print $3\" / \"$2\" MiB\"}'; printf 'AQDISK\\n'; df -h / 2>/dev/null | awk 'NR==2{print $5\" used / \"$4\" free\"}'; printf 'AQGPU\\n'; if command -v nvidia-smi >/dev/null 2>&1; then nvidia-smi --query-gpu=index,name,utilization.gpu,memory.used,memory.total,temperature.gpu,fan.speed,power.draw,power.limit,uuid,power.max_limit --format=csv,noheader,nounits 2>/dev/null; fi; printf 'AQGPUCLOCK\\n'; if command -v nvidia-smi >/dev/null 2>&1; then nvidia-smi --query-gpu=uuid,clocks.current.graphics --format=csv,noheader,nounits 2>/dev/null; fi; printf 'AQPROC\\n'; if command -v nvidia-smi >/dev/null 2>&1; then aq_procs=\"$(nvidia-smi --query-compute-apps=gpu_uuid,pid,process_name,used_gpu_memory --format=csv,noheader,nounits 2>/dev/null)\"; printf '%s\\n' \"$aq_procs\"; fi; printf 'AQUSERS\\n'; ps -eo pid=,user:64= 2>/dev/null; "+CommandMetadataCommand+"printf 'AQEND\\n'";
 if(SshAuthentication.Managed(s)){try{m.Raw=SshAuthentication.ExecuteMonitoring(s,script,14);}catch(Exception ex){m.Error=ex.Message;return m;}}else{
 var args="-n -o BatchMode=yes -o ConnectTimeout=7 -o StrictHostKeyChecking=accept-new "+SshAuthentication.HostOptions+" -p "+s.Port+" ";if(!string.IsNullOrWhiteSpace(s.KeyPath))args+="-i "+Q(Environment.ExpandEnvironmentVariables(s.KeyPath))+" ";var dest=string.IsNullOrWhiteSpace(s.User)?s.Host:s.User+"@"+s.Host;args+="-- "+Q(dest)+" "+Q(script);
 var p=new Process{StartInfo=new ProcessStartInfo("ssh.exe",args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}};try{p.Start();var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(14000)){try{p.Kill();}catch{}m.Error="SSH 连接超时";return m;}m.Raw=stdout.Result;string err=stderr.Result;if(p.ExitCode!=0){m.Error=err.Trim();if(m.Error.Length>220)m.Error=m.Error.Substring(0,220);if(m.Error.Length==0)m.Error="SSH 连接失败 ("+p.ExitCode+")";return m;}}
 catch(Exception ex){m.Error=ex.Message;return m;}finally{p.Dispose();}
 }
 var commands=new Dictionary<string,string>();var identities=new Dictionary<string,string>();var owners=new Dictionary<string,string>();string section="";foreach(var raw in m.Raw.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){string line=raw.Trim();if(Regex.IsMatch(line,@"^AQ(CPU|FREQ|CORES|MEM|DISK|GPU|GPUCLOCK|PROC|USERS|COMMANDS|NETTIME|NET|VOLUMES|END)$")){section=line;continue;}PeripheralCollector.ParseRemote(m,section,line);if(section=="AQCOMMANDS"){var pair=line.Split('\t');if(pair.Length==3&&System.Text.RegularExpressions.Regex.IsMatch(pair[1],@"^[a-fA-F0-9-]+:\d+:\d+$")){try{string command=pair[2]=="-"?null:Encoding.UTF8.GetString(Convert.FromBase64String(pair[2])).Replace('\0',' ').Trim();if(command==null||command.Length<=32768){commands[pair[0]]=command;identities[pair[0]]=pair[1];}}catch(FormatException){}}}if(section=="AQUSERS"){var owner=Regex.Match(line,@"^(\d+)\s+(\S+)$");if(owner.Success)owners[owner.Groups[1].Value]=owner.Groups[2].Value;}if(section=="AQCPU"){var a=line.Split(',');if(a.Length==2){if(a[0]=="cpu")m.Cpu=a[1]+"%";else{int index;double usage;if(int.TryParse(a[0].Substring(3),out index)&&double.TryParse(a[1],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out usage))m.CpuCores.Add(new CpuCore{Index=index,Util=Math.Max(0,Math.Min(100,usage))});}}}if(section=="AQFREQ"){double mhz;if(double.TryParse(line,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out mhz)&&mhz>0)m.CpuFrequencyMHz=mhz;}if(section=="AQGPUCLOCK"){var fields=line.Split(',');if(fields.Length==2){var target=m.Gpus.Find(g=>g.Uuid==fields[0].Trim());if(target!=null)target.Clock=fields[1].Trim();}}if(section=="AQCORES")m.Cores=line;if(section=="AQMEM")m.Mem=line;if(section=="AQDISK")m.Disk=line;if(section=="AQGPU"){var a=line.Split(',');if(a.Length>=9)m.Gpus.Add(new Gpu{Index=a[0].Trim(),Name=a[1].Trim(),Util=a[2].Trim(),Used=a[3].Trim(),Total=a[4].Trim(),Temp=a[5].Trim(),Fan=a[6].Trim(),Power=a[7].Trim(),Cap=a[8].Trim(),Uuid=a.Length>=10?a[9].Trim():"",PowerMax=a.Length>=11?a[10].Trim():null});}if(section=="AQPROC"){var a=line.Split(',');if(a.Length>=4){string uuid=a[0].Trim();var gpu=m.Gpus.Find(x=>x.Uuid==uuid);m.Processes.Add(new GpuProcess{Gpu=gpu==null?"?":gpu.Index,Pid=a[1].Trim(),Type="C",Name=string.Join(",",a,2,a.Length-3).Trim(),Memory=a[a.Length-1].Trim()+" MiB"});}}}foreach(var process in m.Processes){string command;if(commands.TryGetValue(process.Pid,out command)){process.CommandLine=command;process.IdentityToken=identities[process.Pid];}string owner;if(owners.TryGetValue(process.Pid,out owner))process.User=owner;}m.Hardware=HardwareInventory.Get(s);return m;}
}
