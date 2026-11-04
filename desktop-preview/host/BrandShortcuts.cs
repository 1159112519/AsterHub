using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

// Shell metadata is scoped to this exact executable, never another installation.
public static class BrandShortcuts {
 internal static string LastShortcutError;
 static readonly Guid ModelFormat=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
 [DllImport("shell32.dll")]static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)]string appId);
 [DllImport("shell32.dll")]static extern int SHGetPropertyStoreForWindow(IntPtr hwnd,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out BrandPropertyStore store);
 [DllImport("shell32.dll")]static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
 internal static string Executable{get{return Path.GetFullPath(Assembly.GetExecutingAssembly().Location);}}
 public static string AppIdFor(string executable){using(var sha=System.Security.Cryptography.SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(executable).ToLowerInvariant()))).Replace("-","").Substring(0,24);return "AsterHub.Preview."+hash;}}
 public static void SetProcessIdentity(){try{Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(AppIdFor(Executable)));}catch{}}
 internal static string CachedIcon(string dataRoot,string iconId){string folder=Path.Combine(dataRoot,"Branding"),source=BrandIcons.Asset(iconId,"window.ico"),version;if(!File.Exists(source))source=BrandIcons.Asset(iconId,"app.ico");Directory.CreateDirectory(folder);using(var sha=System.Security.Cryptography.SHA256.Create())version=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source))).Replace("-","").Substring(0,16);string cached=Path.Combine(folder,"app-"+BrandIcons.Normalize(iconId)+"-"+version+".ico");if(!File.Exists(cached))File.Copy(source,cached);return cached;}
 static BrandPropertyKey Key(uint property){return new BrandPropertyKey{format=ModelFormat,id=property};}
 internal static void WriteString(BrandPropertyStore store,uint property,string text){var key=Key(property);var value=BrandVariant.Text(text);try{Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref value));}finally{value.Dispose();}}
 internal static string ReadString(BrandPropertyStore store,uint property){var key=Key(property);BrandVariant value;Marshal.ThrowExceptionForHR(store.GetValue(ref key,out value));using(value)return value.Kind==31?Marshal.PtrToStringUni(value.Pointer):null;}
 static void WriteRelaunch(BrandPropertyStore store,string executable,string icon,string appId){
  // Windows requires the relaunch fields before assigning the window AppUserModelID.
  WriteString(store,2,"\""+executable+"\"");WriteString(store,3,icon+",0");WriteString(store,4,"AsterHub Preview");WriteString(store,5,appId);
 }
 internal static BrandPropertyStore WindowStore(IntPtr hwnd){Guid iid=typeof(BrandPropertyStore).GUID;BrandPropertyStore store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(hwnd,ref iid,out store));return store;}
 public static void ApplyWindow(IntPtr hwnd,string iconId){if(hwnd==IntPtr.Zero)return;BrandPropertyStore store=null;try{store=WindowStore(hwnd);WriteRelaunch(store,Executable,CachedIcon(WorkbenchHost.DataRoot,iconId),AppIdFor(Executable));}catch{}finally{Release(store);}}
 public static void ClearWindow(IntPtr hwnd){if(hwnd==IntPtr.Zero)return;BrandPropertyStore store=null;try{store=WindowStore(hwnd);foreach(uint id in new uint[]{2,3,4,5}){var key=Key(id);var empty=new BrandVariant();Marshal.ThrowExceptionForHR(store.SetValue(ref key,ref empty));}}catch{}finally{Release(store);}}
 public static void Apply(string iconId,bool notifyExecutable=false){SetProcessIdentity();try{
  string programs=Environment.GetFolderPath(Environment.SpecialFolder.Programs),desktop=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);var files=new List<string>();string standard=Path.Combine(programs,"AsterHub Preview","AsterHub Preview.lnk"),desk=Path.Combine(desktop,"AsterHub Preview.lnk");if(File.Exists(standard))files.Add(standard);if(File.Exists(desk))files.Add(desk);
  string pinned=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),@"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");if(Directory.Exists(pinned))files.AddRange(Directory.GetFiles(pinned,"*.lnk"));ApplyToCandidates(Executable,WorkbenchHost.DataRoot,iconId,files);if(notifyExecutable)NotifyChanged(Executable);
 }catch{}}
 // Explicit candidate injection supports isolated fixture testing without enumerating user shortcuts.
 public static int ApplyToCandidates(string executable,string dataRoot,string iconId,IEnumerable<string> candidates){string icon=CachedIcon(dataRoot,iconId),appId=AppIdFor(executable);int changed=0;foreach(string link in candidates)if(UpdateShortcut(link,executable,icon,appId))changed++;return changed;}
 internal static bool UpdateShortcut(string linkPath,string executable,string icon,string appId){object shell=null;try{
  if(!File.Exists(linkPath))return false;shell=new BrandShellLink();var link=(BrandShellLinkW)shell;((IPersistFile)shell).Load(linkPath,2);var target=new StringBuilder(32768);link.GetPath(target,target.Capacity,IntPtr.Zero,0);if(string.IsNullOrWhiteSpace(target.ToString())||!string.Equals(Path.GetFullPath(target.ToString()),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase))return false;
  link.SetIconLocation(icon,0);WriteRelaunch((BrandPropertyStore)shell,Path.GetFullPath(executable),icon,appId);Marshal.ThrowExceptionForHR(((BrandPropertyStore)shell).Commit());((IPersistFile)shell).Save(linkPath,true);
  NotifyChanged(linkPath);return true;
 }catch(Exception error){LastShortcutError=error.ToString();return false;}finally{Release(shell);}}
 static void NotifyChanged(string path){IntPtr item=Marshal.StringToHGlobalUni(path);try{SHChangeNotify(0x2000,0x0005,item,IntPtr.Zero);}finally{Marshal.FreeHGlobal(item);}}
 static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.FinalReleaseComObject(value);}
}
[StructLayout(LayoutKind.Sequential)]internal struct BrandPropertyKey{public Guid format;public uint id;}
[StructLayout(LayoutKind.Explicit,Size=24)]internal struct BrandVariant:IDisposable{
 [FieldOffset(0)]public ushort Kind;[FieldOffset(8)]public IntPtr Pointer;
 [DllImport("ole32.dll")]static extern int PropVariantClear(ref BrandVariant value);
 public static BrandVariant Text(string value){return new BrandVariant{Kind=31,Pointer=Marshal.StringToCoTaskMemUni(value)};}
 public void Dispose(){PropVariantClear(ref this);}
}
[ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface BrandPropertyStore{
 [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out BrandPropertyKey key);[PreserveSig]int GetValue(ref BrandPropertyKey key,out BrandVariant value);[PreserveSig]int SetValue(ref BrandPropertyKey key,ref BrandVariant value);[PreserveSig]int Commit();
}
[ComImport,Guid("00021401-0000-0000-C000-000000000046")]internal class BrandShellLink{}
[ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]internal interface BrandShellLinkW{
 void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder file,int size,IntPtr find,uint flags);void GetIDList(out IntPtr item);void SetIDList(IntPtr item);void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string value);void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string value);void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string value);void GetHotkey(out short value);void SetHotkey(short value);void GetShowCmd(out int value);void SetShowCmd(int value);void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int size,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string value,int index);void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string value,uint reserved);void Resolve(IntPtr hwnd,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string value);
}
