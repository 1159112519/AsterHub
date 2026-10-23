using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32.SafeHandles;

public static class ProcessDialogs {
 internal static Window CreateConfirmationWindow(Window owner,ProcessTarget target){if(target==null||target.Disposed)throw new InvalidOperationException("进程目标无效");if(!target.CanTerminate)throw new InvalidOperationException(target.Reason??"目标不允许结束");
  var win=new Window{Title="确认结束 GPU 进程",Width=580,SizeToContent=SizeToContent.Height,MinHeight=370,MaxHeight=720,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,Background=Theme.Bg,Foreground=Theme.Ink,FontFamily=Theme.Chinese,ShowInTaskbar=false};if(owner!=null)win.Owner=owner;win.Resources.MergedDictionaries.Add(Theme.Resources());
  var panel=new StackPanel{Margin=new Thickness(24)};Action<string,System.Windows.Media.Brush> line=(text,color)=>{var t=Theme.Text(text,13,color);t.TextWrapping=TextWrapping.Wrap;t.TextTrimming=TextTrimming.None;t.Margin=new Thickness(0,5,0,5);panel.Children.Add(t);};
  line("结束进程可能导致训练中断、未保存数据丢失和其他任务失败。",Theme.Red);
  line("设备："+target.Device+" · GPU "+target.Gpu,Theme.Ink);line("PID："+target.Pid,Theme.Ink);line("实际进程："+target.Name,Theme.Ink);line("进程用户："+target.User+"；执行用户："+target.SessionUser,Theme.Ink);
  line(target.Endpoint.IsLocal?"Windows 将强制结束所核验的进程。":"Linux 只发送 SIGTERM；不会 sudo，不会自动 SIGKILL。",Theme.Amber);
  line("身份已核验，执行前将再次核对；可取消后刷新进程列表。",Theme.Muted);
  var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};var cancel=Theme.Button("取消",()=>{win.DialogResult=false;});cancel.IsDefault=true;cancel.IsCancel=true;cancel.Margin=new Thickness(0,0,12,0);var yes=Theme.Button("确认结束此进程",()=>{win.DialogResult=true;});yes.Foreground=Theme.Red;buttons.Children.Add(cancel);buttons.Children.Add(yes);panel.Children.Add(buttons);win.Content=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};win.Loaded+=(s,e)=>cancel.Focus();return win;
 }
 public static bool Confirm(Window owner,ProcessTarget target){var win=CreateConfirmationWindow(owner,target);bool ok=win.ShowDialog()==true;target.Confirmed=ok;return ok;}
 public static async Task<string> ConfirmAndTerminateAsync(Window owner,Server device,GpuProcess process){using(var target=await ProcessActions.InspectAsync(device,process)){if(!Confirm(owner,target))return "已取消，未结束进程";return await ProcessActions.TerminateAsync(target);}}

}
