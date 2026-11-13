using System;
using System.Collections.Generic;
using System.IO;
public class Server {public bool IsLocal{get;set;}public string Name{get;set;}public string Host{get;set;}public string User{get;set;}public int Port{get;set;}public string KeyPath{get;set;}public string AuthMode{get;set;}public string ProtectedPassword{get;set;}[System.Xml.Serialization.XmlIgnore]public string Password{get;set;}public Server(){Name="";Host="";User="";Port=22;KeyPath="";AuthMode="auto";ProtectedPassword="";}public override string ToString(){return Name+" / "+Host;}public Server Copy(){return new Server{IsLocal=IsLocal,Name=Name,Host=Host,User=User,Port=Port,KeyPath=KeyPath,AuthMode=AuthMode,ProtectedPassword=ProtectedPassword,Password=Password};}}
public class Settings {public TerminalPreferences Terminal{get;set;}public string[] HistoryModuleOrder{get;set;}public double MiniLeft{get;set;}public double MiniTop{get;set;}public double MiniWidth{get;set;}public double MiniHeight{get;set;}public bool MiniPinned{get;set;}public int RetentionDays{get;set;}public int ArchiveLimitMB{get;set;}public List<Server> Servers{get;set;}public string ThemeName{get;set;}public string SelectedView{get;set;}public int Interval{get;set;}public bool CloseToTray{get;set;}public string SelectedName{get;set;}public Settings(){Terminal=new TerminalPreferences();HistoryModuleOrder=new[]{"curve","process","hardware"};MiniLeft=double.NaN;MiniTop=double.NaN;MiniWidth=360;MiniHeight=480;MiniPinned=true;Servers=new List<Server>();RetentionDays=30;ArchiveLimitMB=1024;ThemeName="graphite";SelectedView="CPU";Interval=5;CloseToTray=true;SelectedName="本机";}public Settings Copy(){var s=new Settings{Terminal=(Terminal??new TerminalPreferences()).Copy(),HistoryModuleOrder=HistoryModuleOrder==null?null:(string[])HistoryModuleOrder.Clone(),MiniLeft=MiniLeft,MiniTop=MiniTop,MiniWidth=MiniWidth,MiniHeight=MiniHeight,MiniPinned=MiniPinned,RetentionDays=RetentionDays,ArchiveLimitMB=ArchiveLimitMB,ThemeName=ThemeName,SelectedView=SelectedView,Interval=Interval,CloseToTray=CloseToTray,SelectedName=SelectedName};foreach(var x in Servers)s.Servers.Add(x.Copy());return s;}}
public class Gpu {public string Index,Name,Util,Used,Total,Temp,Fan,Power,Cap,Uuid,PowerMax,CapSource,Clock,MemoryClock;}
public class GpuProcess {public string Gpu,Pid,Type,Name,Memory,User="—",SharedMemory,MemorySource,CommandLine,IdentityToken,Path;}
public class CpuCore {public int Index;public double Util;}
public sealed class ResourceProcess {public int Pid;public string Name,Identity,Path;public double? Cpu;public long? ResidentBytes;}
public class Metrics {public List<ResourceProcess> ResourceProcesses;public string CpuFrequencySource;public double? CpuFrequencyMHz;public List<DiskActivity> Storage=new List<DiskActivity>();public NetworkMetrics Network;public List<StorageVolume> Volumes=new List<StorageVolume>();public string PrimaryStoragePath;public HardwareInfo Hardware;public List<CpuCore> CpuCores=new List<CpuCore>();public List<Gpu> Gpus=new List<Gpu>();public List<GpuProcess> Processes=new List<GpuProcess>();public string Cpu="—",Cores="—",Mem="—",Disk="—",Raw="",Error="";}

// NVIDIA's compute-app process_name is a reported executable path, not argv[0].
// Recover this only for a remote Linux compute row with a verified birth identity.
public static class GpuProcessPaths {
 public static string Recorded(GpuProcess process,bool remote){
  if(process==null)return null;
  if(!string.IsNullOrEmpty(process.Path))return process.Path;
  string value=process.Name;
  if(!remote||process.Type!="C"||!System.Text.RegularExpressions.Regex.IsMatch(process.IdentityToken??"",@"^[a-fA-F0-9-]+:\d+:\d+$")||string.IsNullOrEmpty(value)||value.Length>32768||value.Length<2||value[0]!='/')return null;
  foreach(char c in value)if(char.IsControl(c))return null;
  return value;
 }
}
