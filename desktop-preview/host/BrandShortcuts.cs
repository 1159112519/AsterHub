using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
public static class BrandShortcuts {
 [DllImport("shell32.dll")]static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
 public static void Apply(string iconId){object shell=null;try{
  string current=Path.GetFullPath(Assembly.GetExecutingAssembly().Location),cache=Path.Combine(WorkbenchHost.DataRoot,"Branding");Directory.CreateDirectory(cache);string sourceIcon=BrandIcons.Asset(iconId,"app.ico"),version;using(var sha=System.Security.Cryptography.SHA256.Create())version=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(sourceIcon))).Replace("-","").Substring(0,16);string icon=Path.Combine(cache,"app-"+BrandIcons.Normalize(iconId)+"-"+version+".ico");if(!File.Exists(icon))File.Copy(sourceIcon,icon);
  var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return;shell=Activator.CreateInstance(type);
  string programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs),desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
  var files=new System.Collections.Generic.List<string>();string standard=Path.Combine(programs,"AsterHub Preview","AsterHub Preview.lnk");if(File.Exists(standard))files.Add(standard);string desk=Path.Combine(desktop,"AsterHub Preview.lnk");if(File.Exists(desk))files.Add(desk);
  string pinned=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");if(Directory.Exists(pinned))files.AddRange(Directory.GetFiles(pinned,"AsterHub*.lnk"));
  // Only the application's exact shortcut name and executable target may be updated.
  foreach(string linkPath in files){object link=null;try{link=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{linkPath});var lt=link.GetType();string target=Convert.ToString(lt.InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null));if(string.IsNullOrEmpty(target)||!string.Equals(Path.GetFullPath(target),current,StringComparison.OrdinalIgnoreCase))continue;lt.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{icon+",0"});lt.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);IntPtr item=Marshal.StringToHGlobalUni(linkPath);try{SHChangeNotify(0x2000,0x0005,item,IntPtr.Zero);}finally{Marshal.FreeHGlobal(item);}}catch{}finally{if(link!=null&&Marshal.IsComObject(link))Marshal.FinalReleaseComObject(link);}}
 }catch{}finally{if(shell!=null&&Marshal.IsComObject(shell))Marshal.FinalReleaseComObject(shell);}}
}
