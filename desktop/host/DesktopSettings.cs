using System;using System.IO;using System.Collections.Generic;using System.Reflection;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Effects;using Microsoft.Win32;
public sealed class DesktopPreferences {
 public TaskbarSettings taskbar=new TaskbarSettings();
 public bool closeToTray=true,minimizeToTray=false,startMinimized=false;public int historyDays=30,historyLimitMB=256,localIntervalMs=2000;public string language="zh-CN";public string density="comfortable",iconThemeId="01";public double scale=.9;
 public static DesktopPreferences Load(){try{var value=ArchiveStore.Json().Deserialize<DesktopPreferences>(File.ReadAllText(Path.Combine(WorkbenchHost.DataRoot,"preferences.json")))??new DesktopPreferences();value.taskbar=(value.taskbar??new TaskbarSettings()).Normalize();value.iconThemeId=BrandIcons.Normalize(value.iconThemeId);value.language=value.language=="en"?"en":"zh-CN";if(!ValidLocalInterval(value.localIntervalMs))value.localIntervalMs=2000;return value;}catch{return new DesktopPreferences{taskbar=new TaskbarSettings().Normalize()};}}
 public static bool ValidLocalInterval(int value){return value==500||value==1000||value==2000||value==3000||value==5000||value==10000;}
 public DesktopPreferences Copy(){return (DesktopPreferences)MemberwiseClone();}
 public void Save(){SaveTo(Path.Combine(WorkbenchHost.DataRoot,"preferences.json"));}
 internal void SaveTo(string path){
  string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";var json=ArchiveStore.Json();
  try{File.WriteAllText(temp,json.Serialize(this));if(File.Exists(path)){
    string backup=path+".pre-taskbar-0.4.2.bak";string original=File.ReadAllText(path);
    if(!File.Exists(backup)){
     bool needsBackup;try{var prior=json.Deserialize<DesktopPreferences>(original);needsBackup=prior==null||prior.taskbar==null||prior.taskbar.layout==null;}catch(ArgumentException){needsBackup=true;}catch(InvalidOperationException){needsBackup=true;}
     if(needsBackup)File.Copy(path,backup,false);
    }
    File.Replace(temp,path,null);
   }else File.Move(temp,path);
  }finally{if(File.Exists(temp))File.Delete(temp);}
 }
}
public sealed class DesktopSettings:IDisposable {
 readonly WorkbenchWindow owner;readonly System.Windows.Forms.NotifyIcon icon;readonly Action<string> showPage;readonly Action quit;NativeCompactMenu.Menu flyout;bool disposed;string theme="graphite",trayKey="",connectionState="neutral";string iconId;System.Drawing.Icon currentIcon;
 public DesktopSettings(WorkbenchWindow window,Action<string> show,Action exit,string selectedIcon){iconId=BrandIcons.Normalize(selectedIcon);owner=window;showPage=show;quit=exit;icon=new System.Windows.Forms.NotifyIcon{Icon=(currentIcon=BrandIcons.Tray(iconId)),Text="AsterHub",Visible=true};icon.MouseUp+=delegate(object s,System.Windows.Forms.MouseEventArgs e){if(e.Button==System.Windows.Forms.MouseButtons.Right)owner.Dispatcher.BeginInvoke(new Action(ShowMenu));else if(e.Button==System.Windows.Forms.MouseButtons.Left&&e.Clicks<=1)owner.Dispatcher.BeginInvoke(new Action(Toggle));};owner.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{if(disposed||owner.IsExiting||flyout!=null)return;try{flyout=NativeCompactMenu.Prepare();}catch(Exception ex){DiagnosticsLog.Write("native-menu-warmup",ex);}}));}
 public void RefreshStatus(string state){
  state=BrandIcons.State(state);if(state!=connectionState){connectionState=state;icon.Text=state=="online"?UiLanguage.Text("AsterHub · 正在监测"):state=="reconnecting"?UiLanguage.Text("AsterHub · 正在重连"):"AsterHub";}
  string key=iconId+"/"+BrandIcons.TrayPixelSize();if(key==trayKey)return;
  var next=BrandIcons.Tray(iconId);var old=currentIcon;icon.Icon=next;currentIcon=next;trayKey=key;if(old!=null)old.Dispose();
 }
 public bool Visible{get{return icon.Visible;}}public bool MenuOpen{get{return flyout!=null&&flyout.Active;}}
 public void Toggle(){if(!icon.Visible)return;var action=WindowClickCommands.ForTaskbarClick(owner.IsExiting,owner.IsVisible,owner.WindowState==WindowState.Minimized);if(action==WindowClickAction.Minimize){CloseMenu();owner.MinimizeLikeCaption();}else if(action==WindowClickAction.Restore)Restore();}
 public void Restore(){if(owner.IsExiting)return;CloseMenu();if(!owner.IsVisible)owner.Show();if(owner.WindowState==WindowState.Minimized)SystemCommands.RestoreWindow(owner);owner.Activate();}
 public void SetExiting(bool value){CloseMenu();icon.Visible=!value;}
 public void ApplyIcon(string value){iconId=BrandIcons.Normalize(value);trayKey="";RefreshStatus(connectionState);}
 public void ApplyTheme(string value){theme=value;CloseMenu();}
 public void CloseMenu(){if(flyout!=null)flyout.Dismiss();}
 public void ShowMenu(){if(owner.IsExiting)return;OpenMenu(new[]{
  new NativeCompactMenu.Entry("\uE80F",UiLanguage.Text("打开主窗口"),Restore),
  new NativeCompactMenu.Entry("\uE713",UiLanguage.Text("偏好设置"),()=>{Restore();showPage("preferences");}),
  new NativeCompactMenu.Entry("\uE946",UiLanguage.Text("关于 AsterHub"),()=>{Restore();showPage("about");}),null,
  new NativeCompactMenu.Entry("\uE8AC",UiLanguage.Text("退出 AsterHub"),quit)});}
 public void ShowTaskbarMenu(Action<int> flip,Action hide){if(owner.IsExiting)return;OpenMenu(new[]{
  new NativeCompactMenu.Entry("\uE80F",UiLanguage.Text("打开主窗口"),Restore),
  new NativeCompactMenu.Entry("\uE76B",UiLanguage.Text("上一页"),()=>flip(-1)),new NativeCompactMenu.Entry("\uE76C",UiLanguage.Text("下一页"),()=>flip(1)),null,
  new NativeCompactMenu.Entry("\uE713",UiLanguage.Text("小组件设置"),()=>{Restore();showPage("taskbar-settings");}),
  new NativeCompactMenu.Entry("\uE711",UiLanguage.Text("隐藏小组件"),hide)});}
 void OpenMenu(NativeCompactMenu.Entry[] entries){
  try{if(flyout==null||flyout.Disposed)flyout=NativeCompactMenu.Prepare();flyout.Open(theme,entries,null,true);}
  catch(Exception ex){DiagnosticsLog.Write("native-menu",ex);}
 }
 public static bool StartupEnabled(){using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){return key!=null&&Convert.ToString(key.GetValue("AsterHub"))==StartupCommand();}}
 static string StartupCommand(){return "\""+Assembly.GetExecutingAssembly().Location+"\" --background";}
 public static void SetStartup(bool value){if(WorkbenchHost.SmokeRoot!=null)throw new InvalidOperationException(UiLanguage.Text("隔离测试不修改开机启动项"));using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(value)key.SetValue("AsterHub",StartupCommand());else if(Convert.ToString(key.GetValue("AsterHub"))==StartupCommand())key.DeleteValue("AsterHub",false);}}
 public void Dispose(){disposed=true;if(flyout!=null){flyout.Dispose();flyout=null;}icon.Visible=false;icon.Dispose();if(currentIcon!=null){currentIcon.Dispose();currentIcon=null;}}
}
