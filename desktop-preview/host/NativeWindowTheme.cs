using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
public static class NativeWindowTheme {
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 public static string Read(string root){try{var value=File.ReadAllText(Path.Combine(root,"window-theme.txt")).Trim();return Valid(value)?value:"graphite";}catch{return "graphite";}}
 static bool Valid(string theme){return theme=="graphite"||theme=="slate"||theme=="forest"||theme=="ocean"||theme=="light";}
 public static void Apply(Window window,string theme,bool save){
  if(!Valid(theme))throw new ArgumentException("未知界面主题");
  int bg=theme=="light"?0xf4f5f9:theme=="forest"?0x19241f:theme=="ocean"?0x1a2330:theme=="slate"?0x1b1e26:0x1c201f;
  int fg=theme=="light"?0x29354a:theme=="forest"?0xe8f0e9:theme=="ocean"?0xe6eff7:theme=="slate"?0xe9edf5:0xe5eae5;
  window.Background=new SolidColorBrush(Color.FromRgb((byte)(bg>>16),(byte)(bg>>8),(byte)bg));
  IntPtr handle=new WindowInteropHelper(window).Handle;
  if(handle!=IntPtr.Zero){int dark=theme=="light"?0:1;if(DwmSetWindowAttribute(handle,20,ref dark,4)!=0)DwmSetWindowAttribute(handle,19,ref dark,4);int caption=ColorRef(bg),text=ColorRef(fg);DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);DwmSetWindowAttribute(handle,34,ref caption,4);}
  if(save)File.WriteAllText(Path.Combine(WorkbenchHost.DataRoot,"window-theme.txt"),theme);
 }
 static int ColorRef(int rgb){return ((rgb>>16)&255)|(rgb&0xff00)|((rgb&255)<<16);}
}
