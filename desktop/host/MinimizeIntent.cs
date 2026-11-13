using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

// Observe a caption/system minimize without replacing Explorer or Windows commands.
// The existing minimize-to-tray preference is applied by the owner StateChanged handler.
internal sealed class MinimizeIntent {
 long captionAt=-1;bool command;
 internal void Caption(){captionAt=Stopwatch.GetTimestamp();}
 internal void Explicit(){command=true;}
 internal void Reset(){captionAt=-1;command=false;}
 internal bool Consume(){bool result=command||captionAt>=0&&(Stopwatch.GetTimestamp()-captionAt)/(double)Stopwatch.Frequency<1.5;Reset();return result;}
}
public sealed partial class WorkbenchWindow {
 readonly MinimizeIntent minimizeIntent=new MinimizeIntent();
 [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
 [StructLayout(LayoutKind.Sequential)]struct IconicWindowPosition{public IntPtr window,after;public int x,y,width,height;public uint flags;}
 internal void MinimizeLikeCaption(){if(IsExiting||WindowState==WindowState.Minimized)return;DiagnosticsLog.Write("window-minimize-caption-action",null);minimizeIntent.Explicit();WindowState=WindowState.Minimized;}
 void AttachMinimizeIntent(){var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);if(source!=null)source.AddHook(MinimizeIntentMessage);}
 IntPtr MinimizeIntentMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled){
  // Redirect only the minimized frame while Windows is positioning it. Changing
  // WINDOWPOS here preserves the normal restore bounds, including maximized state.
  if(message==0x0046&&IsIconic(window)&&lParam!=IntPtr.Zero){
   var position=(IconicWindowPosition)Marshal.PtrToStructure(lParam,typeof(IconicWindowPosition));
   position.x=-32000;position.y=-32000;position.flags&=~2u;
   Marshal.StructureToPtr(position,lParam,false);
  }
  // HTMINBUTTON is reported before SC_MINIMIZE for the standard window caption.
  if(message==0x00A1&&wParam.ToInt64()==8)minimizeIntent.Caption();
  else if(message==0x00A1)minimizeIntent.Reset();
  if(WindowClickCommands.IsMinimizeSystemCommand(message,wParam.ToInt64()))return IntPtr.Zero;
  if(message==0x0112){minimizeIntent.Reset();if((wParam.ToInt64()&0xFFF0)==0xF120)DiagnosticsLog.Write("window-system-restore",null);}
  return IntPtr.Zero;
 }
}
