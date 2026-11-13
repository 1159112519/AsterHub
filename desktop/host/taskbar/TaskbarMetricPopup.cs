using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Forms=System.Windows.Forms;
using Point=System.Drawing.Point;
using Size=System.Drawing.Size;

// HTML/CSS is rendered by Chromium at native scale, never redrawn through GDI+.
internal sealed class TaskbarMetricPopup:IDisposable {
 internal string Metric{get;private set;}
 internal string TriggerMetric{get;set;}
 internal TaskbarDetailContent Content{get;private set;}
 internal bool IsDisposed{get;private set;}
 internal bool Ready{get;private set;}
 internal bool Visible{get{return active&&!IsDisposed&&!VisuallyHidden;}}
 internal bool VisuallyHidden{get{return PopupCloak.Hidden(Window,hwnd);}}
 internal long PresentedAt{get;private set;}
 internal bool RendererReady{get{return initialized&&fonts&&!IsDisposed;}}
 internal string Text{get{return Window.Title;}}
 internal event EventHandler FormClosed;
 internal readonly Window Window;
 readonly WebView2 web=new WebView2();
 readonly Forms.Timer outside=new Forms.Timer{Interval=60};
 readonly JavaScriptSerializer json=new JavaScriptSerializer();
 Rectangle anchor,widget,work;bool dark;float dpi;Action open;
 readonly bool interactive,reusable;readonly string assets;IntPtr hwnd;object pending;bool initialized,fonts,closing,closeQueued,starting,active,requested;int panelHeight=430,generation;
 const int Margin=0;
 internal static string PrimaryMetric(string metric){return TaskbarDetailContent.Primary(metric);}
 internal static TaskbarMetricPopup Prepare(bool interactive=true){using(var scope=new PopupDpiScope()){
  var value=new TaskbarMetricPopup("cpu",Rectangle.Empty,Rectangle.Empty,true,1,null,interactive,true);value.Stage();return value;
 }}
 internal TaskbarMetricPopup(string metric,Rectangle anchor,Rectangle widget,bool dark,float dpi,Action open,bool interactive=true,bool reusable=false){
  this.interactive=interactive;this.reusable=reusable;
  assets=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","workbench");
  Window=new Window{Title="AsterHub detail",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=false,ShowInTaskbar=false,Topmost=interactive,ShowActivated=false,Width=330,Height=430,Content=web,WindowStartupLocation=WindowStartupLocation.Manual};
  web.DefaultBackgroundColor=Color.Transparent;web.ZoomFactor=1;
  Window.SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(Window).Handle;int rounded=2;DwmSetWindowAttribute(hwnd,33,ref rounded,4);int shadow=2;DwmSetWindowAttribute(hwnd,2,ref shadow,4);var margins=new Margins{left=1,right=1,top=1,bottom=1};DwmExtendFrameIntoClientArea(hwnd,ref margins);if(!interactive||!PopupCloak.Set(hwnd,true))Park();};
  Window.Loaded+=async (s,e)=>await Initialize();
  Window.Closing+=(s,e)=>{closing=true;Ready=false;active=false;outside.Stop();};
  Window.Deactivated+=(s,e)=>{if(Ready&&interactive)QueueClose();};
  Window.Closed+=(s,e)=>{IsDisposed=true;outside.Stop();outside.Dispose();web.Dispose();NotifyClosed();};
  Window.PreviewKeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Escape){e.Handled=true;Close();}};
  outside.Tick+=(s,e)=>{if(Forms.Control.MouseButtons!=Forms.MouseButtons.None&&!PanelBounds.Contains(Forms.Cursor.Position)&&!widget.Contains(Forms.Cursor.Position))Close();};
  if(!reusable)Begin(metric,anchor,widget,dark,dpi,open);
 }
 internal void Begin(string metric,Rectangle anchor,Rectangle widget,bool dark,float dpi,Action open){
  if(IsDisposed||closing)throw new ObjectDisposedException("TaskbarMetricPopup");
  if(active)Close();WebViewMemoryPolicy.Set(web.CoreWebView2,true);generation++;active=true;requested=false;closeQueued=false;Ready=false;pending=null;panelHeight=430;
  Metric=PrimaryMetric(metric);TriggerMetric=metric;this.anchor=anchor;this.widget=widget;this.dark=dark;this.dpi=Math.Max(1,dpi);this.open=open;work=Forms.Screen.FromRectangle(anchor).WorkingArea;
  Content=null;Window.Background=new System.Windows.Media.SolidColorBrush(dark?System.Windows.Media.Color.FromRgb(20,23,25):System.Windows.Media.Color.FromRgb(246,247,248));
 }
 void Park(){if(hwnd!=IntPtr.Zero)SetWindowPos(hwnd,IntPtr.Zero,-20000,-20000,0,0,0x0015);}
 void Stage(){using(var scope=new PopupDpiScope()){if(!Window.IsVisible){Park();Window.Show();}}}
 internal Rectangle PanelBounds{get{int width=(int)Math.Ceiling(Math.Min(330,work.Width/dpi)*dpi);var size=new Size(width,(int)Math.Ceiling(panelHeight*dpi));return new Rectangle(Position(anchor,size,work),size);}}
 internal static Point Position(Rectangle anchor,Size size,Rectangle work){int left=anchor.Left+(anchor.Width-size.Width)/2,top=anchor.Top-size.Height-7;if(top<work.Top)top=anchor.Bottom+7;return new Point(Math.Max(work.Left,Math.Min(left,work.Right-size.Width)),Math.Max(work.Top,Math.Min(top,work.Bottom-size.Height)));}
 void Place(){if(hwnd==IntPtr.Zero||IsDisposed)return;using(var scope=new PopupDpiScope()){var b=PanelBounds;int m=(int)Math.Round(Margin*dpi);SetWindowPos(hwnd,IntPtr.Zero,interactive?b.Left-m:-20000,interactive?b.Top-m:-20000,b.Width+2*m,b.Height+2*m,0x0014);}}
 async Task Initialize(){if(starting||IsDisposed||closing)return;starting=true;try{
  var env=await PopupWebViewRuntime.ForWindowAsync(hwnd);if(IsDisposed||closing)return;await web.EnsureCoreWebView2Async(env);if(IsDisposed||closing)return;
  var core=web.CoreWebView2;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.AreDevToolsEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=false;core.Settings.IsPinchZoomEnabled=false;
  core.SetVirtualHostNameToFolderMapping("asterhub-taskbar.invalid",assets,CoreWebView2HostResourceAccessKind.DenyCors);
  core.NavigationStarting+=(s,e)=>{if(!e.Uri.StartsWith("https://asterhub-taskbar.invalid/",StringComparison.OrdinalIgnoreCase))e.Cancel=true;};core.NewWindowRequested+=(s,e)=>e.Handled=true;
  core.WebMessageReceived+=(s,e)=>{if(e.Source.StartsWith("https://asterhub-taskbar.invalid/",StringComparison.OrdinalIgnoreCase))Message(e.WebMessageAsJson);};
  core.NavigationCompleted+=(s,e)=>{if(!e.IsSuccess)Fail(new Exception("Local detail navigation failed: "+e.WebErrorStatus));};
  core.ProcessFailed+=(s,e)=>Fail(new Exception(e.ProcessFailedKind.ToString()));
  web.Source=new Uri("https://asterhub-taskbar.invalid/taskbar-detail/index.html");
 }catch(Exception ex){Fail(ex);}}
 void Fail(Exception ex){if(IsDisposed||closing)return;DiagnosticsLog.Write("taskbar-detail-html",ex);Window.Dispatcher.BeginInvoke(new Action(Dispose));}
 void Message(string raw){if(IsDisposed||closing)return;try{var m=json.DeserializeObject(raw) as System.Collections.Generic.Dictionary<string,object>;if(m==null||!m.ContainsKey("type"))return;string type=Convert.ToString(m["type"]);
  if(type=="ready"){fonts=m.ContainsKey("fonts")&&Convert.ToBoolean(m["fonts"]);initialized=true;WebViewMemoryPolicy.Set(web.CoreWebView2,active);if(active&&requested)Render();else if(reusable&&(!interactive||!PopupCloak.Set(hwnd,true)))Window.Hide();return;}
  if(!active||!requested||(!m.ContainsKey("session")?reusable:Convert.ToInt32(m["session"])!=generation))return;
  if(type=="close"){QueueClose();return;}
  if(type=="open"){var callback=open;int request=generation;Window.Dispatcher.BeginInvoke(new Action(()=>{if(!active||request!=generation)return;Close();if(callback!=null)callback();}));return;}
  if(type=="size"&&m.ContainsKey("height")){
   int size=Convert.ToInt32(m["height"]);if(size<=0)return;
   panelHeight=Math.Max(100,Math.Min((int)(work.Height/dpi)-14,size));Place();
   bool first=!Ready;Ready=fonts;if(first&&Ready){
    if(interactive)PopupCloak.Set(hwnd,false);
    PresentedAt=System.Diagnostics.Stopwatch.GetTimestamp();if(interactive){outside.Start();int request=generation;
     Window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{if(active&&Ready&&request==generation)Window.Activate();}));}
   }
  }
 }catch(Exception ex){DiagnosticsLog.Write("taskbar-detail-html-message",ex);}}
 void Send(object value){if(!IsDisposed&&!closing&&web.CoreWebView2!=null)web.CoreWebView2.PostWebMessageAsJson(json.Serialize(value));}
 void Render(){if(pending!=null)Send(new{type="render",session=generation,value=pending});}
 internal void RefreshMetric(TaskbarSnapshot snapshot,TaskbarHistory history){if(!active||IsDisposed||closing)return;Content=new TaskbarDetailContent(Metric,snapshot,history);var m=Content;Window.Title=m.Title;Func<TaskbarDisplay,object> display=d=>new{text=d.Text,unit=d.Unit};Func<TaskbarDetailContent.Row[],object> rows=a=>a.Select(r=>new{label=r.Label,value=r.Value}).ToArray();
  pending=new{language=UiLanguage.Current,kind=m.Kind,title=m.Title,device=m.Device,subject=m.Subject,window=m.Window,scale=m.Scale,main=display(m.Main),secondary=display(m.Secondary),dual=m.Dual,rows=rows(m.Rows),legend=rows(m.Legend),fraction=m.Fraction,cores=m.Cores,series=m.Series.Select(a=>a.Select(v=>new{time=(v.Time.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds,value=v.Value,breakBefore=v.BreakBefore}).ToArray()).ToArray(),ceiling=m.Ceiling,end=((history.DetailEnd==DateTime.MinValue?DateTime.UtcNow:history.DetailEnd).ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds,dark=dark,bodyLimit=Math.Min(620,Math.Max(80,(int)(work.Height/dpi)-76)),width=Math.Min(330,(int)(work.Width/dpi))};if(initialized&&requested)Render();
 }
 internal void Show(){if(!active||IsDisposed||closing)return;requested=true;Stage();if(initialized)Render();}
 void QueueClose(){if(!active||IsDisposed||closing||closeQueued)return;closeQueued=true;int request=generation;Window.Dispatcher.BeginInvoke(new Action(()=>{if(active&&request==generation)Close();}));}
 void NotifyClosed(){var handler=FormClosed;if(handler!=null)handler(this,EventArgs.Empty);}
 internal void Close(){if(!active||IsDisposed||closing)return;active=false;requested=false;Ready=false;outside.Stop();pending=null;open=null;Content=null;if(reusable){WebViewMemoryPolicy.Set(web.CoreWebView2,false);if(!interactive||!PopupCloak.Set(hwnd,true))Window.Hide();NotifyClosed();}else Dispose();}
 public void Dispose(){if(IsDisposed||closing)return;closing=true;active=false;requested=false;Ready=false;outside.Stop();pending=null;open=null;Window.Close();}
 internal async Task CaptureAsync(string path){if(!Ready)throw new InvalidOperationException("HTML popup is not ready");using(var file=File.Create(path))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);}
 internal async Task<string> InspectAsync(string script){return await web.CoreWebView2.ExecuteScriptAsync(script);}
 [StructLayout(LayoutKind.Sequential)]struct Margins{public int left,right,top,bottom;}
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
 [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,ref Margins margins);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
}
