using System;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

// Do not suspend the workbench: retained terminals and bridge clients still run.
// Low is only a cache-pressure hint; it is not a guaranteed memory cap.
internal static class WebViewMemoryPolicy {
 static volatile bool unavailable;
 internal static void Set(CoreWebView2 core,bool active){
  if(core==null||unavailable)return;
  try{core.MemoryUsageTargetLevel=active?CoreWebView2MemoryUsageTargetLevel.Normal:CoreWebView2MemoryUsageTargetLevel.Low;}
  catch(NotImplementedException){unavailable=true;}
  catch(COMException error){if(error.ErrorCode!=unchecked((int)0x80004002)&&error.ErrorCode!=unchecked((int)0x80004001))throw;unavailable=true;}
 }
}
