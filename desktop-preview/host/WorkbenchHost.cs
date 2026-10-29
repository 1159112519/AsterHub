using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

public static class WorkbenchHost {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern bool SetDllDirectory(string path);
 public static string DataRoot,SmokeRoot;public static int DebugPort;public static bool StartBackground;
 [STAThread]public static int Main(string[] args){
  try{
   DataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AsterHub","Preview1");
   for(int i=0;i<args.Length;i++){if(args[i]=="--background")StartBackground=true;else if(args[i]=="--data-root"&&i+1<args.Length)DataRoot=Path.GetFullPath(args[++i]);else if(args[i]=="--smoke"&&i+1<args.Length)SmokeRoot=Path.GetFullPath(args[++i]);else if(args[i]=="--debug-port"&&i+1<args.Length)DebugPort=int.Parse(args[++i]);}
   Directory.CreateDirectory(DataRoot);if(SmokeRoot!=null)Directory.CreateDirectory(SmokeRoot);
   SetDllDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime","win-x64"));
   string instance;using(var sha=System.Security.Cryptography.SHA256.Create()){instance=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(DataRoot).ToLowerInvariant()))).Replace("-","").Substring(0,24);}bool first;
   using(var mutex=new Mutex(true,@"Local\AsterHubPreview-"+instance,out first)){
    if(!first){try{using(var existing=EventWaitHandle.OpenExisting(@"Local\AsterHubPreviewRestore-"+instance))existing.Set();}catch{}return 0;}
    using(var restore=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\AsterHubPreviewRestore-"+instance)){
     var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var window=new WorkbenchWindow();app.MainWindow=window;
     var watcher=ThreadPool.RegisterWaitForSingleObject(restore,delegate{window.Dispatcher.BeginInvoke(new Action(window.RestoreFromTray));},null,Timeout.Infinite,false);
     app.SessionEnding+=delegate{window.ExitForSession();};try{return app.Run(window);}finally{watcher.Unregister(null);mutex.ReleaseMutex();}
    }
   }
  }catch(Exception ex){File.WriteAllText(Path.Combine(DataRoot??Path.GetTempPath(),"startup-error.txt"),ex.ToString());MessageBox.Show("AsterHub 无法启动："+ex.Message,"AsterHub");return 1;}
 }
}
public sealed partial class WorkbenchWindow:Window {
 const string Origin="https://asterhub.local";
 readonly WebView2 web=new WebView2();readonly Server local=Storage.LocalServer();readonly PreviewDevices devices;
 readonly Dictionary<string,Tuple<ConnectionResult,DateTime>> draftProbes=new Dictionary<string,Tuple<ConnectionResult,DateTime>>();
 readonly Dictionary<string,ConnectionResult> probes=new Dictionary<string,ConnectionResult>();readonly Dictionary<string,DateTime> probeTimes=new Dictionary<string,DateTime>();readonly HashSet<string> probing=new HashSet<string>();readonly SemaphoreSlim probeSlots=new SemaphoreSlim(2,2);
 readonly JavaScriptSerializer json=ArchiveStore.Json();readonly ArchiveService archive;
 readonly SemaphoreSlim reads=new SemaphoreSlim(2,2);readonly Dictionary<string,ArchivePoint> points=new Dictionary<string,ArchivePoint>();readonly object pointGate=new object();
 readonly PreviewPreferences preferences=PreviewPreferences.Load();PreviewDesktop desktop;bool exitRequested;
 DeviceCapture capture;volatile DeviceReading latest;bool ready,closed,shuttingDown;int interval=3;
 public WorkbenchWindow(){
  Title="AsterHub · Windows 预览";Width=1360;Height=920;MinWidth=960;MinHeight=680;Background=new SolidColorBrush(Color.FromRgb(29,34,31));Content=web;WindowStartupLocation=WindowStartupLocation.CenterScreen;if(WorkbenchHost.SmokeRoot!=null){ShowActivated=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.Manual;Left=-18000;Top=-18000;}
  SetWebBackground(NativeWindowTheme.Read(WorkbenchHost.DataRoot));web.ZoomFactor=preferences.scale;
  devices=new PreviewDevices(WorkbenchHost.DataRoot);interval=devices.Interval;SshAuthentication.KnownHostsPath=Path.Combine(WorkbenchHost.DataRoot,"known_hosts");
  archive=new ArchiveService(preferences.historyDays,preferences.historyLimitMB,Path.Combine(WorkbenchHost.DataRoot,"History"));
  var inventory=new PreviewInventory(WorkbenchHost.DataRoot);capture=new DeviceCapture(archive,Receive,inventory.Collect);capture.Configure(devices.All(),interval);
  SourceInitialized+=delegate{NativeWindowTheme.Apply(this,NativeWindowTheme.Read(WorkbenchHost.DataRoot),false);};
  Loaded+=async delegate{desktop=new PreviewDesktop(this,command=>Send(new{type="navigate",value=command}),RequestExit);desktop.ApplyTheme(NativeWindowTheme.Read(WorkbenchHost.DataRoot));await Initialize();if(WorkbenchHost.StartBackground||preferences.startMinimized)Hide();};
  StateChanged+=delegate{if(WindowState==WindowState.Minimized&&preferences.minimizeToTray)Hide();};Closing+=ClosingWindow;
 }
 void SetWebBackground(string theme){int rgb=theme=="light"?0xf5f6f3:theme=="forest"?0x17231e:theme=="ocean"?0x1d232b:0x1c201f;web.DefaultBackgroundColor=System.Drawing.Color.FromArgb(255,(rgb>>16)&255,(rgb>>8)&255,rgb&255);Background=new SolidColorBrush(Color.FromRgb((byte)(rgb>>16),(byte)(rgb>>8),(byte)rgb));}
 bool Trusted(string source){Uri uri;return Uri.TryCreate(source,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="asterhub.local"&&uri.IsDefaultPort;}
 async Task Initialize(){try{
  var options=new CoreWebView2EnvironmentOptions();if(WorkbenchHost.DebugPort>0&&WorkbenchHost.SmokeRoot!=null)options.AdditionalBrowserArguments="--remote-debugging-address=127.0.0.1 --remote-debugging-port="+WorkbenchHost.DebugPort;
  var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(WorkbenchHost.DataRoot,"WebView"),options);if(closed)return;await web.EnsureCoreWebView2Async(env);if(closed)return;
  var core=web.CoreWebView2;await core.AddScriptToExecuteOnDocumentCreatedAsync("globalThis.__ASTER_THEME="+json.Serialize(NativeWindowTheme.Read(WorkbenchHost.DataRoot))+";");core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
  core.PermissionRequested+=delegate(object s,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};core.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;};core.NavigationStarting+=delegate(object s,CoreWebView2NavigationStartingEventArgs e){if(!Trusted(e.Uri))e.Cancel=true;};core.DownloadStarting+=delegate(object s,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;};
  core.SetVirtualHostNameToFolderMapping("asterhub.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","workbench"),CoreWebView2HostResourceAccessKind.DenyCors);
  core.WebMessageReceived+=Message;core.ProcessFailed+=delegate{ready=false;Content=new TextBlock{Text="界面进程已退出。采样仍在后台运行，请重新启动预览。",Foreground=Brushes.White,Margin=new Thickness(24)};};
  core.NavigationCompleted+=async delegate(object s,CoreWebView2NavigationCompletedEventArgs e){if(!e.IsSuccess){if(e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)ShowError("界面加载失败："+e.WebErrorStatus);return;}ready=true;foreach(var device in devices.All()){var reading=capture.Latest(device);if(reading!=null)Send(new{type="capture",value=Snapshot(reading.Metrics,reading.TimeUtc,device)});}if(WorkbenchHost.SmokeRoot!=null)await Smoke();};core.Navigate(Origin+"/index.html");
 }catch(Exception ex){ShowError("界面初始化失败："+ex.Message);}}
 void ShowError(string text){Content=new TextBlock{Text=text,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24)};File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"startup-error.txt"),text);}
 static object Finite(double value){return double.IsNaN(value)||double.IsInfinity(value)?null:(object)value;}
 static double Milliseconds(DateTime time){return (time.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds;}
 object Snapshot(Metrics m,DateTime time,Server device=null,bool historical=false){device=device??local;var good=capture.LastSuccessful(device);return new{deviceId=PreviewDevices.Id(device),time=Milliseconds(time),cpu=Finite(ResourceSample.ParsePercent(m.Cpu)),memory=Finite(ResourceSample.MemoryPercent(m.Mem)),mem=m.Mem,hardware=m.Hardware??(historical||good==null?null:good.Metrics.Hardware),cores=m.CpuCores,gpus=m.Gpus,processes=m.Processes,network=m.Network,storage=m.Storage,volumes=m.Volumes,error=devices.SafeError(m.Error,device),interval=interval};}
 string ProbeIdentity(Server s){using(var sha=System.Security.Cryptography.SHA256.Create())return PreviewDevices.Id(s)+"|"+s.AuthMode+"|"+s.KeyPath+"|"+Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(SshAuthentication.Password(s))));}
 object DeviceList(){return new{selected=devices.Selected,interval=interval,items=devices.All().Select(s=>DeviceView(s)).ToArray()};}
 object DeviceView(Server server){string id=PreviewDevices.Id(server);var reading=capture.Latest(server);var good=capture.LastSuccessful(server);ConnectionResult probe;probes.TryGetValue(id,out probe);DateTime checkedAt;probeTimes.TryGetValue(id,out checkedAt);return new{device=devices.Public(server),state=reading==null?"connecting":string.IsNullOrEmpty(reading.Metrics.Error)?"connected":"retrying",lastSample=reading==null?null:(object)Milliseconds(reading.TimeUtc),lastSuccess=good==null?null:(object)Milliseconds(good.TimeUtc),probe=probe==null?null:new{success=probe.Success,milliseconds=probe.Success?(object)probe.Milliseconds:null,message=devices.SafeError(probe.Message,server),checkedAt=Milliseconds(checkedAt)}};}
 Server Requested(Dictionary<string,object> p){return devices.Find(p.ContainsKey("deviceId")?Convert.ToString(p["deviceId"]):devices.Selected);}
 void Receive(DeviceReading reading){if(reading.Device.IsLocal)latest=reading;if(closed)return;Dispatcher.BeginInvoke(new Action(delegate{if(!closed&&ready){try{var current=devices.Find(PreviewDevices.Id(reading.Device));Send(new{type="capture",value=Snapshot(reading.Metrics,reading.TimeUtc,current)});Send(new{type="devices",value=DeviceList()});}catch(ArgumentException){}}}));}
 void Send(object value){if(closed||!ready)return;try{web.CoreWebView2.PostWebMessageAsJson(json.Serialize(value));}catch(Exception ex){File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"bridge-error.txt"),ex.GetBaseException().Message);}}
 async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){
  if(closed||!Trusted(e.Source)||e.WebMessageAsJson.Length>16384)return;string id=null;
  try{
   var req=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);id=Convert.ToString(req["id"],CultureInfo.InvariantCulture);if(id.Length>80)throw new ArgumentException("请求编号无效");string method=Convert.ToString(req["method"]);object payload;req.TryGetValue("params",out payload);var p=payload as Dictionary<string,object>??new Dictionary<string,object>();object result=null;
   switch(method){
    case "app.info":result=new{name="AsterHub",stage="Windows Preview 4",device=new{id="local",name="本机",host=local.Host,user=local.User},dataRoot=WorkbenchHost.DataRoot,archiveRoot=archive.Store.Root,interval=interval};break;
    case "devices.list":result=DeviceList();break;
    case "devices.select":devices.Select(Convert.ToString(p["id"]));result=DeviceList();break;
    case "devices.save":var saved=devices.Save(p);string savedId=PreviewDevices.Id(saved);Tuple<ConnectionResult,DateTime> tested;if(draftProbes.TryGetValue(ProbeIdentity(saved),out tested)){probes[savedId]=tested.Item1;probeTimes[savedId]=tested.Item2;}else{probes.Remove(savedId);probeTimes.Remove(savedId);}capture.Configure(devices.All(),interval);result=devices.Public(saved);Send(new{type="devices",value=DeviceList()});break;
    case "devices.remove":string removed=Convert.ToString(p["id"]);devices.Remove(removed);await CloseDeviceTerminals(removed);probes.Remove(removed);probeTimes.Remove(removed);capture.Configure(devices.All(),interval);result=DeviceList();Send(new{type="devices",value=result});break;
    case "devices.keyFile":var picker=new Microsoft.Win32.OpenFileDialog{Title="选择 SSH 私钥",Filter="私钥文件|*.*",CheckFileExists=true,Multiselect=false};result=picker.ShowDialog(this)==true?picker.FileName:null;break;
    case "devices.test":
     var target=devices.Draft(p);string probeId=PreviewDevices.Id(target);if(probing.Count>=4||!probing.Add(probeId))throw new InvalidOperationException("此连接正在测试，请稍候");
     try{await probeSlots.WaitAsync();ConnectionResult checkedResult;try{checkedResult=await Task.Run(()=>ConnectionProbe.Run(target));}finally{probeSlots.Release();}
      if(draftProbes.Count>32)draftProbes.Clear();draftProbes[ProbeIdentity(target)]=Tuple.Create(checkedResult,DateTime.UtcNow);
      // A draft must never replace the status of a saved connection with different credentials.
      var existing=devices.All().FirstOrDefault(d=>PreviewDevices.Id(d)==probeId);if(existing!=null&&existing.AuthMode==target.AuthMode&&existing.KeyPath==target.KeyPath&&SshAuthentication.Password(existing)==SshAuthentication.Password(target)){probes[probeId]=checkedResult;probeTimes[probeId]=DateTime.UtcNow;Send(new{type="devices",value=DeviceList()});}
      result=new{success=checkedResult.Success,milliseconds=checkedResult.Success?(object)checkedResult.Milliseconds:null,message=devices.SafeError(checkedResult.Message,target),details=devices.SafeError(checkedResult.Details,target)};
     }finally{probing.Remove(probeId);}break;
    case "process.inspect":result=await InspectProcess(p);break;
    case "process.terminate":result=await EndProcess(p);break;
    case "process.cancel":result=CancelProcess(p);break;
    case "updates.check":case "updates.download":case "updates.install":case "feedback.submit":result=await ServiceRequest(method,p);break;
    case "terminal.list":case "terminal.create":case "terminal.read":case "terminal.write":case "terminal.resize":case "terminal.close":result=await TerminalRequest(method,p);break;
    case "files.list":case "files.prepare":case "files.start":case "files.transfers":case "files.cancel":case "files.undo":case "files.pick":result=await FilesRequest(method,p);break;
    case "snapshot.latest":var requested=Requested(p);var newest=capture.Latest(requested);result=newest==null?null:Snapshot(newest.Metrics,newest.TimeUtc,requested);break;
    case "history.query":
     var historyDevice=Requested(p);string historyId=PreviewDevices.Id(historyDevice);
     int minutes=p.ContainsKey("minutes")?Convert.ToInt32(p["minutes"]):60;if(minutes<1||minutes>1440)throw new ArgumentOutOfRangeException("minutes");
     await reads.WaitAsync();try{result=await Task.Run<object>(delegate{var data=archive.Store.Query(historyDevice,DateTime.UtcNow.AddMinutes(-minutes),DateTime.UtcNow,CancellationToken.None);var displayed=PreviewHistory.Thin(data.Points);lock(pointGate){foreach(var point in displayed)points[(historyId=="local"?"":historyId+":")+point.T.ToString(CultureInfo.InvariantCulture)]=point;if(points.Count>10000){var keep=new HashSet<string>(displayed.Select(x=>(historyId=="local"?"":historyId+":")+x.T.ToString(CultureInfo.InvariantCulture)));foreach(var key in points.Keys.Where(k=>!keep.Contains(k)).ToArray())points.Remove(key);}}return new{total=data.Points.Count,points=displayed.Select(x=>new{id=(historyId=="local"?"":historyId+":")+x.T.ToString(CultureInfo.InvariantCulture),time=Milliseconds(new DateTime(x.T,DateTimeKind.Utc)),cpu=Finite(x.Cpu),memory=Finite(x.Memory),gpu=Finite(x.Gpu)}).ToArray(),skipped=data.Skipped};});}finally{reads.Release();}break;
    case "history.read":
     var readDevice=Requested(p);string readId=PreviewDevices.Id(readDevice);
     string archiveKey=Convert.ToString(p["key"]);if(readId=="local"?archiveKey.Contains(":"):!archiveKey.StartsWith(readId+":",StringComparison.Ordinal))throw new InvalidOperationException("快照不在当前设备索引中");ArchivePoint selected;lock(pointGate){if(!points.TryGetValue(archiveKey,out selected))throw new InvalidOperationException("快照不在当前索引中，请刷新历史");}
     await reads.WaitAsync();try{result=await Task.Run<object>(delegate{var record=archive.Store.Read(selected);return Snapshot(record.Metrics(),new DateTime(record.T,DateTimeKind.Utc),readDevice,true);});}finally{reads.Release();}break;
    case "capture.interval":int seconds=Convert.ToInt32(p["seconds"]);if(seconds<2||seconds>60)throw new ArgumentOutOfRangeException("seconds");devices.SetInterval(seconds);interval=seconds;capture.Configure(devices.All(),interval);result=interval;break;
    case "window.theme":SetWebBackground(Convert.ToString(p["theme"]));if(desktop!=null)desktop.ApplyTheme(Convert.ToString(p["theme"]));NativeWindowTheme.Apply(this,Convert.ToString(p["theme"]),true);result=true;break;
    case "window.minimize":WindowState=WindowState.Minimized;break;
    case "window.maximize":WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;break;
    case "desktop.settings":result=DesktopSettings();break;
    case "desktop.save":
     int days=Convert.ToInt32(p["historyDays"]),limit=Convert.ToInt32(p["historyLimitMB"]);if(days<1||days>365||limit<256||limit>16384)throw new ArgumentOutOfRangeException("历史保留范围");
     double scale=Convert.ToDouble(p["scale"]);if(scale<.9||scale>1.3)throw new ArgumentOutOfRangeException("界面缩放");string density=Convert.ToString(p["density"]);if(density!="comfortable"&&density!="compact")throw new ArgumentException("未知界面密度");bool startup=Convert.ToBoolean(p["startup"]);if(startup!=PreviewDesktop.StartupEnabled())PreviewDesktop.SetStartup(startup);
     preferences.closeToTray=Convert.ToBoolean(p["closeToTray"]);preferences.minimizeToTray=Convert.ToBoolean(p["minimizeToTray"]);preferences.startMinimized=Convert.ToBoolean(p["startMinimized"]);preferences.historyDays=days;preferences.historyLimitMB=limit;preferences.scale=scale;preferences.density=density;preferences.Save();SetWebBackground(NativeWindowTheme.Read(WorkbenchHost.DataRoot));web.ZoomFactor=preferences.scale;archive.Store.Days=days;archive.Store.LimitMB=limit;result=DesktopSettings();break;
    case "desktop.status":result=new{trayVisible=desktop!=null&&desktop.Visible,windowVisible=IsVisible,menuOpen=desktop!=null&&desktop.MenuOpen};break;
    case "desktop.restore":desktop.Restore();break;
    case "desktop.menu":desktop.ShowMenu();break;
    case "desktop.closeMenu":desktop.CloseMenu();break;
    case "app.exit":RequestExit();break;
    case "window.close":Close();break;
    default:throw new InvalidOperationException("尚未开放此操作");
   }
   Send(new{id=id,ok=true,result=result});
  }catch(Exception ex){Send(new{id=id,ok=false,error=ex.GetBaseException().Message});}
 }
 async Task Smoke(){try{await web.CoreWebView2.ExecuteScriptAsync("document.documentElement.dataset.nativeHost='true'");File.WriteAllText(Path.Combine(WorkbenchHost.SmokeRoot,"host-ready.json"),json.Serialize(new{native=true,origin=Origin,dataRoot=WorkbenchHost.DataRoot}));}catch(Exception ex){File.WriteAllText(Path.Combine(WorkbenchHost.SmokeRoot,"host-error.txt"),ex.ToString());}}
 object DesktopSettings(){return new{closeToTray=preferences.closeToTray,minimizeToTray=preferences.minimizeToTray,startMinimized=preferences.startMinimized,historyDays=preferences.historyDays,historyLimitMB=preferences.historyLimitMB,scale=preferences.scale,density=preferences.density,startup=PreviewDesktop.StartupEnabled()};}
 public void RestoreFromTray(){if(desktop!=null)desktop.Restore();else{Show();Activate();}}
 public void ExitForSession(){RequestExit();}
 void RequestExit(){exitRequested=true;Close();}
 async void ClosingWindow(object sender,System.ComponentModel.CancelEventArgs e){if(shuttingDown)return;if(!exitRequested&&preferences.closeToTray&&desktop!=null&&desktop.Visible){e.Cancel=true;Hide();return;}e.Cancel=true;shuttingDown=true;IsEnabled=false;try{await CleanupFiles();}catch(IOException ex){shuttingDown=false;exitRequested=false;IsEnabled=true;Show();Activate();System.Windows.MessageBox.Show(this,ex.Message,"AsterHub",MessageBoxButton.OK,MessageBoxImage.Information);return;}ClearProcessTargets();await DisposeTerminalSessions();ready=false;await Task.Run(delegate{capture.Dispose();archive.Flush(10000);archive.Dispose();});closed=true;if(desktop!=null)desktop.Dispose();web.Dispose();Close();}
}
