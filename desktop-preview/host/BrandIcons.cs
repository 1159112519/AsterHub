using System;
using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
public static class BrandIcons {
 public static readonly string[] Ids={"night","pearl","zones","ember","mint","glacier"};
 public static string Normalize(string id){return Array.IndexOf(Ids,id)>=0?id:"night";}
 public static string Asset(string id,string relative){string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","brand-icons");string path=Path.Combine(root,Normalize(id),relative);return File.Exists(path)?path:Path.Combine(root,"night",relative);}
 public static System.Drawing.Icon Tray(string id,string state){string mode="dark";try{using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")){if(key!=null&&Convert.ToInt32(key.GetValue("SystemUsesLightTheme",0))!=0)mode="light";}}catch{}try{return new System.Drawing.Icon(Asset(id,Path.Combine("tray",mode+"-"+state+".ico")),new System.Drawing.Size(32,32));}catch{try{return new System.Drawing.Icon(Asset("night",Path.Combine("tray",mode+"-neutral.ico")),new System.Drawing.Size(32,32));}catch{return System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);}}}
 public static BitmapFrame Window(string id){try{using(var input=File.OpenRead(Asset(id,"window.ico"))){return BitmapFrame.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);}}catch{if(id!="night")return Window("night");return null;}}
}
