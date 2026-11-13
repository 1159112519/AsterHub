using System.Diagnostics;
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
 public static string DataRoot,SmokeRoot;public static int DebugPort;public static bool StartBackground,SmokeTaskbar;public static int RestartParent;
 [STAThread]public static int Main(string[] args){
  try{
   DataRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AsterHub","Preview1");
   for(int i=0;i<args.Length;i++){if(args[i]=="--restart-parent"&&i+1<args.Length)RestartParent=int.Parse(args[++i]);else if(args[i]=="--smoke-taskbar")SmokeTaskbar=true;else if(args[i]=="--background")StartBackground=true;else if(args[i]=="--data-root"&&i+1<args.Length)DataRoot=Path.GetFullPath(args[++i]);else if(args[i]=="--smoke"&&i+1<args.Length)SmokeRoot=Path.GetFullPath(args[++i]);else if(args[i]=="--debug-port"&&i+1<args.Length)DebugPort=int.Parse(args[++i]);}
   if(RestartParent>0){try{using(var parent=System.Diagnostics.Process.GetProcessById(RestartParent)){if(!parent.WaitForExit(90000))return 2;}}catch(ArgumentException){}}
   Directory.CreateDirectory(DataRoot);DiagnosticsLog.Configure(DataRoot);if(SmokeRoot!=null)Directory.CreateDirectory(SmokeRoot);
   SetDllDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"runtime","win-x64"));
   string instance;using(var sha=System.Security.Cryptography.SHA256.Create()){instance=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(DataRoot).ToLowerInvariant()))).Replace("-","").Substring(0,24);}bool first;
   using(var mutex=new Mutex(true,@"Local\AsterHubPreview-"+instance,out first)){
    if(!first){if(!StartBackground){try{using(var existing=EventWaitHandle.OpenExisting(@"Local\AsterHubPreviewToggle-"+instance))existing.Set();}catch(WaitHandleCannotBeOpenedException){try{using(var existing=EventWaitHandle.OpenExisting(@"Local\AsterHubPreviewRestore-"+instance))existing.Set();}catch{}}catch{}}return 0;}
    if(!StartBackground&&SmokeRoot==null&&args.Length==0&&WorkbenchElevation.TryRelaunchForeground()){mutex.ReleaseMutex();return 0;}
    using(var restore=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\AsterHubPreviewRestore-"+instance))
    using(var toggle=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\AsterHubPreviewToggle-"+instance)){
     var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};var window=new WorkbenchWindow();app.MainWindow=window;app.DispatcherUnhandledException+=delegate(object sender,System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e){DiagnosticsLog.Write("dispatcher",e.Exception);};
     var watcher=ThreadPool.RegisterWaitForSingleObject(restore,delegate{window.Dispatcher.BeginInvoke(new Action(window.RestoreFromTray));},null,Timeout.Infinite,false);
     var toggleWatcher=ThreadPool.RegisterWaitForSingleObject(toggle,delegate{window.Dispatcher.BeginInvoke(new Action(window.ToggleFromTaskbar));},null,Timeout.Infinite,false);
     app.SessionEnding+=delegate{window.ExitForSession();};try{return app.Run(window);}finally{watcher.Unregister(null);toggleWatcher.Unregister(null);mutex.ReleaseMutex();}
    }
   }
  }catch(Exception ex){DiagnosticsLog.Write("startup",ex);File.WriteAllText(Path.Combine(DataRoot??Path.GetTempPath(),"startup-error.txt"),ex.ToString());MessageBox.Show(UiLanguage.Text("AsterHub 无法启动：")+ex.Message,"AsterHub");return 1;}
 }
}
public sealed partial class WorkbenchWindow:Window {
 const string Origin="https://asterhub.local";
 readonly WebView2 web=new WebView2();readonly Server local=Storage.LocalServer();readonly DeviceRegistry devices;
 readonly Dictionary<string,Tuple<ConnectionResult,DateTime>> draftProbes=new Dictionary<string,Tuple<ConnectionResult,DateTime>>();
 readonly Dictionary<string,ConnectionResult> probes=new Dictionary<string,ConnectionResult>();readonly Dictionary<string,DateTime> probeTimes=new Dictionary<string,DateTime>();readonly HashSet<string> probing=new HashSet<string>();readonly SemaphoreSlim probeSlots=new SemaphoreSlim(2,2);
 readonly JavaScriptSerializer json=ArchiveStore.Json();readonly ArchiveService archive;readonly MemoryMaintenance memoryMaintenance=new MemoryMaintenance(WorkbenchHost.DataRoot);
 readonly object historyGate=new object();readonly Dictionary<string,CancellationTokenSource> historyQueries=new Dictionary<string,CancellationTokenSource>();
 readonly SemaphoreSlim reads=new SemaphoreSlim(2,2);readonly Dictionary<string,ArchivePoint> points=new Dictionary<string,ArchivePoint>();readonly object pointGate=new object();
 volatile DesktopPreferences preferences=DesktopPreferences.Load();DesktopSettings desktop;bool exitRequested,restartRequested;string appliedIconId=BrandIcons.Normalize(DesktopPreferences.Load().iconThemeId);
 LaunchSurface launch;readonly Stopwatch launchClock=Stopwatch.StartNew();
 DeviceCapture capture;volatile DeviceReading latest;bool ready,closed,shuttingDown;int interval=3;
 public WorkbenchWindow(){
  Title="AsterHub";BrandIcons.ApplyWindow(this,appliedIconId);Width=1360;Height=920;MinWidth=960;MinHeight=680;Background=new SolidColorBrush(Color.FromRgb(29,34,31));launch=new LaunchSurface(appliedIconId,NativeWindowTheme.Read(WorkbenchHost.DataRoot));web.Visibility=Visibility.Hidden;var launchGrid=new System.Windows.Controls.Grid();launchGrid.Children.Add(web);launchGrid.Children.Add(launch);Content=launchGrid;WindowStartupLocation=WindowStartupLocation.CenterScreen;if(WorkbenchHost.SmokeRoot!=null){ShowActivated=false;ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.Manual;Left=-18000;Top=-18000;}
  SetWebBackground(NativeWindowTheme.Read(WorkbenchHost.DataRoot));web.ZoomFactor=preferences.scale;
  devices=new DeviceRegistry(WorkbenchHost.DataRoot);interval=devices.Interval;SshAuthentication.KnownHostsPath=Path.Combine(WorkbenchHost.DataRoot,"known_hosts");
  archive=new ArchiveService(preferences.historyDays,preferences.historyLimitMB,Path.Combine(WorkbenchHost.DataRoot,"History"));
  var inventory=new DeviceInventory(WorkbenchHost.DataRoot);capture=new DeviceCapture(archive,Receive,inventory.Collect);capture.Configure(devices.All(),interval,preferences.localIntervalMs);StartLocalLightCapture();
  if(WorkbenchHost.SmokeRoot==null)BrandShortcuts.Apply(appliedIconId,true);
  SourceInitialized+=delegate{string theme=NativeWindowTheme.Read(WorkbenchHost.DataRoot);NativeWindowTheme.Apply(this,theme,false);SetWebBackground(theme);AttachMinimizeIntent();};
  Loaded+=async delegate{desktop=new DesktopSettings(this,command=>Send(new{type="navigate",value=command}),RequestExit,appliedIconId);desktop.ApplyTheme(NativeWindowTheme.Read(WorkbenchHost.DataRoot));await Initialize();StartTaskbar();StartStorageIndexWarmup();if(!IsExiting&&(WorkbenchHost.StartBackground||preferences.startMinimized))Hide();};
  StateChanged+=delegate{if(WindowState==WindowState.Minimized){if(minimizeIntent.Consume()&&preferences.minimizeToTray)Hide();}else minimizeIntent.Reset();PublishTelemetryVisibility();};IsVisibleChanged+=delegate{PublishTelemetryVisibility();};Closing+=ClosingWindow;
 }
 bool SetWebBackground(string theme){var appearance=NativeWindowTheme.Current(theme);var color=NativeWindowTheme.ColorValue(appearance.colors["background"]);bool glass=NativeWindowTheme.ApplyClientBackdrop(this,appearance);web.DefaultBackgroundColor=glass?System.Drawing.Color.FromArgb(0,0,0,0):System.Drawing.Color.FromArgb(255,color.R,color.G,color.B);return glass;}

 bool Trusted(string source){Uri uri;return Uri.TryCreate(source,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.Host=="asterhub.local"&&uri.IsDefaultPort;}
 async Task Initialize(){try{
  var options=new CoreWebView2EnvironmentOptions();if(WorkbenchHost.DebugPort>0&&WorkbenchHost.SmokeRoot!=null)options.AdditionalBrowserArguments="--remote-debugging-address=127.0.0.1 --remote-debugging-port="+WorkbenchHost.DebugPort;
  var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(WorkbenchHost.DataRoot,"WebView"),options);if(closed)return;await web.EnsureCoreWebView2Async(env);if(closed)return;
  var core=web.CoreWebView2;await core.AddScriptToExecuteOnDocumentCreatedAsync("globalThis.__ASTER_THEME="+json.Serialize(NativeWindowTheme.Read(WorkbenchHost.DataRoot))+";globalThis.__ASTER_ICON="+json.Serialize(appliedIconId)+";globalThis.__ASTER_APPEARANCE="+json.Serialize(NativeWindowTheme.Current(NativeWindowTheme.Read(WorkbenchHost.DataRoot)))+";");core.Settings.AreDevToolsEnabled=false;core.Settings.AreDefaultContextMenusEnabled=false;core.Settings.IsStatusBarEnabled=false;core.Settings.IsZoomControlEnabled=false;core.Settings.AreBrowserAcceleratorKeysEnabled=false;core.Settings.IsPasswordAutosaveEnabled=false;core.Settings.IsGeneralAutofillEnabled=false;
  core.PermissionRequested+=delegate(object s,CoreWebView2PermissionRequestedEventArgs e){e.State=CoreWebView2PermissionState.Deny;};core.NewWindowRequested+=delegate(object s,CoreWebView2NewWindowRequestedEventArgs e){e.Handled=true;};core.NavigationStarting+=delegate(object s,CoreWebView2NavigationStartingEventArgs e){if(!Trusted(e.Uri))e.Cancel=true;};core.DownloadStarting+=delegate(object s,CoreWebView2DownloadStartingEventArgs e){e.Cancel=true;};
  core.SetVirtualHostNameToFolderMapping("asterhub.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","workbench"),CoreWebView2HostResourceAccessKind.DenyCors);
  core.WebMessageReceived+=Message;core.ProcessFailed+=delegate{DiagnosticsLog.Write("webview-process-failed",null);ready=false;Content=new TextBlock{Text=UiLanguage.Text("界面进程已退出。采样仍在后台运行，请重新启动预览。"),Foreground=Brushes.White,Margin=new Thickness(24)};};
  core.NavigationCompleted+=async delegate(object s,CoreWebView2NavigationCompletedEventArgs e){if(!e.IsSuccess){if(e.WebErrorStatus!=CoreWebView2WebErrorStatus.OperationCanceled)ShowError(UiLanguage.Text("界面加载失败：")+e.WebErrorStatus);return;}ready=true;PublishTelemetryVisibility();SendLocalLightSnapshot();foreach(var device in devices.All()){var reading=capture.Latest(device);if(reading!=null)Send(new{type="capture",value=Snapshot(reading.Metrics,reading.TimeUtc,device)});}if(WorkbenchHost.SmokeRoot!=null)await Smoke();else BeginStartupUpdateCheck();};core.Navigate(Origin+"/index.html");
 }catch(Exception ex){ShowError(UiLanguage.Text("界面初始化失败：")+ex.Message);}}
 void ShowError(string text){Content=new TextBlock{Text=text,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24)};File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"startup-error.txt"),text);}
 static object Finite(double value){return double.IsNaN(value)||double.IsInfinity(value)?null:(object)value;}
 static double Milliseconds(DateTime time){return (time.ToUniversalTime()-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds;}
 object Snapshot(Metrics m,DateTime time,Server device=null,bool historical=false){device=device??local;var good=capture.LastSuccessful(device);return new{deviceId=DeviceRegistry.Id(device),time=Milliseconds(time),cpu=Finite(ResourceSample.ParsePercent(m.Cpu)),cpuFrequencySource=m.CpuFrequencySource,cpuFrequencyMHz=m.CpuFrequencyMHz,memory=Finite(ResourceSample.MemoryPercent(m.Mem)),mem=m.Mem,hardware=m.Hardware??(historical||good==null?null:good.Metrics.Hardware),cores=m.CpuCores,gpus=m.Gpus,processes=m.Processes,network=m.Network,storage=m.Storage,volumes=m.Volumes,error=devices.SafeError(m.Error,device),hardGap=TelemetryContinuity.IsDisconnect(m.Error),interval=capture.IntervalMilliseconds(device)/1000.0,lastGood=!historical&&!string.IsNullOrEmpty(m.Error)&&good!=null?Snapshot(good.Metrics,good.TimeUtc,device,true):null};}
 string ProbeIdentity(Server s){using(var sha=System.Security.Cryptography.SHA256.Create())return DeviceRegistry.Id(s)+"|"+s.AuthMode+"|"+s.KeyPath+"|"+Convert.ToBase64String(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(SshAuthentication.Password(s))));}
 object DeviceList(){return new{selected=devices.Selected,interval=interval,items=devices.All().Select(s=>DeviceView(s)).ToArray()};}
 object DeviceView(Server server){string id=DeviceRegistry.Id(server);var reading=capture.Latest(server);var good=capture.LastSuccessful(server);ConnectionResult probe;probes.TryGetValue(id,out probe);DateTime checkedAt;probeTimes.TryGetValue(id,out checkedAt);return new{device=devices.Public(server),state=reading==null?"connecting":string.IsNullOrEmpty(reading.Metrics.Error)?"connected":"retrying",lastSample=reading==null?null:(object)Milliseconds(reading.TimeUtc),lastSuccess=good==null?null:(object)Milliseconds(good.TimeUtc),probe=probe==null?null:new{success=probe.Success,milliseconds=probe.Success?(object)probe.Milliseconds:null,message=devices.SafeError(probe.Message,server),checkedAt=Milliseconds(checkedAt)}};}
 Server Requested(Dictionary<string,object> p){return devices.Find(p.ContainsKey("deviceId")?Convert.ToString(p["deviceId"]):devices.Selected);}
 readonly Dictionary<string,bool> reportedDeviceStates=new Dictionary<string,bool>(StringComparer.Ordinal);
 bool TelemetryWindowVisible{get{return IsVisible&&WindowState!=WindowState.Minimized;}}
 void PublishTelemetryVisibility(){storageWindowVisible=TelemetryWindowVisible;if(!closed)WebViewMemoryPolicy.Set(web.CoreWebView2,storageWindowVisible);if(ready&&!closed)Send(new{type="window-telemetry",value=TelemetryWindowVisible});}
 void Receive(DeviceReading reading){if(reading.Device.IsLocal)latest=reading;if(closed)return;Dispatcher.BeginInvoke(new Action(delegate{if(!closed&&ready){try{
  string id=DeviceRegistry.Id(reading.Device);var current=devices.Find(id);bool connected=string.IsNullOrEmpty(reading.Metrics.Error),previous;
  if(!reportedDeviceStates.TryGetValue(id,out previous)||previous!=connected){reportedDeviceStates[id]=connected;if(desktop!=null){var list=devices.All().Select(d=>capture.Latest(d)).Where(r=>r!=null).ToArray();desktop.RefreshStatus(list.Any(r=>!string.IsNullOrEmpty(r.Metrics.Error))?"reconnecting":list.Length>0?"online":"neutral");}Send(new{type="devices",value=DeviceList()});}
  Send(new{type="capture",value=Snapshot(reading.Metrics,reading.TimeUtc,current)});
 }catch(ArgumentException){}}}));}

 void Send(object value){if(closed||web.CoreWebView2==null)return;try{web.CoreWebView2.PostWebMessageAsJson(json.Serialize(value));}catch(Exception ex){File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"bridge-error.txt"),ex.GetBaseException().Message);}}
 async void Message(object sender,CoreWebView2WebMessageReceivedEventArgs e){
  if(closed||!Trusted(e.Source)||e.WebMessageAsJson.Length>400000)return;string id=null;
  try{
   var req=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);id=Convert.ToString(req["id"],CultureInfo.InvariantCulture);if(id.Length>80)throw new ArgumentException(UiLanguage.Text("请求编号无效"));string method=Convert.ToString(req["method"]);object payload;req.TryGetValue("params",out payload);var p=payload as Dictionary<string,object>??new Dictionary<string,object>();object result=null;
   if(e.WebMessageAsJson.Length>16384&&method!="clipboard.write")throw new ArgumentException(UiLanguage.Text("请求过长"));
   switch(method){
    case "app.openExternal":case "clipboard.write":case "clipboard.read":result=await DesktopRequest(method,p);break;
    case "app.uiReady":web.Visibility=Visibility.Visible;if(launch!=null){var grid=launch.Parent as System.Windows.Controls.Grid;if(grid!=null)grid.Children.Remove(launch);launch=null;}result=new{ready=true,elapsedMs=launchClock.ElapsedMilliseconds};break;
    case "app.info":result=new{name="AsterHub",version=AppUpdates.DisplayVersion,stage=UiLanguage.Text("Windows 正式版"),device=new{id="local",name=UiLanguage.Text("本机"),host=local.Host,user=local.User},dataRoot=WorkbenchHost.DataRoot,archiveRoot=archive.Store.Root,interval=interval};break;
    case "devices.list":result=DeviceList();break;
    case "devices.select":devices.Select(Convert.ToString(p["id"]));result=DeviceList();break;
    case "devices.save":var saved=devices.Save(p);string savedId=DeviceRegistry.Id(saved);reportedDeviceStates.Remove(savedId);Tuple<ConnectionResult,DateTime> tested;if(draftProbes.TryGetValue(ProbeIdentity(saved),out tested)){probes[savedId]=tested.Item1;probeTimes[savedId]=tested.Item2;}else{probes.Remove(savedId);probeTimes.Remove(savedId);}capture.Configure(devices.All(),interval,preferences.localIntervalMs);result=devices.Public(saved);Send(new{type="devices",value=DeviceList()});break;
    case "devices.remove":string removed=Convert.ToString(p["id"]);devices.Remove(removed);reportedDeviceStates.Remove(removed);CancelStorageForDevice(removed);await CloseDeviceTerminals(removed);probes.Remove(removed);probeTimes.Remove(removed);capture.Configure(devices.All(),interval,preferences.localIntervalMs);result=DeviceList();Send(new{type="devices",value=result});break;
    case "devices.keyFile":var picker=new Microsoft.Win32.OpenFileDialog{Title=UiLanguage.Text("选择 SSH 私钥"),Filter=UiLanguage.Text("私钥文件|*.*"),CheckFileExists=true,Multiselect=false};result=picker.ShowDialog(this)==true?picker.FileName:null;break;
    case "devices.test":
     var target=devices.Draft(p);string probeId=DeviceRegistry.Id(target);if(probing.Count>=4||!probing.Add(probeId))throw new InvalidOperationException(UiLanguage.Text("此连接正在测试，请稍候"));
     try{await probeSlots.WaitAsync();ConnectionResult checkedResult;try{checkedResult=await Task.Run(()=>ConnectionProbe.Run(target));}finally{probeSlots.Release();}
      if(draftProbes.Count>32)draftProbes.Clear();draftProbes[ProbeIdentity(target)]=Tuple.Create(checkedResult,DateTime.UtcNow);
      // A draft must never replace the status of a saved connection with different credentials.
      var existing=devices.All().FirstOrDefault(d=>DeviceRegistry.Id(d)==probeId);if(existing!=null&&existing.AuthMode==target.AuthMode&&existing.KeyPath==target.KeyPath&&SshAuthentication.Password(existing)==SshAuthentication.Password(target)){probes[probeId]=checkedResult;probeTimes[probeId]=DateTime.UtcNow;Send(new{type="devices",value=DeviceList()});}
      result=new{success=checkedResult.Success,milliseconds=checkedResult.Success?(object)checkedResult.Milliseconds:null,message=devices.SafeError(checkedResult.Message,target),details=devices.SafeError(checkedResult.Details,target)};
     }finally{probing.Remove(probeId);}break;
    case "memory.selection.save":case "memory.process.protect":case "memory.process.terminate":case "memory.status":case "memory.prepare":case "memory.cancel":case "memory.execute":case "memory.policy.save":result=await Task.Run(delegate{return memoryMaintenance.Request(method,p);});break;
    case "networkSpeed.start":case "networkSpeed.cancel":case "networkSpeed.request":case "networkSpeed.udp":result=await Task.Run(()=>NetworkSpeedRequest(method,p));break;
    case "process.inspect":result=await InspectProcess(p);break;
    case "process.terminate":result=await EndProcess(p);break;
    case "process.cancel":result=CancelProcess(p);break;
    case "updates.startup.state":result=StartupUpdatePromptState();break;
    case "updates.startup.answer":result=AnswerStartupUpdate(p);break;
    case "updates.preferences":case "updates.preferences.save":result=UpdatePreferenceRequest(method,p);break;
    case "updates.status":case "updates.cancel":case "updates.check":case "updates.download":case "updates.install":case "feedback.submit":result=await ServiceRequest(method,p);break;
    case "terminal.rename":case "terminal.list":case "terminal.create":case "terminal.read":case "terminal.write":case "terminal.resize":case "terminal.close":result=await TerminalRequest(method,p);break;
    case "storage.lease":case "storage.locations":case "storage.directories":case "storage.trash.delete":case "storage.jobs":case "storage.cleanup.jobs":case "storage.scan":case "storage.control":case "storage.status":case "storage.query":case "storage.copy":case "storage.trash":case "storage.trash.list":case "storage.trash.restore":result=await StorageRequest(method,p);break;
    case "files.list":case "files.prepare":case "files.start":case "files.transfers":case "files.cancel":case "files.undo":case "files.pick":result=await FilesRequest(method,p);break;
    case "snapshot.light":result=Requested(p).IsLocal?LocalLightSnapshot():null;break;
    case "snapshot.latest":var requested=Requested(p);var newest=capture.Latest(requested);result=newest==null?null:Snapshot(newest.Metrics,newest.TimeUtc,requested);break;
    case "recorder.status":result=await RecorderStatus(p);break;
    case "analysis.query":result=await QueryResourceAnalysis(p);break;
    case "history.query":result=await QueryHistory(p);break;
    case "history.cancel":result=CancelHistory(p);break;
    case "history.read":result=await ReadHistory(p);break;
    case "capture.interval":int seconds=Convert.ToInt32(p["seconds"]);if(seconds<2||seconds>60)throw new ArgumentOutOfRangeException("seconds");devices.SetInterval(seconds);interval=seconds;capture.Configure(devices.All(),interval,preferences.localIntervalMs);result=interval;break;
    case "window.appearance":NativeWindowTheme.ApplyResolved(this,p);bool glass=SetWebBackground(Convert.ToString(p["theme"]));if(desktop!=null)desktop.ApplyTheme(Convert.ToString(p["theme"]));result=new{glass=glass};break;
    case "window.theme":NativeWindowTheme.Apply(this,Convert.ToString(p["theme"]),true);SetWebBackground(Convert.ToString(p["theme"]));if(desktop!=null)desktop.ApplyTheme(Convert.ToString(p["theme"]));result=true;break;
    case "window.telemetry":result=TelemetryWindowVisible;break;
    case "window.minimize":MinimizeLikeCaption();break;
    case "window.maximize":WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;break;
    case "taskbar.status":result=new{host=taskbar==null?null:taskbar.Status(),snapshot=ReadTaskbar(p.ContainsKey("deviceId")?Convert.ToString(p["deviceId"]):null),systemTheme=BrandIcons.SystemLight()?"light":"dark"};break;
    case "desktop.settings":result=GetDesktopSettings();break;
    case "desktop.save":
     object localIntervalValue;int localInterval=p.TryGetValue("localIntervalMs",out localIntervalValue)?Convert.ToInt32(localIntervalValue):preferences.localIntervalMs;if(!DesktopPreferences.ValidLocalInterval(localInterval))throw new ArgumentOutOfRangeException("localIntervalMs");
     int days=Convert.ToInt32(p["historyDays"]),limit=Convert.ToInt32(p["historyLimitMB"]);if(days<1||days>365||limit<256||limit>16384)throw new ArgumentOutOfRangeException(UiLanguage.Text("历史保留范围"));
     double scale=Convert.ToDouble(p["scale"]);if(scale<.9||scale>1.3)throw new ArgumentOutOfRangeException(UiLanguage.Text("界面缩放"));string density=Convert.ToString(p["density"]);if(density!="comfortable"&&density!="compact")throw new ArgumentException(UiLanguage.Text("未知界面密度"));bool startup=Convert.ToBoolean(p["startup"]);if(startup!=DesktopSettings.StartupEnabled())DesktopSettings.SetStartup(startup);
     var candidate=preferences.Copy();object languageValue;if(p.TryGetValue("language",out languageValue)){string language=Convert.ToString(languageValue);if(language!="en"&&language!="zh-CN")throw new ArgumentException("Unsupported interface language");candidate.language=language;}candidate.closeToTray=Convert.ToBoolean(p["closeToTray"]);candidate.minimizeToTray=Convert.ToBoolean(p["minimizeToTray"]);candidate.startMinimized=Convert.ToBoolean(p["startMinimized"]);candidate.historyDays=days;candidate.historyLimitMB=limit;candidate.scale=scale;candidate.density=density;object selectedIcon;if(p.TryGetValue("iconThemeId",out selectedIcon)){string idValue=Convert.ToString(selectedIcon);if(!BrandIcons.Known(idValue))throw new ArgumentException(UiLanguage.Text("未知应用图标"));candidate.iconThemeId=BrandIcons.Normalize(idValue);}candidate.localIntervalMs=localInterval;object widget;if(p.TryGetValue("taskbar",out widget)&&widget!=null){candidate.taskbar=(json.Deserialize<TaskbarSettings>(json.Serialize(widget))??new TaskbarSettings()).Normalize();if(candidate.taskbar.deviceId!=preferences.taskbar.deviceId)devices.Find(candidate.taskbar.deviceId);
      var rawWidget=widget as Dictionary<string,object>;object activeEdited;
      if(rawWidget==null||!rawWidget.TryGetValue("activePageEdited",out activeEdited)||!Convert.ToBoolean(activeEdited))candidate.taskbar.SetActive(preferences.taskbar.PageId(preferences.taskbar.page));
      if(candidate.taskbar.Revision!=preferences.taskbar.Revision)throw new InvalidOperationException(UiLanguage.Text("任务栏布局已更新，请关闭设置后重新打开。"));
      if(candidate.taskbar.Key!=preferences.taskbar.Key||candidate.taskbar.page!=preferences.taskbar.page||candidate.taskbar.enabled!=preferences.taskbar.enabled)candidate.taskbar.layout["revision"]=checked(preferences.taskbar.Revision+1);}candidate.Save();bool reconfigureTaskbar=candidate.taskbar.Revision!=preferences.taskbar.Revision;preferences=candidate;UiLanguage.Set(candidate.language);if(taskbar!=null&&reconfigureTaskbar)taskbar.Configure(preferences.taskbar);if(localLightCapture!=null)localLightCapture.SetInterval(localInterval);capture.Configure(devices.All(),interval,localInterval);appliedIconId=BrandIcons.Normalize(preferences.iconThemeId);BrandIcons.ApplyWindow(this,appliedIconId);if(desktop!=null)desktop.ApplyIcon(appliedIconId);BrandShortcuts.Apply(appliedIconId);SetWebBackground(NativeWindowTheme.Read(WorkbenchHost.DataRoot));web.ZoomFactor=preferences.scale;archive.Store.Days=days;archive.Store.LimitMB=limit;result=GetDesktopSettings();break;
    case "desktop.status":result=new{trayVisible=desktop!=null&&desktop.Visible,windowVisible=IsVisible,menuOpen=desktop!=null&&desktop.MenuOpen};break;
    case "desktop.restore":desktop.Restore();break;
    case "desktop.menu":desktop.ShowMenu();break;
    case "desktop.closeMenu":desktop.CloseMenu();break;
    case "app.restartStatus":result=RestartStatus();break;
    case "app.restart":EnsureRestartSafe(p);restartRequested=true;Dispatcher.BeginInvoke(new Action(RequestExit));result=true;break;
    case "app.exit":RequestExit();break;
    case "window.close":Close();break;
    default:throw new InvalidOperationException(UiLanguage.Text("尚未开放此操作"));
   }
   Send(new{id=id,ok=true,result=result});
  }catch(Exception ex){Send(new{id=id,ok=false,error=UiLanguage.Text(ex.GetBaseException().Message)});}
 }
 async Task Smoke(){try{await web.CoreWebView2.ExecuteScriptAsync("document.documentElement.dataset.nativeHost='true'");File.WriteAllText(Path.Combine(WorkbenchHost.SmokeRoot,"host-ready.json"),json.Serialize(new{native=true,origin=Origin,dataRoot=WorkbenchHost.DataRoot}));}catch(Exception ex){File.WriteAllText(Path.Combine(WorkbenchHost.SmokeRoot,"host-error.txt"),ex.ToString());}}
 object GetDesktopSettings(){return new{language=preferences.language,taskbar=preferences.taskbar,closeToTray=preferences.closeToTray,minimizeToTray=preferences.minimizeToTray,startMinimized=preferences.startMinimized,historyDays=preferences.historyDays,historyLimitMB=preferences.historyLimitMB,localIntervalMs=preferences.localIntervalMs,scale=preferences.scale,density=preferences.density,startup=DesktopSettings.StartupEnabled(),iconThemeId=BrandIcons.Normalize(preferences.iconThemeId),appliedIconId=appliedIconId,restartRequired=preferences.iconThemeId!=appliedIconId};}
 public bool IsExiting{get{return exitRequested||shuttingDown||closed;}}
 public void ToggleFromTaskbar(){if(IsExiting)return;DiagnosticsLog.Write("window-taskbar-launch-toggle",null);if(desktop!=null)desktop.Toggle();else{var action=WindowClickCommands.ForTaskbarClick(IsExiting,IsVisible,WindowState==WindowState.Minimized);if(action==WindowClickAction.Minimize)MinimizeLikeCaption();else if(action==WindowClickAction.Restore)RestoreFromTray();}}
  public void RestoreFromTray(){if(IsExiting)return;if(desktop!=null)desktop.Restore();else{Show();Activate();}}
 public void ExitForSession(){RequestExit();}
 void RequestExit(){if(IsExiting)return;exitRequested=true;Close();}
 async void ClosingWindow(object sender,System.ComponentModel.CancelEventArgs e){if(closed)return;if(shuttingDown){e.Cancel=true;return;}if(!exitRequested&&preferences.closeToTray&&desktop!=null&&desktop.Visible){e.Cancel=true;Dispatcher.BeginInvoke(new Action(Hide));return;}e.Cancel=true;shuttingDown=true;IsEnabled=false;bool wasVisible=IsVisible;var previousState=WindowState;if(desktop!=null)desktop.SetExiting(true);await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Normal);Hide();try{await CleanupStorage();await CleanupFiles();}catch(IOException ex){shuttingDown=false;exitRequested=false;IsEnabled=true;if(desktop!=null)desktop.SetExiting(false);WindowState=wasVisible&&previousState!=WindowState.Minimized?previousState:WindowState.Normal;Show();Activate();System.Windows.MessageBox.Show(this,ex.Message,"AsterHub",MessageBoxButton.OK,MessageBoxImage.Information);return;}if(taskbar!=null){taskbar.Dispose();taskbar=null;}if(backgroundUpdate!=null)await backgroundUpdate.Stop();ClearProcessTargets();await DisposeTerminalSessions();ready=false;await Task.Run(delegate{memoryMaintenance.Dispose();StopLocalLightCapture();capture.Dispose();archive.Flush(10000);archive.Dispose();});closed=true;if(restartRequested){try{StartReplacement();}catch(Exception restartError){File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"restart-error.txt"),restartError.Message);System.Windows.MessageBox.Show(UiLanguage.Text("无法自动重启，请重新打开 AsterHub。图标选择已保存。"),"AsterHub");}}if(taskbar!=null)taskbar.Dispose();if(desktop!=null)desktop.Dispose();web.Dispose();Close();}
}
