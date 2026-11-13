using System;
using System.Runtime.InteropServices;
using System.Windows;

// Keep WPF and WebView2 initialized while DWM keeps the popup out of the desktop.
internal static class PopupCloak {
 const int Cloak=13,Cloaked=14;
 [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int bytes);
 [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int bytes);
 internal static bool Set(IntPtr hwnd,bool hidden){int value=hidden?1:0;return hwnd!=IntPtr.Zero&&DwmSetWindowAttribute(hwnd,Cloak,ref value,sizeof(int))==0;}
 internal static bool Hidden(Window window,IntPtr hwnd){int value;return !window.IsVisible||(hwnd!=IntPtr.Zero&&DwmGetWindowAttribute(hwnd,Cloaked,out value,sizeof(int))==0&&value!=0);}
}
