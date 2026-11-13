using System;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
public static class Startup {
 public static string ExecutablePath{get{var path=Environment.GetEnvironmentVariable("AQSERVER_LAUNCHER_PATH");return string.IsNullOrWhiteSpace(path)?Assembly.GetExecutingAssembly().Location:path;}}
 const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
 public static bool Enabled(){try{using(var k=Registry.CurrentUser.OpenSubKey(Key)){return k!=null&&k.GetValue("AQServer")!=null;}}catch{return false;}}
 public static string CommandFor(string path){return "\""+Path.GetFullPath(path)+"\" --tray";}
 public static void Set(bool value){using(var k=Registry.CurrentUser.CreateSubKey(Key)){if(value)k.SetValue("AQServer",CommandFor(ExecutablePath));else k.DeleteValue("AQServer",false);}}
}
