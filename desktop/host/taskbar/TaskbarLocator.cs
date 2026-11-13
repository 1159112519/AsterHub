using System;
using System.Runtime.InteropServices;
using System.Windows.Automation;
internal static class TaskbarLocator {
 [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rt,B; public int W{get{return Rt-L;}} public int H{get{return B-T;}} public override string ToString(){return L+","+T+","+Rt+","+B;} }
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out R rect);
 [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpi);
 [DllImport("user32.dll")] public static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 public sealed class Layout {public IntPtr Parent;public int X,Y,W,H;public float Scale;public int Available;public string Reason;public R Shell;public int Safe;public bool Valid{get{return Reason==null;}}}
 public static Layout Read(bool verbose,int slots=4){
  var prior=SetThreadDpiAwarenessContext((IntPtr)(-4));try{return ReadPhysical(verbose,TaskbarSettings.LogicalWidth(slots));}finally{if(prior!=IntPtr.Zero)SetThreadDpiAwarenessContext(prior);}
 }
 internal static Layout Read(TaskbarSettings options){
  var prior=SetThreadDpiAwarenessContext((IntPtr)(-4));try{return ReadPhysical(false,options.SqueezeWidth);}finally{if(prior!=IntPtr.Zero)SetThreadDpiAwarenessContext(prior);}
 }
 static Layout ReadPhysical(bool verbose,float desired){
  var h=FindWindow("Shell_TrayWnd",null); R r=new R(); var o=new Layout();o.Parent=h;
  if(h==IntPtr.Zero||!GetWindowRect(h,out r)||!IsWindowVisible(h)||r.W<500||r.H<25||r.H>130){o.Shell=r;o.Reason="unsupported, hidden or missing taskbar: hwnd="+h+" visible="+IsWindowVisible(h);return o;}o.Shell=r;
  var dpi=Math.Max(GetDpiForWindow(h),96u);var scale=dpi/96.0;int hh=(int)Math.Round(38*scale), inset=(int)Math.Round(8*scale);
  // Cache the properties in the same UIA query; don't make a cross-process call per button.
  var root=AutomationElement.FromHandle(h);var request=new CacheRequest{AutomationElementMode=AutomationElementMode.None};
  foreach(var property in new[]{AutomationElement.BoundingRectangleProperty,AutomationElement.IsOffscreenProperty,AutomationElement.AutomationIdProperty,AutomationElement.NameProperty})request.Add(property);
  AutomationElementCollection all;
  using(request.Activate())all=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
  int safe=int.MaxValue; bool start=false;
  foreach(AutomationElement e in all){var c=e.Cached;var b=c.BoundingRectangle;if(verbose)Console.WriteLine(c.AutomationId+" | "+c.Name+" | "+b+" | offscreen="+c.IsOffscreen);if(c.IsOffscreen||b.IsEmpty||b.Width<8||b.Width>r.W*.8||b.Right<=r.L||b.Left>=r.Rt||b.Bottom<=r.T||b.Top>=r.B)continue;
   if(c.AutomationId=="StartButton"||c.AutomationId=="Start"||c.Name=="Start"||c.Name=="开始")start=true;
   safe=Math.Min(safe,(int)Math.Floor(b.Left));
  }
  o.Safe=safe;
  if(!start||safe==int.MaxValue){o.Reason="no verified free space before first taskbar control (start="+start+", boundary="+safe+")";return o;}
  o.Scale=(float)scale;o.Available=Math.Max(0,safe-r.L-2*inset);
  o.X=inset;o.Y=Math.Max(0,(r.H-hh)/2);o.W=Math.Min(o.Available,(int)Math.Ceiling(desired*scale));o.H=Math.Min(hh,r.H);return o;
 }
}
