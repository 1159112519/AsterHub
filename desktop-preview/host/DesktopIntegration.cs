using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
public sealed partial class WorkbenchWindow {
 async Task<object> DesktopRequest(string method,Dictionary<string,object> p) {
  if(method=="app.openExternal") {
   Uri uri;string url=FileText(p,"url");
   if(url.Length>4096||!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="github.com"||!(uri.AbsolutePath=="/1159112519/AsterHub"||uri.AbsolutePath.StartsWith("/1159112519/AsterHub/",StringComparison.Ordinal))||!string.IsNullOrEmpty(uri.UserInfo))throw new ArgumentException("不支持的项目链接");
   Process.Start(new ProcessStartInfo(uri.AbsoluteUri){UseShellExecute=true});return true;
  }
  for(int attempt=0;attempt<5;attempt++) {
   try {
    if(method=="clipboard.read"){string value=Clipboard.ContainsText()?Clipboard.GetText(TextDataFormat.UnicodeText):"";if(value.Length>65536)throw new ArgumentException("单次粘贴请不超过 64 KiB");return value;}
    string text=FileText(p,"text");if(text.Length>65536)throw new ArgumentException("单次复制请不超过 64 KiB");
    Clipboard.SetDataObject(text,true);return true;
   }catch(System.Runtime.InteropServices.COMException){if(attempt==4)throw new InvalidOperationException("剪贴板正被占用，请重试");}
   await Task.Delay(35);
  }
  return false;
 }
}
