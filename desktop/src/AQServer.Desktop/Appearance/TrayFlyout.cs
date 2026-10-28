using System;using System.IO;using System.Linq;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Media;using System.Windows.Media.Effects;
public sealed class TrayFlyout {
 Window host; public bool IsOpen { get { return host != null && host.IsVisible; } } internal Window Host { get { return host; } }readonly Action restore,mini,preferences,about,exit;readonly Action<bool> startup;readonly Func<bool> isStartup;readonly string device;
 public TrayFlyout(string deviceName,Action open,Action compact,Action settings,Action update,Action quit,Func<bool> getStartup,Action<bool> setStartup){device=deviceName;restore=open;mini=compact;preferences=settings;about=update;exit=quit;isStartup=getStartup;startup=setStartup;}
 public Grid Build(){var root=new Grid{Width=304,Margin=new Thickness(14),Resources=Theme.Resources(),Background=Brushes.Transparent,UseLayoutRounding=true,SnapsToDevicePixels=true};KeyboardNavigation.SetTabNavigation(root,KeyboardNavigationMode.Cycle);KeyboardNavigation.SetDirectionalNavigation(root,KeyboardNavigationMode.Cycle);var border=new Border{CornerRadius=new CornerRadius(12),Background=Theme.Surface.CloneCurrentValue(),BorderBrush=Theme.Line.CloneCurrentValue(),BorderThickness=new Thickness(1),Padding=new Thickness(6),Effect=new DropShadowEffect{BlurRadius=20,ShadowDepth=4,Opacity=.24,Color=Colors.Black}};root.Children.Add(border);var stack=new StackPanel();border.Child=stack;
 var brand=new Grid{Margin=new Thickness(13,12,13,13)};brand.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(34)});brand.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});brand.Children.Add(new IconView("Server",22,Theme.Teal.CloneCurrentValue()));var text=new StackPanel{VerticalAlignment=VerticalAlignment.Center};text.Children.Add(Theme.Text("AQ Server",15,Theme.Ink,false,true));var detail=Theme.Text(string.IsNullOrWhiteSpace(device)?"设备监控":device,12,Theme.Muted);detail.Margin=new Thickness(0,5,0,0);text.Children.Add(detail);Grid.SetColumn(text,1);brand.Children.Add(text);stack.Children.Add(brand);
 Add(stack,"LayoutDashboard","打开主窗口",restore,"双击图标");Add(stack,"PictureInPicture2","迷你监控",mini,null);Divide(stack);Add(stack,"Settings2","偏好设置",preferences,null);bool enabled=isStartup();Add(stack,"Power","开机启动",()=>startup(!enabled),enabled?"已开启":"已关闭");Add(stack,"Info","关于与更新",about,null);Divide(stack);Add(stack,"LogOut","退出 AQ Server",exit,null);
 var version=Theme.Text("v"+AppUpdates.DisplayVersion,11,Theme.Muted);version.FontSize=11;version.Margin=new Thickness(15,11,0,7);stack.Children.Add(version);root.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};return root;}
 void Divide(StackPanel stack){stack.Children.Add(new Border{Height=1,Margin=new Thickness(12,6,12,6),Background=Theme.Line.CloneCurrentValue(),Opacity=.7});}
 void Add(StackPanel stack,string icon,string label,Action action,string hint){var button=Theme.Action("",()=>{Close();if(action!=null)action();});button.Height=button.MinHeight=40;button.Padding=new Thickness(12,0,12,0);button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.VerticalContentAlignment=VerticalAlignment.Center;button.ToolTip=null;var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});grid.Children.Add(new IconView(icon,17,Theme.Muted.CloneCurrentValue()));var title=Theme.Text(label,14,Theme.Ink);Grid.SetColumn(title,1);grid.Children.Add(title);if(hint!=null){var auxiliary=Theme.Text(hint,11,Theme.Muted);auxiliary.FontSize=11;auxiliary.Margin=new Thickness(12,0,0,0);Grid.SetColumn(auxiliary,2);grid.Children.Add(auxiliary);}button.Content=grid;System.Windows.Automation.AutomationProperties.SetName(button,label);stack.Children.Add(button);}
 public void Show()
 {
     Close();
     var root = Build();
     var current = new Window {
         WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent,
         ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = true, Topmost = true,
         SizeToContent = SizeToContent.WidthAndHeight, Content = root, Left = -18000, Top = -18000,
         Opacity = 0, UseLayoutRounding = true, SnapsToDevicePixels = true
     };
     host = current;
     // A tray callback has no active WPF owner. An activating tool window gives
     // Windows a real focus boundary, so outside clicks and Alt+Tab dismiss it.
     current.Deactivated += (s, e) => { if (current.IsVisible) current.Close(); };
     current.Closed += (s, e) => { if (object.ReferenceEquals(host, current)) host = null; };
     current.Show(); current.UpdateLayout();
     if (!object.ReferenceEquals(host, current)) return;
     var source = PresentationSource.FromVisual(current);
     var fromDevice = source == null ? Matrix.Identity : source.CompositionTarget.TransformFromDevice;
     var toDevice = source == null ? Matrix.Identity : source.CompositionTarget.TransformToDevice;
     var cursor = System.Windows.Forms.Cursor.Position;
     var work = System.Windows.Forms.Screen.FromPoint(cursor).WorkingArea;
     double w = current.ActualWidth * toDevice.M11, h = current.ActualHeight * toDevice.M22;
     double x = Math.Max(work.Left + 4, Math.Min(cursor.X - 12, work.Right - w - 4));
     double y = cursor.Y + h <= work.Bottom ? cursor.Y : cursor.Y - h;
     y = Math.Max(work.Top + 4, Math.Min(y, work.Bottom - h - 4));
     var point = fromDevice.Transform(new Point(x, y)); current.Left = point.X; current.Top = point.Y;
     current.Opacity = 1; current.Activate();
     var first = VisualButtons(root).FirstOrDefault(); if (first != null) first.Focus();
 }

 static System.Collections.Generic.IEnumerable<Button> VisualButtons(DependencyObject root){for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);var button=child as Button;if(button!=null)yield return button;foreach(var nested in VisualButtons(child))yield return nested;}}
 public void Close(){var current=host;host=null;if(current!=null)current.Close();}
 public void Render(string path){var outer=new Grid();var root=Build();root.HorizontalAlignment=HorizontalAlignment.Left;root.VerticalAlignment=VerticalAlignment.Top;outer.Children.Add(root);Snapshot.Save(outer,path,380,470,144,null);}
}
public sealed partial class MonitorWindow {
 TrayFlyout trayFlyout;
 void ShowTrayFlyout(){if(trayFlyout!=null)trayFlyout.Close();trayFlyout=new TrayFlyout(active==null?"设备监控":active.Name,Restore,ShowCompact,()=>{Restore();OpenSettings();},()=>{Restore();OpenAbout();},Exit,()=>Startup.Enabled(),value=>{try{Startup.Set(value);}catch(Exception e){MessageBox.Show(this,"无法修改开机启动："+e.Message,"AQ Server");}});trayFlyout.Show();}
}
