using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
public static class FooterLayoutTests {
 static List<string> log=new List<string>(); static int failures;
 static T Field<T>(object o,string n){return (T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(o);}
 static Rect Bounds(FrameworkElement e,UIElement root){return new Rect(e.TranslatePoint(new Point(),root),e.RenderSize);}
 static void Check(bool ok,string text){log.Add((ok?"PASS ":"FAIL ")+text);if(!ok)failures++;}
 static double TextWidth(TextBlock t){return new FormattedText(t.Text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(t.FontFamily,t.FontStyle,t.FontWeight,t.FontStretch),t.FontSize,Brushes.Black,1).Width;}
 static double InkCenter(TextBlock t,UIElement root){var ft=new FormattedText(t.Text,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(t.FontFamily,t.FontStyle,t.FontWeight,t.FontStretch),t.FontSize,Brushes.Black,1);var ink=ft.BuildGeometry(new Point()).Bounds;return Bounds(t,root).Top+ink.Top+ink.Height/2;}
 static void Save(FrameworkElement e,string file,int dpi){var b=new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth*dpi/96),(int)Math.Ceiling(e.ActualHeight*dpi/96),dpi,dpi,PixelFormats.Pbgra32);var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){dc.DrawRectangle(new VisualBrush(e){AutoLayoutContent=false,Stretch=Stretch.Fill},null,new Rect(0,0,e.ActualWidth,e.ActualHeight));}b.Render(visual);var p=new PngBitmapEncoder();p.Frames.Add(BitmapFrame.Create(b));using(var f=File.Create(file))p.Save(f);}
 public static async void Run(Application app,string folder){Directory.CreateDirectory(folder);MonitorWindow w=null;try{
 w=new MonitorWindow(true){ShowActivated=false,ShowInTaskbar=false,Left=-18000,Top=-18000,WindowStartupLocation=WindowStartupLocation.Manual};w.Show();w.Height=800;
 foreach(string theme in ((folder.Contains("baseline")||folder.Contains("probe"))?new[]{"graphite"}:new[]{"graphite","light","forest","ocean"})){w.ApplyAppearance(theme);
 foreach(int width in (folder.Contains("baseline")?new[]{1920}:folder.Contains("probe")?new[]{760,1440,1920}:new[]{760,1000,1200,1440,1720,1920})){
 w.Width=width;w.Root.Width=width-48;w.Root.Height=752;w.Root.Measure(new Size(width-48,752));w.Root.Arrange(new Rect(0,0,width-48,752));await Task.Delay(70);var m=Program.Fixture("eight");m.Cpu="2.1%";m.Mem="46660 / 947361 MiB";foreach(var g in m.Gpus)g.Util="12.5";w.SetMetrics(m,false);w.SwitchView("内存");await Task.Delay(960);w.UpdateLayout();Save(w.Root,Path.Combine(folder,theme+"-"+width+"-window.png"),96);await Task.Delay(80);w.Root.UpdateLayout();
 var footer=Field<Border>(w,"workspaceFooter");var cells=Field<Grid[]>(w,"footerCells");var cpu=Field<MiniSpark>(w,"footerCpu");var gpu=Field<GpuMini>(w,"footerGpu");var meter=Field<PixelMeter>(w,"footerMemory");var capacity=Field<TextBlock>(w,"memoryDetail");
 string tag=theme+"-"+width;Check(Math.Abs(w.Root.ActualWidth-(width-48))<1,tag+" requested width arranged actual="+w.Root.ActualWidth);var cr=Bounds(cpu,footer);var gr=Bounds(gpu,footer);var mr=Bounds(meter,footer);var cap=Bounds(capacity,footer);
 Check(Math.Abs(cr.Width-gr.Width)<.1,tag+" equal CPU/GPU graph widths");
 double co=cr.X-Bounds(cells[0],footer).X,go=gr.X-Bounds(cells[1],footer).X;
 Check(Math.Abs(co-go)<=.81,tag+" equal label-number-chart columns diff="+Math.Abs(co-go));
 Check(meter.Visibility==Visibility.Visible,tag+" memory meter remains available");Check(Field<TextBlock>(w,"cpuDetail").Visibility==Visibility.Collapsed&&Field<TextBlock>(w,"gpuDetail").Visibility==Visibility.Collapsed&&capacity.Visibility==Visibility.Collapsed,tag+" only name, value and plot visible");

 foreach(var cell in cells){var r=Bounds(cell,footer);var parent=(Grid)cell.Parent;double allocated=parent.ColumnDefinitions[Grid.GetColumn(cell)].ActualWidth-cell.Margin.Left-cell.Margin.Right;Check(Math.Abs(cell.ActualWidth-allocated)<.6,tag+" cell fits allocated lane actual="+cell.ActualWidth+" allocated="+allocated);Check(r.Left>=footer.Padding.Left-.5&&r.Right<=footer.ActualWidth-footer.Padding.Right+.5,tag+" lane inside footer");foreach(var child in cell.Children.OfType<FrameworkElement>().Where(x=>x.Visibility==Visibility.Visible)){var b=Bounds(child,footer);Check(b.Left>=r.Left-.5&&b.Right<=r.Right+.5,tag+" child contained "+child.GetType().Name);var t=child as TextBlock;if(t!=null)Check(TextWidth(t)<=t.ActualWidth+.5,tag+" full text "+t.Text);}}
 foreach(var cell in cells){var label=(TextBlock)cell.Children[0];var value=(TextBlock)cell.Children[1];var plot=cell.Children.OfType<FrameworkElement>().Single(x=>x is MiniSpark||x is GpuMini||x is PixelMeter);var r=Bounds(cell,footer);Check(Math.Abs((Bounds(label,footer).Left+Bounds(plot,footer).Right)/2-(r.Left+r.Right)/2)<.8,tag+" content group horizontally centered");Check(label.TextAlignment==TextAlignment.Center&&value.TextAlignment==TextAlignment.Center,tag+" labels and numbers centered in fixed slots");Check(Math.Abs(InkCenter(label,footer)-InkCenter(value,footer))<1.5,tag+" glyphs optically centered");}
 foreach(int dpi in new[]{96,144})Save(footer,Path.Combine(folder,tag+"-footer-"+dpi+".png"),dpi);
 if(width==1440||width==1920)Save(w.Root,Path.Combine(folder,tag+"-window.png"),96);
 var controls=new FrameworkElement[]{cpu,gpu,meter,capacity};var before=controls.Select(c=>Bounds(c,footer)).ToArray();
 m.Cpu="100.0%";m.Mem="1024000 / 33554432 MiB";foreach(var g in m.Gpus)g.Util="100.0";w.SetMetrics(m,false);
 for(int frame=0;frame<8;frame++){await Task.Delay(120);w.UpdateLayout();for(int i=0;i<controls.Length;i++){if(controls[i].Visibility!=Visibility.Visible)continue;var r=Bounds(controls[i],footer);Check(Math.Abs(r.X-before[i].X)<.1&&Math.Abs(r.Width-before[i].Width)<.1,tag+" animated slot "+i+" frame "+frame);}}
 }
 }
 foreach(string fixture in new[]{"none","two","eight"}){w.SetMetrics(Program.Fixture(fixture),false);await Task.Delay(960);w.UpdateLayout();Check(!w.MemoryValue.Text.Contains("/"),"single memory number "+fixture);Check(TextWidth(w.DiskValue)<=w.DiskValue.ActualWidth+.5,"GPU summary fully visible "+fixture);} 
 w.Exit();w=null;File.WriteAllLines(Path.Combine(folder,"footer-results.txt"),new[]{"FAILURES="+failures,"CHECKS="+log.Count}.Concat(log));app.Shutdown(failures==0?0:1);
 }catch(Exception e){File.WriteAllText(Path.Combine(folder,"footer-error.txt"),e.ToString());File.WriteAllLines(Path.Combine(folder,"footer-results.txt"),log);if(w!=null)w.Exit();app.Shutdown(1);}}
}





