using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

// Both tray and widget menus use native-scale browser text, like metric details.
internal static class NativeCompactMenu {
 internal sealed class Entry {internal string Glyph,Text;internal Action Run;internal Entry(string glyph,string text,Action run){Glyph=glyph;Text=UiLanguage.Text(text);Run=run;}}
 [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)]struct MouseData {public NativePoint Point;public uint MouseDataValue,Flags,Time;public UIntPtr Extra;}
 [StructLayout(LayoutKind.Sequential)]struct MonitorInfo {public int Size;public NativeRect Monitor,Work;public uint Flags;}
 delegate IntPtr MouseProc(int code,IntPtr message,IntPtr data);
 [DllImport("user32.dll")]static extern bool GetCursorPos(out NativePoint point);
 [DllImport("user32.dll")]static extern IntPtr MonitorFromPoint(NativePoint point,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
 [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,MouseProc proc,IntPtr module,uint thread);
 [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr hook);
 [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string module);
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
 internal static Window Open(string theme,IEnumerable<Entry> entries,Action closed,bool systemAppearance=false,bool interactive=true){
  using(var dpi=new PopupDpiScope())return new Menu(false).Open(theme,entries,closed,systemAppearance,interactive);
 }
 internal static Menu Prepare(){using(var dpi=new PopupDpiScope()){var menu=new Menu(true);menu.Stage();return menu;}}
 internal sealed class Menu:IDisposable {
  const int TrayHeight=153;
  Entry[] entries;Action closed;bool interactive;
  readonly bool reusable;readonly JavaScriptSerializer json=new JavaScriptSerializer();
  readonly WebView2 web=new WebView2();readonly Window window;NativePoint cursor;NativeRect work;
  NativeWindowTheme.Appearance appearance;IntPtr handle,hook;internal string LastCloseReason;MouseProc mouseProc;
  bool disposed,armed,closing,closeQueued,starting,loaded,active;int height,generation;
  internal bool Disposed{get{return disposed||closing;}}
  internal bool Active{get{return active&&!Disposed;}}
  internal bool Ready{get{return loaded&&!Disposed;}}
  internal bool Presented{get{return Active&&armed;}}
  internal long PresentedAt{get;private set;}
  internal Window Host{get{return window;}}
  internal bool VisuallyHidden{get{return PopupCloak.Hidden(window,handle);}}
  internal Menu(bool reusable){
   this.reusable=reusable;
   window=new Window{Title="AsterHub menu",WindowStyle=WindowStyle.None,AllowsTransparency=false,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,ShowActivated=false,Topmost=reusable,Width=204,Height=TrayHeight,Content=web,UseLayoutRounding=true,SnapsToDevicePixels=true};
   window.SourceInitialized+=delegate{handle=new WindowInteropHelper(window).Handle;int corners=2;try{DwmSetWindowAttribute(handle,33,ref corners,4);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}if(!PopupCloak.Set(handle,true))Park();};
   window.Tag=this;
   window.Closing+=delegate{closing=true;armed=false;};
   window.Deactivated+=async delegate{if(!interactive||!armed)return;int request=generation;await Task.Delay(80);if(request!=generation||!Presented)return;var foreground=GetForegroundWindow();if(foreground!=handle&&GetAncestor(foreground,2)!=handle)QueueClose("deactivated");};
   window.PreviewKeyDown+=(s,e)=>{
    if(!Presented)return;string key=e.Key.ToString();
    if(key=="Escape"||key=="Tab"){e.Handled=true;QueueClose();}
    else if(key=="Down"||key=="Up"||key=="Home"||key=="End"||key=="Return"||key=="Space"){
     e.Handled=true;web.CoreWebView2.PostWebMessageAsJson(json.Serialize(new{type="key",session=generation,key=key}));
    }
   };
   window.Closed+=delegate{disposed=true;armed=false;active=false;Unhook();web.Dispose();NotifyClosed();};
   window.Loaded+=async(s,e)=>await Initialize();
  }
  void Park(){if(handle!=IntPtr.Zero)SetWindowPos(handle,IntPtr.Zero,-20000,-20000,0,0,0x15);}
  internal void Stage(){if(!window.IsVisible){Park();window.Show();}}
  internal Window Open(string theme,IEnumerable<Entry> items,Action onClosed,bool systemAppearance=false,bool interactive=true){
   if(Disposed)throw new ObjectDisposedException("Menu");
   if(Active)Dismiss();WebViewMemoryPolicy.Set(web.CoreWebView2,true);using(var dpi=new PopupDpiScope()){
    entries=items.ToArray();closed=onClosed;this.interactive=interactive;generation++;active=true;closeQueued=false;LastCloseReason=null;
    ReadAnchor();
    appearance=systemAppearance?NativeWindowTheme.SystemMenu(BrandIcons.SystemLight()):NativeWindowTheme.Current(theme);
    window.Background=new SolidColorBrush(NativeWindowTheme.ColorValue(appearance.colors["surface"]));height=12+entries.Sum(e=>e==null?9:34);
    window.Topmost=interactive;Stage();if(interactive)ObserveOutside();if(loaded)Render();return window;
   }
  }
  void ReadAnchor(){
   GetCursorPos(out cursor);var monitor=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(!GetMonitorInfo(MonitorFromPoint(cursor,2),ref monitor)){var b=System.Windows.Forms.Screen.PrimaryScreen.WorkingArea;monitor.Work=new NativeRect{Left=b.Left,Top=b.Top,Right=b.Right,Bottom=b.Bottom};}work=monitor.Work;
  }
  void Place(bool onscreen){using(var scope=new PopupDpiScope())for(int pass=0;pass<2;pass++){
    var dpi=VisualTreeHelper.GetDpi(window);int w=(int)Math.Ceiling(204*dpi.DpiScaleX),h=Math.Min(work.Bottom-work.Top,(int)Math.Ceiling(height*dpi.DpiScaleY));
    int x=Math.Max(work.Left,Math.Min(work.Right-w,cursor.X-w+(int)Math.Round(20*dpi.DpiScaleX))),y=Math.Max(work.Top,Math.Min(work.Bottom-h,cursor.Y-h+(int)Math.Round(12*dpi.DpiScaleY)));
    if(!onscreen){x=-20000;y=-20000;}
    NativeRect current;bool hasBounds=GetWindowRect(handle,out current);bool sameSize=hasBounds&&current.Right-current.Left==w&&current.Bottom-current.Top==h;
    if(!sameSize||current.Left!=x||current.Top!=y)SetWindowPos(handle,IntPtr.Zero,x,y,w,h,sameSize?0x15u:0x14u);
    if(VisualTreeHelper.GetDpi(window).DpiScaleX==dpi.DpiScaleX)break;
   }}
  void ObserveOutside(){if(!interactive||hook!=IntPtr.Zero)return;mouseProc=OnMouse;hook=SetWindowsHookEx(14,mouseProc,GetModuleHandle(null),0);if(hook==IntPtr.Zero)DiagnosticsLog.Write("native-menu-hook",new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));}
  void Unhook(){if(hook!=IntPtr.Zero){UnhookWindowsHookEx(hook);hook=IntPtr.Zero;}}
  void NotifyClosed(){var callback=closed;closed=null;if(callback!=null)callback();}
  void QueueClose(string reason="close"){if(!Active||closeQueued)return;closeQueued=true;int request=generation;window.Dispatcher.BeginInvoke(new Action(()=>{if(!Active||request!=generation)return;LastCloseReason=reason;Dismiss();}));}
  internal void Dismiss(){if(!Active)return;active=false;armed=false;closeQueued=false;entries=null;Unhook();if(reusable){WebViewMemoryPolicy.Set(web.CoreWebView2,false);if(!PopupCloak.Set(handle,true))window.Hide();NotifyClosed();}else Dispose();}
  public void Dispose(){if(Disposed)return;closing=true;active=false;armed=false;entries=null;Unhook();window.Close();}
  void Render(){web.CoreWebView2.PostWebMessageAsJson(json.Serialize(new{type="render",session=generation,colors=appearance.colors,entries=entries.Select((v,i)=>v==null?null:new{id=i,glyph=v.Glyph,text=v.Text}).ToArray()}));}
  async Task Initialize(){if(starting||Disposed)return;starting=true;try{
   var env=await PopupWebViewRuntime.ForWindowAsync(handle);if(Disposed)return;await web.EnsureCoreWebView2Async(env);if(Disposed)return;
   web.ZoomFactor=1;var core=web.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=false;core.Settings.IsPinchZoomEnabled=false;
   core.SetVirtualHostNameToFolderMapping("asterhub-menu.invalid",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","workbench"),CoreWebView2HostResourceAccessKind.DenyCors);
   core.NavigationStarting+=(s,e)=>{if(!e.Uri.StartsWith("https://asterhub-menu.invalid/",StringComparison.OrdinalIgnoreCase))e.Cancel=true;};core.NewWindowRequested+=(s,e)=>e.Handled=true;
   core.WebMessageReceived+=(s,e)=>{if(Disposed||!e.Source.StartsWith("https://asterhub-menu.invalid/",StringComparison.OrdinalIgnoreCase))return;Message(e.WebMessageAsJson);};
   core.NavigationCompleted+=(s,e)=>{if(!e.IsSuccess)Fail("native-menu-navigation",new Exception(e.WebErrorStatus.ToString()));};
   core.ProcessFailed+=(s,e)=>Fail("native-menu-renderer",new Exception(e.ProcessFailedKind.ToString()));
   web.Source=new Uri("https://asterhub-menu.invalid/compact-menu/index.html");
  }catch(Exception ex){Fail("native-menu",ex);}}
  void Fail(string scope,Exception error){if(Disposed)return;DiagnosticsLog.Write(scope,error);window.Dispatcher.BeginInvoke(new Action(Dispose));}
  void Message(string raw){
   var m=json.DeserializeObject(raw) as Dictionary<string,object>;if(m==null||!m.ContainsKey("type"))return;string type=Convert.ToString(m["type"]);
   if(type=="ready"){WebViewMemoryPolicy.Set(web.CoreWebView2,Active);
    if(!Active&&reusable){
     // Establish the default tray menu bounds while the initialized browser is still cloaked.
     if(PopupCloak.Set(handle,true)){ReadAnchor();height=TrayHeight;Place(true);}
     else window.Hide();
    }
    loaded=true;if(Active)Render();return;
   }
   if(!Active||!m.ContainsKey("session")||Convert.ToInt32(m["session"])!=generation)return;
   if(type=="shown"){
    height=Math.Max(30,Convert.ToInt32(m["height"]));Place(interactive);PopupCloak.Set(handle,false);PresentedAt=System.Diagnostics.Stopwatch.GetTimestamp();armed=true;
    if(interactive){int request=generation;window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{if(Presented&&request==generation)SetForegroundWindow(handle);}));}
   }else if(type=="close")QueueClose();else if(type=="run"&&m.ContainsKey("id")){
    int id=Convert.ToInt32(m["id"]);if(id<0||id>=entries.Length||entries[id]==null)return;var run=entries[id].Run;int request=generation;
    window.Dispatcher.BeginInvoke(new Action(()=>{if(!Active||request!=generation)return;Dismiss();if(run!=null)run();}));
   }
  }
  IntPtr OnMouse(int code,IntPtr message,IntPtr data){
   if(code>=0&&Active&&(armed||interactive)&&(message==(IntPtr)0x201||message==(IntPtr)0x204||message==(IntPtr)0x207||message==(IntPtr)0x20B)){
    var p=((MouseData)Marshal.PtrToStructure(data,typeof(MouseData))).Point;NativeRect b;if(GetWindowRect(handle,out b)&&(p.X<b.Left||p.X>=b.Right||p.Y<b.Top||p.Y>=b.Bottom))QueueClose("outside-click");
   }return CallNextHookEx(hook,code,message,data);
  }
 }
}
