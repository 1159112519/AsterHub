using System;
using System.Runtime.InteropServices;
using System.Threading;
// Explorer changes app-button bounds over several frames. Observe that burst rather
// than discovering only its final rectangle on the ordinary low-rate safety probe.
internal sealed class TaskbarLayoutEvents:IDisposable {
 delegate void Callback(IntPtr hook,uint kind,IntPtr hwnd,int objectId,int childId,uint thread,uint time);
 [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,Callback callback,uint process,uint thread,uint flags);
 [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
 [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr window);
 readonly Callback callback;IntPtr hook;int dirty;
 internal TaskbarLayoutEvents(){callback=Changed;hook=SetWinEventHook(0x800B,0x800B,IntPtr.Zero,callback,0,0,2);}
 void Changed(IntPtr h,uint kind,IntPtr window,int obj,int child,uint thread,uint time){
  if(window==IntPtr.Zero)return;var taskbar=TaskbarLocator.FindWindow("Shell_TrayWnd",null);
  if(taskbar!=IntPtr.Zero&&(window==taskbar||IsChild(taskbar,window)))Interlocked.Exchange(ref dirty,1);
 }
 internal bool Consume(){return Interlocked.Exchange(ref dirty,0)!=0;}
 public void Dispose(){if(hook!=IntPtr.Zero){UnhookWinEvent(hook);hook=IntPtr.Zero;}GC.KeepAlive(callback);}
}
