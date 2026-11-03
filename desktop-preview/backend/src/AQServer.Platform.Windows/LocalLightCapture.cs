using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

// This path never invokes WMI, nvidia-smi, SSH, inventory or the archive writer.
public sealed class LocalLightCounters {
 public ulong Idle,Kernel,User,TotalBytes,AvailableBytes;public DateTime TimeUtc;public bool CpuSupported=true;
}
public sealed class LocalLightReading {
 public long Sequence;public DateTime TimeUtc;public int IntervalMs;public double DurationMs;public double? Cpu;public ulong TotalBytes,AvailableBytes;public string Error;public bool CpuSupported=true;
}
public sealed class LocalLightCapture : IDisposable {
 [StructLayout(LayoutKind.Sequential)]struct MemoryState {public uint Length,Load;public ulong TotalPhys,AvailPhys,TotalPage,AvailPage,TotalVirtual,AvailVirtual,AvailExtended;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GlobalMemoryStatusEx(ref MemoryState state);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool GetSystemTimes(out ulong idle,out ulong kernel,out ulong user);
 [DllImport("kernel32.dll")]static extern ushort GetActiveProcessorGroupCount();
 readonly Action<LocalLightReading> publish;readonly Func<LocalLightCounters> read;readonly int intervalMs;readonly Timer timer;readonly object stoppedGate=new object();LocalLightCounters previous;
 volatile LocalLightReading latest,good;int busy,stopping,skipped;long sequence;
 public LocalLightReading Latest{get{return latest;}}public LocalLightReading LastSuccessful{get{return good;}}public int Skipped{get{return Volatile.Read(ref skipped);}}
 public LocalLightCapture(Action<LocalLightReading> callback,int milliseconds=500,Func<LocalLightCounters> provider=null,bool start=true){
  if(milliseconds<250||milliseconds>10000)throw new ArgumentOutOfRangeException("milliseconds");publish=callback;read=provider??ReadWindows;intervalMs=milliseconds;
  if(start)timer=new Timer(delegate{RequestRefresh();},null,0,milliseconds);
 }
 static LocalLightCounters ReadWindows(){
  var memory=new MemoryState();memory.Length=(uint)Marshal.SizeOf(typeof(MemoryState));ulong idle,kernel,user;
  var time=DateTime.UtcNow;
  if(!GlobalMemoryStatusEx(ref memory))throw new Win32Exception(Marshal.GetLastWin32Error());
  if(!GetSystemTimes(out idle,out kernel,out user))throw new Win32Exception(Marshal.GetLastWin32Error());
  // GetSystemTimes is group scoped on multi-group Windows. Keep the full collector CPU there.
  return new LocalLightCounters{CpuSupported=GetActiveProcessorGroupCount()<=1,TimeUtc=time,Idle=idle,Kernel=kernel,User=user,TotalBytes=memory.TotalPhys,AvailableBytes=memory.AvailPhys};
 }
 public void RequestRefresh(){
  if(Volatile.Read(ref stopping)!=0)return;
  if(Interlocked.CompareExchange(ref busy,1,0)!=0){Interlocked.Increment(ref skipped);return;}
  try{
   if(Volatile.Read(ref stopping)!=0)return;
   var watch=Stopwatch.StartNew();var output=new LocalLightReading{Sequence=Interlocked.Increment(ref sequence),TimeUtc=DateTime.UtcNow,IntervalMs=intervalMs};
   try{
    var value=read();if(value==null||value.TotalBytes==0||value.AvailableBytes>value.TotalBytes)throw new InvalidOperationException("Invalid memory counters");
    output.TimeUtc=value.TimeUtc==default(DateTime)?output.TimeUtc:value.TimeUtc;output.TotalBytes=value.TotalBytes;output.AvailableBytes=value.AvailableBytes;output.CpuSupported=value.CpuSupported;
    if(value.CpuSupported&&previous!=null&&previous.CpuSupported&&value.Kernel>=previous.Kernel&&value.User>=previous.User&&value.Idle>=previous.Idle){
     double total=(double)(value.Kernel-previous.Kernel)+(value.User-previous.User),idle=value.Idle-previous.Idle;
     if(total>0&&idle<=total)output.Cpu=Math.Max(0,Math.Min(100,100*(total-idle)/total));
    }
    previous=value;
   }catch(Exception error){output.Error=error.GetType().Name+": "+error.Message;previous=null;}
   output.DurationMs=watch.Elapsed.TotalMilliseconds;if(string.IsNullOrEmpty(output.Error))good=output;latest=output;
   if(Volatile.Read(ref stopping)==0&&publish!=null)try{publish(output);}catch{}
  }finally{Interlocked.Exchange(ref busy,0);lock(stoppedGate)Monitor.PulseAll(stoppedGate);}
 }
 public void Dispose(){
  if(Interlocked.Exchange(ref stopping,1)!=0)return;if(timer!=null)timer.Dispose();
  lock(stoppedGate){var until=DateTime.UtcNow.AddSeconds(5);while(Volatile.Read(ref busy)!=0&&DateTime.UtcNow<until)Monitor.Wait(stoppedGate,50);}
 }
}
