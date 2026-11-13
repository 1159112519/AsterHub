using System;
using System.Runtime.InteropServices;
// Transparent layered pixels hit Explorer. Observe the verified widget rectangle for wheel/right-click.
internal sealed class TaskbarHoverWheel:IDisposable {
 internal const int Message=0x8000+92,ContextMessage=0x8000+93;
 internal static bool LastInstallationSucceeded;
 delegate IntPtr MouseProc(int code,IntPtr message,IntPtr info);
 readonly MouseProc callback;readonly IntPtr surface,parent;IntPtr hook;bool rightPressed;
 [StructLayout(LayoutKind.Sequential)]struct Point{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct MouseInfo{public Point point;public uint data,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)]struct Rect{public int left,top,right,bottom;}
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,MouseProc proc,IntPtr module,uint thread);
 [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr h,int code,IntPtr message,IntPtr info);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out Rect rect);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr WindowFromPoint(Point point);
 [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr h,int message,IntPtr w,IntPtr l);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 internal TaskbarHoverWheel(IntPtr surface,IntPtr parent){this.surface=surface;this.parent=parent;callback=OnMouse;hook=SetWindowsHookEx(14,callback,GetModuleHandle(null),0);LastInstallationSucceeded=hook!=IntPtr.Zero;}
 internal bool RoutePointer(int message,int delta,bool overSurface){
  if(message==0x0204&&overSurface){rightPressed=true;return true;}
  if(message==0x0205&&rightPressed){rightPressed=false;if(overSurface)PostMessage(surface,ContextMessage,IntPtr.Zero,IntPtr.Zero);return true;}
  if(message==0x020A&&overSurface&&delta!=0)return PostMessage(surface,Message,(IntPtr)delta,IntPtr.Zero);
  return false;
 }
 IntPtr OnMouse(int code,IntPtr message,IntPtr info){
  int kind=message.ToInt32();if(code>=0&&(kind==0x020A||kind==0x0204||kind==0x0205)){
   var input=(MouseInfo)Marshal.PtrToStructure(info,typeof(MouseInfo));Rect r;bool over=false;
   if(IsWindowVisible(surface)&&GetWindowRect(surface,out r)&&input.point.X>=r.left&&input.point.X<r.right&&input.point.Y>=r.top&&input.point.Y<r.bottom){IntPtr hit=WindowFromPoint(input.point);over=hit==surface||hit==parent||IsChild(parent,hit);}
   if(RoutePointer(kind,(short)(input.data>>16),over))return (IntPtr)1;
  }
  return CallNextHookEx(hook,code,message,info);
 }
 public void Dispose(){rightPressed=false;if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
}
