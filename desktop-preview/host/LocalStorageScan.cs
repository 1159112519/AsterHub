// Independently implemented from the public Win32 directory-enumeration API.
// No third-party implementation or GPL source is included; see docs/storage-native-enum/scanner-origin.md.
using System;using System.IO;using System.Collections.Generic;using System.ComponentModel;
using System.Runtime.InteropServices;using System.Threading;
public static class LocalStorageScan {
 const int NoMoreFiles=18,FileNotFound=2,InvalidParameter=87,NotSupported=50;
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 struct FindData {
  public uint Attributes,CreationLow,CreationHigh,AccessLow,AccessHigh,WriteLow,WriteHigh,SizeHigh,SizeLow,Reserved0,Reserved1;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=14)]public string Alternate;
 }
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr FindFirstFileExW(string path,int info,out FindData data,int search,IntPtr filter,int flags);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool FindNextFileW(IntPtr handle,out FindData data);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool FindClose(IntPtr handle);
 static readonly IntPtr Invalid=new IntPtr(-1);
 static string NativePath(string path){if(path.StartsWith(@"\\?\",StringComparison.Ordinal))return path;if(path.StartsWith(@"\\",StringComparison.Ordinal))return @"\\?\UNC\"+path.Substring(2);return @"\\?\"+path;}
 static Exception Error(int code,string path){if(code==5)return new UnauthorizedAccessException("无法读取目录："+path);if(code==3||code==267)return new DirectoryNotFoundException("目录不存在："+path);return new Win32Exception(code,"无法扫描目录："+path+" ("+code+")");}
 public static IEnumerable<FileEntry> Enumerate(string path,CancellationToken token){
  token.ThrowIfCancellationRequested();FindData data;string pattern=NativePath(path.TrimEnd('\\'))+@"\*";
  IntPtr handle=FindFirstFileExW(pattern,1,out data,0,IntPtr.Zero,2);int error=Marshal.GetLastWin32Error();
  // Older providers can reject large fetch or basic metadata; retain ordinary enumeration.
  if(handle==Invalid&&(error==InvalidParameter||error==NotSupported)){handle=FindFirstFileExW(pattern,0,out data,0,IntPtr.Zero,0);error=Marshal.GetLastWin32Error();}
  if(handle==Invalid){if(error==FileNotFound)yield break;throw Error(error,path);}
  try{while(true){
   token.ThrowIfCancellationRequested();if(data.Name!="."&&data.Name!=".."){
    bool dir=(data.Attributes&(uint)FileAttributes.Directory)!=0;
    DateTime modified;try{modified=DateTime.FromFileTimeUtc(((long)data.WriteHigh<<32)|data.WriteLow);}catch(ArgumentOutOfRangeException){modified=DateTime.MinValue;}
    yield return new FileEntry{Name=data.Name,FullPath=Path.Combine(path,data.Name),IsDirectory=dir,IsLink=(data.Attributes&(uint)FileAttributes.ReparsePoint)!=0,Size=dir?0:checked((long)(((ulong)data.SizeHigh<<32)|data.SizeLow)),Modified=modified,Permissions=(data.Attributes&(uint)FileAttributes.ReadOnly)!=0?"只读":"读写"};
   }
   if(!FindNextFileW(handle,out data)){error=Marshal.GetLastWin32Error();if(error!=NoMoreFiles)throw Error(error,path);break;}
  }}finally{FindClose(handle);}
 }
}
