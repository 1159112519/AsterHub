using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Controls.Primitives;using System.Windows.Input;using System.Windows.Media;
public static class TrayTests {
 static List<string> log=new List<string>();static void Check(bool ok,string text){if(!ok)throw new Exception(text);log.Add("PASS "+text);}
 static IEnumerable<DependencyObject> Walk(DependencyObject root){yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var nested in Walk(VisualTreeHelper.GetChild(root,i)))yield return nested;}
 public static async void Run(Application app,string folder){Directory.CreateDirectory(folder);TrayFlyout tray=null;try{string[] expected={"打开主窗口","迷你监控","偏好设置","开机启动","关于与更新","退出 AQ Server"};bool startup=false;int[] calls=new int[5];tray=new TrayFlyout("compute-node",()=>calls[0]++,()=>calls[1]++,()=>calls[2]++,()=>calls[3]++,()=>calls[4]++,()=>startup,v=>startup=v);
 foreach(var theme in Theme.Keys){Theme.Apply(theme);var root=tray.Build();root.Measure(new Size(380,470));root.Arrange(new Rect(0,0,380,470));root.UpdateLayout();var buttons=Walk(root).OfType<Button>().ToArray();Check(buttons.Select(b=>System.Windows.Automation.AutomationProperties.GetName(b)).SequenceEqual(expected),"only six user tasks, no refresh/pause/terminal: "+theme);Check(buttons.All(b=>b.Height==40&&b.VerticalContentAlignment==VerticalAlignment.Center),"consistent action size: "+theme);Check(!Theme.Raised.IsFrozen,"shared theme brush stays mutable: "+theme);tray.Render(Path.Combine(folder,"tray-"+theme+".png"));}
 var items=Walk(tray.Build()).OfType<Button>().ToArray();for(int i=0;i<items.Length;i++)items[i].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(calls.All(n=>n==1)&&startup,"each task invokes correct action and startup toggles");var nextRoot=tray.Build();nextRoot.Measure(new Size(380,470));nextRoot.Arrange(new Rect(0,0,380,470));nextRoot.UpdateLayout();var next=Walk(nextRoot).OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="开机启动");Check(Walk(next).OfType<TextBlock>().Any(t=>t.Text=="已开启"),"startup state rebuilt from saved value");next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Check(!startup,"startup can toggle off");
 tray.Show(); await Task.Delay(160);
 Check(tray.IsOpen, "tray window opens from a background context");
 var child=(Grid)tray.Host.Content; var source=PresentationSource.FromVisual(child);
 Check(source!=null,"tray has native presentation source");
 Check(tray.Host.IsActive,"tray has real keyboard focus before dismissal");
 child.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});
 Check(!tray.IsOpen,"Escape dismisses tray window");
 tray.Show(); await Task.Delay(120);
 Check(tray.IsOpen && tray.Host.IsActive,"tray can reopen and activate");
 var outside=new Window{Width=160,Height=100,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual,ShowInTaskbar=false,Title="AQ Server isolated dismissal test"};
 outside.Show(); outside.Activate(); await Task.Delay(160);
 Check(outside.IsActive,"native focus actually moved to another window");
 Check(!tray.IsOpen,"focus outside tray dismisses the menu");
 outside.Close(); tray.Show(); await Task.Delay(100);
 Check(tray.IsOpen,"tray reopens after outside dismissal");
 var action=Walk(tray.Host.Content as DependencyObject).OfType<Button>().First(); action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
 Check(!tray.IsOpen && calls[0]==2,"selecting an action dismisses before executing");
 Theme.Apply("forest");File.WriteAllLines(Path.Combine(folder,"tray-results.txt"),log);app.Shutdown(0);
 }catch(Exception ex){if(tray!=null)tray.Close();File.WriteAllLines(Path.Combine(folder,"tray-results.txt"),log);File.WriteAllText(Path.Combine(folder,"tray-error.txt"),ex.ToString());app.Shutdown(1);}}
}
