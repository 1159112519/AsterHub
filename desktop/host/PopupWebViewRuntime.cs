using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

// A WebView browser process fixes its DPI mode when its first controller starts.
// The system-DPI workbench and per-monitor taskbar HWNDs must not share its profile.
// COM environments remain cached on their owning STA, never passed across threads.
internal static class PopupWebViewRuntime {
 [ThreadStatic]static Dictionary<string,Task<CoreWebView2Environment>> environments;
 [DllImport("user32.dll")]static extern int GetAwarenessFromDpiAwarenessContext(IntPtr context);
 [DllImport("user32.dll")]static extern bool AreDpiAwarenessContextsEqual(IntPtr left,IntPtr right);
 internal static Task<CoreWebView2Environment> ForWindowAsync(IntPtr hwnd){
  if(hwnd==IntPtr.Zero)throw new ArgumentException("Popup HWND is required", "hwnd");
  var context=TaskbarLocator.GetWindowDpiAwarenessContext(hwnd);int awareness=GetAwarenessFromDpiAwarenessContext(context);
  string mode=AreDpiAwarenessContextsEqual(context,(IntPtr)(-4))?"per-monitor-v2":awareness==2?"per-monitor":awareness==1?"system":"unaware";
  string folder=Path.GetFullPath(Path.Combine(WorkbenchHost.DataRoot,"WebViewPopups",mode));
  if(environments==null)environments=new Dictionary<string,Task<CoreWebView2Environment>>(StringComparer.OrdinalIgnoreCase);
  Task<CoreWebView2Environment> pending;if(environments.TryGetValue(folder,out pending)&&!pending.IsFaulted&&!pending.IsCanceled)return pending;
  // The workbench owns its optional debugging port; preloaded popup browsers never compete for it.
  pending=CoreWebView2Environment.CreateAsync(null,folder);environments[folder]=pending;return pending;
 }
}

// HWND creation must use the popup's DPI context even on the workbench dispatcher.
internal sealed class PopupDpiScope:IDisposable {
 readonly IntPtr previous=TaskbarLocator.SetThreadDpiAwarenessContext((IntPtr)(-4));
 public void Dispose(){if(previous!=IntPtr.Zero)TaskbarLocator.SetThreadDpiAwarenessContext(previous);}
}
