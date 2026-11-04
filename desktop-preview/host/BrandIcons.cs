using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
public static class BrandIcons {
 [DllImport("user32.dll")]static extern int GetSystemMetrics(int index);
 [DllImport("user32.dll")]static extern int GetSystemMetricsForDpi(int index,uint dpi);
 [DllImport("user32.dll")]static extern uint GetDpiForSystem();
 public static int TrayPixelSize(){try{return Math.Max(16,Math.Min(64,GetSystemMetricsForDpi(49,GetDpiForSystem())));}catch{try{return Math.Max(16,Math.Min(64,GetSystemMetrics(49)));}catch{return 16;}}}
 public static readonly string[] Ids={"01","02","03","04","05","06","07","08","09"};
 public static bool Known(string id){return Array.IndexOf(Ids,id)>=0||Array.IndexOf(new[]{"night","pearl","zones","ember","mint","glacier"},id)>=0;}
 public static string Normalize(string id){int old=Array.IndexOf(new[]{"night","pearl","zones","ember","mint","glacier"},id);return old>=0?Ids[old]:Array.IndexOf(Ids,id)>=0?id:"01";}
 public static string Asset(string id,string relative){string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","brand-icons");string path=Path.Combine(root,Normalize(id),relative);return File.Exists(path)?path:Path.Combine(root,"01",relative);}
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadImage(IntPtr instance,string file,uint type,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
 static System.Drawing.Icon LoadTrayIcon(string file,int size){IntPtr handle=LoadImage(IntPtr.Zero,file,1,size,size,0x10);if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());try{using(var borrowed=System.Drawing.Icon.FromHandle(handle))return (System.Drawing.Icon)borrowed.Clone();}finally{DestroyIcon(handle);}}
 public static string State(string state){if(state=="connected")return "online";if(state=="disconnected")return "offline";return Array.IndexOf(new[]{"neutral","online","reconnecting","offline","error"},state)>=0?state:"neutral";}
 static string FamilyAsset(string id,params string[] relatives){string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","brand-icons");foreach(string family in new[]{Normalize(id),"01"})foreach(string relative in relatives){string path=Path.Combine(root,family,relative);if(File.Exists(path))return path;}throw new FileNotFoundException("应用图标资源不可用");}
 public static System.Drawing.Icon Tray(string id,string state){string mode="dark";try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){if(key!=null&&Convert.ToInt32(key.GetValue("SystemUsesLightTheme",0))!=0)mode="light";}}catch{}return TrayAt(id,state,mode,TrayPixelSize());}
 internal static System.Drawing.Icon TrayAt(string id,string state,string mode,int size){try{return LoadTrayIcon(FamilyAsset(id,Path.Combine("tray",mode+"-"+State(state)+".ico"),Path.Combine("tray",mode+"-neutral.ico"),"app.ico"),size);}catch{return System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);}}
 // The window ICO starts with a 16px frame. Loading surfaces must use the full raster.
 public static BitmapSource Launch(string id){try{var image=new BitmapImage();using(var input=File.OpenRead(Asset(id,"app-256.png"))){image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=input;image.EndInit();}image.Freeze();return image;}catch{if(Normalize(id)!="01")return Launch("01");return null;}}
 public static BitmapFrame Window(string id){try{using(var input=File.OpenRead(FamilyAsset(id,"window.ico","app.ico"))){var decoder=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);BitmapFrame best=decoder.Frames[0];foreach(var frame in decoder.Frames)if(frame.PixelWidth>best.PixelWidth)best=frame;best.Freeze();return best;}}catch{return null;}}
 static readonly ConditionalWeakTable<Window,NativeWindowIcons> nativeIcons=new ConditionalWeakTable<Window,NativeWindowIcons>();
 // Own only HICONs loaded here; WPF retains ownership of its independent handles.
 public static void ApplyWindow(Window window,string id){BrandShortcuts.SetProcessIdentity();window.Icon=Window(id);nativeIcons.GetValue(window,w=>new NativeWindowIcons(w)).Apply(id);}
 sealed class NativeWindowIcons:IDisposable{
  readonly Window owner;IntPtr hwnd,small,big;string selected,rebound;bool hooked,taskbarReady,refreshQueued,disposed;
  static readonly uint TaskbarButtonCreated=RegisterWindowMessage("TaskbarButtonCreated");
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string name);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadImage(IntPtr instance,string file,uint type,int width,int height,uint flags);
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wparam,IntPtr lparam);
  [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
  [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr hwnd);
  public NativeWindowIcons(Window window){owner=window;owner.SourceInitialized+=delegate{Apply(selected);};owner.Closed+=delegate{Dispose();};}
  public void Apply(string id){string next=Normalize(id);bool changed=next!=selected;selected=next;hwnd=new WindowInteropHelper(owner).Handle;if(hwnd==IntPtr.Zero)return;if(!hooked){var source=HwndSource.FromHwnd(hwnd);if(source!=null){source.AddHook(Observe);hooked=true;}}
   uint dpi=96;try{dpi=GetDpiForWindow(hwnd);}catch{}int smallSize=16,bigSize=32;try{smallSize=GetSystemMetricsForDpi(49,dpi);bigSize=GetSystemMetricsForDpi(11,dpi);}catch{}
   string file=FamilyAsset(selected,"window.ico","app.ico");IntPtr nextSmall=LoadImage(IntPtr.Zero,file,1,Math.Max(16,smallSize),Math.Max(16,smallSize),0x10),nextBig=LoadImage(IntPtr.Zero,file,1,Math.Max(32,bigSize),Math.Max(32,bigSize),0x10);if(nextSmall==IntPtr.Zero||nextBig==IntPtr.Zero){if(nextSmall!=IntPtr.Zero)DestroyIcon(nextSmall);if(nextBig!=IntPtr.Zero)DestroyIcon(nextBig);return;}
   SendMessage(hwnd,0x80,IntPtr.Zero,nextSmall);SendMessage(hwnd,0x80,new IntPtr(1),nextBig);if(small!=IntPtr.Zero)DestroyIcon(small);if(big!=IntPtr.Zero)DestroyIcon(big);small=nextSmall;big=nextBig;BrandShortcuts.ApplyWindow(hwnd,selected);if(changed&&taskbarReady)QueueTaskbarRefresh();
  }
  // Explorer may have created this button from an old pin/EXE cache during the first launch.
  // Defer shell calls until the window message returns; AddTab can send this message again.
  IntPtr Observe(IntPtr window,int message,IntPtr wparam,IntPtr lparam,ref bool handled){
   if(message==0x2){BrandShortcuts.ClearWindow(window);taskbarReady=false;}
   if(TaskbarButtonCreated!=0&&message==TaskbarButtonCreated){taskbarReady=true;QueueTaskbarRefresh();}
   return IntPtr.Zero;
  }
  void QueueTaskbarRefresh(){
   if(refreshQueued)return;refreshQueued=true;
   owner.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(delegate{
    refreshQueued=false;if(disposed||!taskbarReady||!owner.IsVisible||!owner.ShowInTaskbar||rebound==selected)return;
    Apply(selected);rebound=selected;
    object shell=null;
    try{
     shell=new BrandTaskbarList();var taskbar=(IBrandTaskbarList)shell;
     if(taskbar.HrInit()<0||taskbar.DeleteTab(hwnd)<0)return;
     if(taskbar.AddTab(hwnd)>=0&&GetForegroundWindow()==hwnd)taskbar.ActivateTab(hwnd);
    }catch(COMException){}catch(InvalidCastException){}
    finally{if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}
   }));
  }
  public void Dispose(){disposed=true;if(small!=IntPtr.Zero){DestroyIcon(small);small=IntPtr.Zero;}if(big!=IntPtr.Zero){DestroyIcon(big);big=IntPtr.Zero;}}
 }
}

[ComImport,Guid("56FDF344-FD6D-11D0-958A-006097C9A090")]
internal class BrandTaskbarList{}
[ComImport,Guid("56FDF342-FD6D-11D0-958A-006097C9A090"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IBrandTaskbarList{
 [PreserveSig]int HrInit();
 [PreserveSig]int AddTab(IntPtr hwnd);
 [PreserveSig]int DeleteTab(IntPtr hwnd);
 [PreserveSig]int ActivateTab(IntPtr hwnd);
 [PreserveSig]int SetActiveAlt(IntPtr hwnd);
}
