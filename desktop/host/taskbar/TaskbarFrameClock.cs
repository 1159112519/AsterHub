using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
// Runs only during an animation. One pending message prevents a slow UI from building a queue.
internal sealed class TaskbarFrameClock:IDisposable {
 internal const int Message=0x8000+317;
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateWaitableTimerEx(IntPtr attributes,string name,uint flags,uint access);
 [DllImport("kernel32.dll")]static extern bool SetWaitableTimer(IntPtr timer,ref long due,int period,IntPtr completion,IntPtr arg,bool resume);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
 readonly IntPtr window;readonly AutoResetEvent wake=new AutoResetEvent(false);readonly Thread worker;volatile bool active,disposed;int pending;
 internal TaskbarFrameClock(IntPtr window){this.window=window;worker=new Thread(Run){IsBackground=true,Name="AsterHub animation clock"};worker.Start();}
 internal void Start(){if(disposed)return;active=true;wake.Set();}
 internal void Stop(){active=false;}
 internal void Acknowledge(){Interlocked.Exchange(ref pending,0);}
 void Run(){IntPtr timer=CreateWaitableTimerEx(IntPtr.Zero,null,2,0x1F0003);if(timer==IntPtr.Zero)timer=CreateWaitableTimerEx(IntPtr.Zero,null,0,0x1F0003);
  try{double period=Stopwatch.Frequency/64.0;while(!disposed){wake.WaitOne();double target=Stopwatch.GetTimestamp();while(active&&!disposed){target+=period;long remaining=(long)(target-Stopwatch.GetTimestamp());if(remaining>0){if(timer!=IntPtr.Zero){long due=-Math.Max(1,(long)(remaining*10000000.0/Stopwatch.Frequency));if(SetWaitableTimer(timer,ref due,0,IntPtr.Zero,IntPtr.Zero,false))WaitForSingleObject(timer,50);else Thread.Sleep(1);}else Thread.Sleep(Math.Max(1,(int)(remaining*1000.0/Stopwatch.Frequency)));}
    if(!active||disposed)break;if(Interlocked.CompareExchange(ref pending,1,0)==0&&!PostMessage(window,Message,IntPtr.Zero,IntPtr.Zero))Acknowledge();if(Stopwatch.GetTimestamp()-target>period)target=Stopwatch.GetTimestamp();
   }}
  }finally{if(timer!=IntPtr.Zero)CloseHandle(timer);}
 }
 public void Dispose(){if(disposed)return;disposed=true;active=false;wake.Set();worker.Join();wake.Dispose();}
}
