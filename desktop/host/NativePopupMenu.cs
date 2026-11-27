using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// A native desktop popup; Windows owns painting, keyboard input and outside-click dismissal.
internal sealed class NativePopupMenu:IDisposable {
 internal sealed class Entry {
  internal readonly string Text;
  internal readonly Action Run;
  internal Entry(string text,Action run){Text=text;Run=run;}
 }
 sealed class OwnerWindow:NativeWindow {
  internal OwnerWindow(){CreateHandle(new CreateParams{Caption="AsterHub context menu owner",Style=unchecked((int)0x80000000),ExStyle=0x80});}
  internal void Release(){if(Handle!=IntPtr.Zero)DestroyHandle();}
 }
 const uint MF_STRING=0,MF_SEPARATOR=0x800;
 const uint TPM_RIGHTBUTTON=0x2,TPM_NONOTIFY=0x80,TPM_RETURNCMD=0x100;
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr CreatePopupMenu();
 [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr command,string text);
 [DllImport("user32.dll")]static extern bool DestroyMenu(IntPtr menu);
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
 [DllImport("user32.dll")]static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr window,IntPtr parameters);
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr wparam,IntPtr lparam);
 [DllImport("user32.dll")]static extern bool EndMenu();
 OwnerWindow owner;
 bool disposed,dismissed;
 internal bool Active{get;private set;}
 internal void Open(Entry[] entries,int x,int y){
  if(disposed||Active||entries.Length==0)return;
  IntPtr menu=CreatePopupMenu();if(menu==IntPtr.Zero)throw new Win32Exception();
  uint command=0;
  try{
   for(int i=0;i<entries.Length;i++){
    var entry=entries[i];
    if(!AppendMenu(menu,entry==null?MF_SEPARATOR:MF_STRING,entry==null?UIntPtr.Zero:new UIntPtr((uint)i+1),entry==null?null:entry.Text.Replace("&","&&")))throw new Win32Exception();
   }
   if(owner==null)owner=new OwnerWindow();
   Active=true;dismissed=false;
   // Giving the owner foreground first lets Windows dismiss a tray menu on outside clicks.
   var handle=owner.Handle;SetForegroundWindow(handle);
   if(!dismissed&&!disposed)command=TrackPopupMenuEx(menu,TPM_RIGHTBUTTON|TPM_NONOTIFY|TPM_RETURNCMD,x,y,handle,IntPtr.Zero);
   // A harmless queued message leaves clean foreground state before the next tray open.
   PostMessage(handle,0,IntPtr.Zero,IntPtr.Zero);
  }finally{
   Active=false;DestroyMenu(menu);if(disposed)ReleaseOwner();
  }
  if(!disposed&&!dismissed&&command>0&&command<=entries.Length){var entry=entries[command-1];if(entry!=null&&entry.Run!=null)entry.Run();}
 }
 internal void Dismiss(){if(!Active||dismissed)return;dismissed=true;EndMenu();}
 void ReleaseOwner(){if(owner!=null){owner.Release();owner=null;}}
 public void Dispose(){if(disposed)return;disposed=true;if(Active)Dismiss();else ReleaseOwner();}
}
