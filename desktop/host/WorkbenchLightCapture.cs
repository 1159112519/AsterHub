using System;
using System.Threading;

public sealed partial class WorkbenchWindow {
 LocalLightCapture localLightCapture;volatile LocalLightReading localLightLatest;int localLightDispatchPending;
 void StartLocalLightCapture(){localLightCapture=new LocalLightCapture(ReceiveLocalLight,preferences.localIntervalMs);}
 object LocalLightSnapshot(){
  var reading=localLightLatest;if(reading==null)return null;
  return new{deviceId="local",source="windows-light",sequence=reading.Sequence,time=Milliseconds(reading.TimeUtc),intervalMs=reading.IntervalMs,durationMs=reading.DurationMs,cpuSupported=reading.CpuSupported,cpu=reading.Cpu.HasValue?(object)reading.Cpu.Value:null,totalBytes=string.IsNullOrEmpty(reading.Error)?(object)reading.TotalBytes:null,availableBytes=string.IsNullOrEmpty(reading.Error)?(object)reading.AvailableBytes:null,error=reading.Error??""};
 }
 void SendLocalLightSnapshot(){var value=LocalLightSnapshot();if(value!=null&&ready&&!closed)Send(new{type="capture-light",value=value});}
 void ReceiveLocalLight(LocalLightReading reading){
  localLightLatest=reading;
  if(closed||Dispatcher.HasShutdownStarted||Interlocked.CompareExchange(ref localLightDispatchPending,1,0)!=0)return;
  try{Dispatcher.BeginInvoke(new Action(delegate{Interlocked.Exchange(ref localLightDispatchPending,0);SendLocalLightSnapshot();}));}
  catch(InvalidOperationException){Interlocked.Exchange(ref localLightDispatchPending,0);}
 }
 void StopLocalLightCapture(){if(localLightCapture!=null)localLightCapture.Dispose();}
}
