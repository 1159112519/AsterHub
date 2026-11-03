using System;
using System.IO;
using System.Runtime.InteropServices;
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
 public static System.Drawing.Icon Tray(string id,string state){string mode="dark";try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){if(key!=null&&Convert.ToInt32(key.GetValue("SystemUsesLightTheme",0))!=0)mode="light";}}catch{}try{return new System.Drawing.Icon(Asset(id,Path.Combine("tray",mode+"-"+state+".ico")),new System.Drawing.Size(TrayPixelSize(),TrayPixelSize()));}catch{try{return new System.Drawing.Icon(Asset("night",Path.Combine("tray",mode+"-neutral.ico")),new System.Drawing.Size(TrayPixelSize(),TrayPixelSize()));}catch{return System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);}}}
 public static BitmapFrame Window(string id){try{using(var input=File.OpenRead(Asset(id,"window.ico"))){return BitmapFrame.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}}catch{if(id!="01")return Window("01");return null;}}
}
