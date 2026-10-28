using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
public static class ThemeTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,(Action)(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
 static void Image(FrameworkElement element,string path){element.UpdateLayout();int w=Math.Max(1,(int)Math.Ceiling(element.ActualWidth)),h=Math.Max(1,(int)Math.Ceiling(element.ActualHeight));var bitmap=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(path))encoder.Save(f);}
 public static int Run(string folder){Directory.CreateDirectory(folder);var log=new List<string>();var flags=BindingFlags.NonPublic|BindingFlags.Instance;
 foreach(string key in Theme.Keys){var monitor=new MonitorWindow(true){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};monitor.ApplyAppearance(key);monitor.SetMetrics(Program.Fixture("eight"));monitor.SwitchView("GPU");monitor.RenderImage(Path.Combine(folder,key+"-gpu.png"),1200,900,120);Check(monitor.Audit().Count==0,"main layout "+key);
 var settings=new Settings{ThemeName=key};settings.Servers.Add(Storage.DefaultServer());settings.Servers.Add(Storage.LocalServer());var editor=new SettingsWindow(settings,true){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};editor.Show();Pump();editor.RenderImage(Path.Combine(folder,key+"-settings.png"),860,760,120);
 editor.SelectSettingsPage(1);Pump();editor.RenderImage(Path.Combine(folder,key+"-appearance.png"),860,760,120);
 var combo=(ComboBox)typeof(SettingsWindow).GetField("themePicker",flags).GetValue(editor);combo.IsDropDownOpen=true;Pump();var popup=(Popup)combo.Template.FindName("PART_Popup",combo);Check(popup.IsOpen,"theme popup "+key);Image((FrameworkElement)popup.Child,Path.Combine(folder,key+"-dropdown.png"));var popupBorder=(Border)popup.Child;Check(((SolidColorBrush)popupBorder.Background).Color==Theme.Surface.Color,"popup palette "+key);combo.IsDropDownOpen=false;
 var tip=new ToolTip{Content="显卡温度与风扇读数 · GPU 0",PlacementTarget=combo};combo.ToolTip=tip;tip.IsOpen=true;Pump();Check(((SolidColorBrush)tip.Background).Color==Theme.Raised.Color,"tooltip palette "+key);Check(((SolidColorBrush)tip.Foreground).Color==Theme.Ink.Color,"tooltip text "+key);Image(tip,Path.Combine(folder,key+"-tooltip.png"));tip.IsOpen=false;
 editor.SelectSettingsPage(0);Pump();
 var field=(TextBox)typeof(SettingsWindow).GetField("host",flags).GetValue(editor);field.Focus();Pump();Check(field.FontFamily.Source==Theme.Chinese.Source,"input font "+key);Check(((SolidColorBrush)field.CaretBrush).Color==Theme.Green.Color,"caret palette "+key);editor.Close();monitor.Exit();log.Add(key+"_SETTINGS_DROPDOWN_TOOLTIP_FIELD_FONT_AND_MAIN_LAYOUT=True");}
 Theme.Apply("forest");File.WriteAllLines(Path.Combine(folder,"theme-results.txt"),log);return 0;
 }
}
