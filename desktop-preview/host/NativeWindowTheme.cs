using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
public static class NativeWindowTheme {
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 static readonly string[] legacy={"graphite","slate","forest","ocean","light"};
 public sealed class Appearance {public int schemaVersion=1;public string theme,mode;public Dictionary<string,string> colors;}
 static bool Valid(string id){int n;return Array.IndexOf(legacy,id)>=0||id!=null&&id.Length==2&&int.TryParse(id,out n)&&n>=1&&n<=9;}
 public static string Read(string root){try{string id=File.ReadAllText(Path.Combine(root,"window-theme.txt")).Trim();return Valid(id)?id:"graphite";}catch{return "graphite";}}
 static Appearance Legacy(string theme){string bg=theme=="light"?"#f4f5f9":theme=="forest"?"#19241f":theme=="ocean"?"#1a2330":theme=="slate"?"#1b1e26":"#1c201f",fg=theme=="light"?"#29354a":theme=="forest"?"#e8f0e9":theme=="ocean"?"#e6eff7":theme=="slate"?"#e9edf5":"#e5eae5";return new Appearance{theme=theme,mode=theme=="light"?"light":"dark",colors=new Dictionary<string,string>{{"background",bg},{"foreground",fg},{"surface",theme=="light"?"#ffffff":theme=="forest"?"#202e27":theme=="ocean"?"#202d3c":theme=="slate"?"#222631":"#222724"},{"muted",theme=="light"?"#53647b":theme=="graphite"?"#a0aaa2":"#b0b9cb"},{"border",theme=="light"?"#ccd4e3":theme=="graphite"?"#343c36":"#3a4354"}}};}
 static bool ValidColors(Appearance a){if(a==null||a.schemaVersion!=1||!Valid(a.theme)||(a.mode!="light"&&a.mode!="dark")||a.colors==null)return false;foreach(string k in new[]{"background","foreground","surface","muted","border"}){string color;if(!a.colors.TryGetValue(k,out color)||!Regex.IsMatch(color??"","^#[0-9a-fA-F]{6}$"))return false;}return a.colors.Count==5;}
 public static Appearance Current(string theme){if(Array.IndexOf(legacy,theme)>=0)return Legacy(theme);try{string file=Path.Combine(WorkbenchHost.DataRoot,"window-appearance.json");if(new FileInfo(file).Length<8192){var a=new JavaScriptSerializer().Deserialize<Appearance>(File.ReadAllText(file));if(ValidColors(a)&&a.theme==theme)return a;}}catch{}return Legacy("graphite");}
 public static Color ColorValue(string value){return (Color)ColorConverter.ConvertFromString(value);}
 public static void ApplyResolved(Window window,Dictionary<string,object> p){string id=Convert.ToString(p["theme"]);if(!Valid(id))throw new ArgumentException("Unknown appearance");Appearance a;if(Array.IndexOf(legacy,id)>=0)a=Legacy(id);else{object raw;if(!p.TryGetValue("colors",out raw)||!(raw is Dictionary<string,object>))throw new ArgumentException("Missing appearance colors");a=new Appearance{schemaVersion=Convert.ToInt32(p["schemaVersion"]),theme=id,mode=Convert.ToString(p["mode"]),colors=new Dictionary<string,string>()};foreach(var item in (Dictionary<string,object>)raw)a.colors[item.Key]=Convert.ToString(item.Value);if(!ValidColors(a))throw new ArgumentException("Invalid appearance colors");}Save(a);ApplyColors(window,a);}
 static void Save(Appearance a){string path=Path.Combine(WorkbenchHost.DataRoot,"window-appearance.json"),temp=path+".tmp";Directory.CreateDirectory(WorkbenchHost.DataRoot);File.WriteAllText(temp,new JavaScriptSerializer().Serialize(a));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"window-theme.txt"),a.theme);}
 public static void Apply(Window window,string theme,bool save){if(!Valid(theme))throw new ArgumentException("Unknown appearance");var a=Current(theme);if(save)Save(a);ApplyColors(window,a);}
 static void ApplyColors(Window window,Appearance a){Color bg=ColorValue(a.colors["background"]),fg=ColorValue(a.colors["foreground"]);window.Background=new SolidColorBrush(bg);IntPtr handle=new WindowInteropHelper(window).Handle;if(handle==IntPtr.Zero)return;int dark=a.mode=="light"?0:1;if(DwmSetWindowAttribute(handle,20,ref dark,4)!=0)DwmSetWindowAttribute(handle,19,ref dark,4);int caption=ColorRef(bg),text=ColorRef(fg);DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);DwmSetWindowAttribute(handle,34,ref caption,4);}
 static int ColorRef(Color c){return c.R|(c.G<<8)|(c.B<<16);}
}
