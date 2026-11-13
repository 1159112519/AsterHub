using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;

internal static class WorkbenchElevation {
 const int TokenElevationType=18, TokenElevationTypeLimited=3;
 [DllImport("advapi32.dll", SetLastError=true)]
 static extern bool GetTokenInformation(IntPtr token,int informationClass,out int value,int length,out int returned);
 static bool HasLinkedAdminToken(){
  using(var identity=WindowsIdentity.GetCurrent()){
   int value,returned;
   return GetTokenInformation(identity.Token,TokenElevationType,out value,sizeof(int),out returned)&&value==TokenElevationTypeLimited;
  }
 }
 internal static bool TryRelaunchForeground(){
  try{
   // A filtered token can omit the deny-only Administrators SID from WindowsIdentity.Groups.
   // Only a limited split token can elevate without switching to another user's profile.
   if(!HasLinkedAdminToken())return false;
   var info=new ProcessStartInfo{FileName=Assembly.GetExecutingAssembly().Location,UseShellExecute=true,Verb="runas",WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,Arguments="--restart-parent "+Process.GetCurrentProcess().Id};
   using(var process=Process.Start(info))return process!=null;
  }catch(Win32Exception error){if(error.NativeErrorCode!=1223)DiagnosticsLog.Write("startup-elevation",error);return false;}
  catch(Exception error){DiagnosticsLog.Write("startup-elevation",error);return false;}
 }
}
