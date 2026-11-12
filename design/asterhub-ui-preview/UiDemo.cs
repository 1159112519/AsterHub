using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
[assembly:System.Reflection.AssemblyTitle("AsterHub UI Demo")]
[assembly:System.Reflection.AssemblyProduct("AsterHub UI Demo")]
[assembly:System.Reflection.AssemblyVersion("0.4.3.2")]
internal static class UiDemo {
 [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
 const string Site="https://asterhub-demo.local/";
 static async Task<bool> Click(CoreWebView2 core,string label,string selector){return await core.ExecuteScriptAsync("(()=>{const b=[...document.querySelectorAll('"+selector+"')].find(x=>x.textContent.trim()==='"+label+"'&&!x.disabled);if(b)b.click();return !!b})()") == "true";}
 [STAThread] static int Main(string[] args) {
  string root=AppDomain.CurrentDomain.BaseDirectory, check=args.Length==2&&args[0]=="--verify"?Path.GetFullPath(args[1]):null;
  string profile=Path.Combine(Path.GetTempPath(),"AsterHub-UI-Demo-"+Guid.NewGuid().ToString("N"));
  var app=new Application(); var view=new WebView2{DefaultBackgroundColor=System.Drawing.Color.FromArgb(23,24,26)};
  var window=new Window{Title="AsterHub · 设计改进 UI 演示",Width=Math.Min(1280,SystemParameters.WorkArea.Width-32),Height=Math.Min(900,SystemParameters.WorkArea.Height-40),MinWidth=920,MinHeight=630,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=new SolidColorBrush(Color.FromRgb(23,24,26)),Content=view,ShowActivated=check==null,ShowInTaskbar=check==null};
  
  window.Closed+=(s,e)=>view.Dispose();
  window.Loaded+=async(s,e)=>{
   try {
    if(!File.Exists(Path.Combine(root,"ui","index.html")))throw new IOException("缺少 ui 资源，请保留完整的演示文件夹。");
    var env=await CoreWebView2Environment.CreateAsync(null,profile);
    await view.EnsureCoreWebView2Async(env);
    var handle=new WindowInteropHelper(window).Handle;
    Action applyScale=()=>{var dpi=GetDpiForWindow(handle);view.ZoomFactor=dpi>0?96.0/dpi:1.0;};
    applyScale();
    HwndSource.FromHwnd(handle).AddHook(delegate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled){
     if(message==0x02E0)window.Dispatcher.BeginInvoke(applyScale);
     return IntPtr.Zero;
    });
    var core=view.CoreWebView2;core.Settings.IsZoomControlEnabled=false;core.Settings.IsPinchZoomEnabled=false;
    core.Settings.IsWebMessageEnabled=false;core.Settings.AreHostObjectsAllowed=false;core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;
    core.NavigationStarting+=(a,b)=>{if(!b.Uri.StartsWith(Site,StringComparison.Ordinal))b.Cancel=true;};
    core.NewWindowRequested+=(a,b)=>{b.Handled=true;};core.PermissionRequested+=(a,b)=>{b.State=CoreWebView2PermissionState.Deny;};core.DownloadStarting+=(a,b)=>{b.Cancel=true;};
    core.AddWebResourceRequestedFilter("*",CoreWebView2WebResourceContext.All);
    var ui=Path.GetFullPath(Path.Combine(root,"ui"))+Path.DirectorySeparatorChar;
    core.WebResourceRequested+=(a,b)=>{
     var address=b.Request.Uri;
     if(!address.StartsWith(Site,StringComparison.Ordinal)){b.Response=env.CreateWebResourceResponse(null,403,"Demo only","Content-Type: text/plain");return;}
     var relative=Uri.UnescapeDataString(new Uri(address).AbsolutePath.TrimStart('/')).Replace('/',Path.DirectorySeparatorChar);
     var asset=Path.GetFullPath(Path.Combine(ui,relative));
     if(!asset.StartsWith(ui,StringComparison.OrdinalIgnoreCase)||!File.Exists(asset)){b.Response=env.CreateWebResourceResponse(null,404,"Not found","Content-Type: text/plain");return;}
     string mime;switch(Path.GetExtension(asset).ToLowerInvariant()){
      case ".html":mime="text/html; charset=utf-8";break;case ".js":mime="application/javascript; charset=utf-8";break;case ".css":mime="text/css; charset=utf-8";break;case ".svg":mime="image/svg+xml";break;case ".ttf":mime="font/ttf";break;case ".woff2":mime="font/woff2";break;default:mime="application/octet-stream";break;
     }
     var stream=new MemoryStream(File.ReadAllBytes(asset),false);
     b.Response=env.CreateWebResourceResponse(stream,200,"OK","Content-Type: "+mime+"\r\nX-Content-Type-Options: nosniff");
    };
    var loaded=new TaskCompletionSource<bool>(); CoreWebView2WebErrorStatus navError=CoreWebView2WebErrorStatus.Unknown; core.NavigationCompleted+=(a,b)=>{navError=b.WebErrorStatus;loaded.TrySetResult(b.IsSuccess);};
    core.Navigate(Site+"index.html");
    if(check!=null){
      if(await Task.WhenAny(loaded.Task,Task.Delay(20000))!=loaded.Task||!await loaded.Task)throw new Exception("Navigation: "+navError+" Source: "+core.Source);
      for(int i=0;i<90;i++){
        if(await core.ExecuteScriptAsync("Boolean(document.querySelector('[data-resource-analysis=cpu] .ev-app-static')&&!document.getElementById('launch-shell'))")=="true")break;
        if(i==89)throw new Exception("CPU fixture did not start");await Task.Delay(130);
      }
      await core.ExecuteScriptAsync("window.demoFonts=null;Promise.all([document.fonts.load('400 14px Inter','123'),document.fonts.load('400 14px HarmonyOS Sans SC','中文')]).then(v=>window.demoFonts=v.every(x=>x.length>0));true");
      string fonts="null";for(int f=0;f<35&&fonts=="null";f++){await Task.Delay(100);fonts=await core.ExecuteScriptAsync("window.demoFonts");}if(fonts!="true")throw new Exception("Missing interface fonts");
      string[] pages={"GPU","内存","网络"},keys={"gpu","memory","network"},tabs={"显存分析","内存分析","流量分析"};
      for(int i=0;i<pages.Length;i++){
        if(!await Click(core,pages[i],".sidebar button"))throw new Exception("Missing navigation: "+pages[i]);
        for(int j=0;j<35;j++){
          if(await core.ExecuteScriptAsync("Boolean(document.querySelector('[data-resource-analysis="+keys[i]+"] .study-range'))")=="true")break;
          if(j==34)throw new Exception("Missing range: "+keys[i]);
          await Click(core,tabs[i],"button");await Task.Delay(140);
        }
      }
      if(await core.ExecuteScriptAsync("(()=>{const b=document.querySelector('.task-center-trigger');b?.click();return !!b})()")!="true")throw new Exception("Missing task center");
      for(int i=0;i<40;i++){
        if(await core.ExecuteScriptAsync("document.querySelectorAll('.task-center-row').length>=3")=="true")break;
        if(i==39)throw new Exception("Missing mock task rows");await Task.Delay(120);
      }
      if(core.Settings.IsWebMessageEnabled||core.Settings.AreHostObjectsAllowed)throw new Exception("Host bridge not isolated");
      File.WriteAllText(check,"PASS: packaged WPF/WebView2, both interface fonts, four analysis surfaces, three mock task states, native bridge disabled.\r\n"+core.DocumentTitle);app.Shutdown(0);return;
    }
   }catch(Exception error){if(check!=null)File.WriteAllText(check,error.ToString());else MessageBox.Show(window,"无法启动 UI 演示。请确认演示目录完整，并且 Windows 已安装 Microsoft Edge WebView2 Runtime。\n\n"+error.Message,"AsterHub UI 演示",MessageBoxButton.OK,MessageBoxImage.Warning);app.Shutdown(1);}
  };
  int exit=app.Run(window);
  // Only remove the unique scratch profile created by this exact run.
  var final=Path.GetFullPath(profile);var expected=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar+"AsterHub-UI-Demo-";
  if(final.StartsWith(expected,StringComparison.OrdinalIgnoreCase)&&Directory.Exists(final))try{Directory.Delete(final,true);}catch(IOException){}catch(UnauthorizedAccessException){}
  return exit;
 }
}













