using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Serialization;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

public class GpuCard : Border {
 public TextBlock Id,Model,Percent;public PixelMeter Meter,MemoryMeter;public Grid MemoryRow,TemperatureRow,PowerRow,MaxPowerRow;Border dot;public string DeviceIndex;
 public GpuCard(Gpu g){CornerRadius=new CornerRadius(8);Background=Theme.Surface;BorderBrush=Theme.Line;BorderThickness=new Thickness(1);Padding=new Thickness(18);Margin=new Thickness(0,0,16,16);SnapsToDevicePixels=true;var content=new StackPanel();Child=content;
 var heading=new Grid();heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Id=Theme.Text("",18,Theme.Ink,true,true);heading.Children.Add(Id);dot=new Border{Width=7,Height=7,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0)};Grid.SetColumn(dot,1);heading.Children.Add(dot);content.Children.Add(heading);Model=Theme.Text("",13,Theme.Muted);Model.Margin=new Thickness(0,8,0,0);content.Children.Add(Model);
 var metric=new Grid{Margin=new Thickness(0,16,0,0)};metric.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});metric.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});Percent=Theme.Text("",40,Theme.Green,true,true);metric.Children.Add(Percent);var caption=Theme.Text("利用率",12,Theme.Muted);caption.VerticalAlignment=VerticalAlignment.Bottom;caption.Margin=new Thickness(12,0,0,8);Grid.SetColumn(caption,1);metric.Children.Add(caption);content.Children.Add(metric);Meter=new PixelMeter(Number(g.Util),Theme.Green,7){Margin=new Thickness(0,12,0,18)};content.Children.Add(Meter);
 MemoryRow=Theme.Pair("显存","");content.Children.Add(MemoryRow);MemoryMeter=new PixelMeter(Number(g.Total)>0?100*Number(g.Used)/Number(g.Total):0,Theme.Teal,4){Blocks=32,Margin=new Thickness(0,8,0,14)};content.Children.Add(MemoryMeter);TemperatureRow=Theme.Pair("温度 / 风扇","");TemperatureRow.Margin=new Thickness(0,0,0,10);content.Children.Add(TemperatureRow);PowerRow=Theme.Pair("功耗 / 限制","");content.Children.Add(PowerRow);MaxPowerRow=Theme.Pair("最大功耗上限","");MaxPowerRow.Margin=new Thickness(0,8,0,0);MaxPowerRow.Visibility=Visibility.Collapsed;Update(g);
 }
 static void SetPair(Grid row,string value){NumericMotion.Text((TextBlock)row.Children[1],value);}
 public void Update(Gpu g){DeviceIndex=g.Index;double value=Number(g.Util),temp=Number(g.Temp);Brush accent=temp>=75?Theme.Red:value>=80?Theme.Amber:Theme.Green;Id.Text="GPU "+(g.Index??"0");Model.Text=g.Name;Model.ToolTip=g.Name;NumericMotion.Text(Percent,GpuTelemetry.Unit(g.Util,"%"));Percent.Foreground=dot.Background=Meter.Fill=accent;Meter.Value=value;Meter.InvalidateVisual();MemoryMeter.Value=Number(g.Total)>0?100*Number(g.Used)/Number(g.Total):0;SetPair(MemoryRow,g.Used+" / "+g.Total+" MiB");SetPair(TemperatureRow,GpuTelemetry.Unit(g.Temp,"°C")+" / "+GpuTelemetry.Unit(g.Fan,"%"));TemperatureRow.ToolTip=GpuTelemetry.Available(g.Fan)?"温度与风扇转速百分比由驱动提供":"驱动未提供 GPU 风扇读数；笔记本整机风扇可能由厂商控制器管理。未提供不代表风扇停转。";SetPair(PowerRow,GpuTelemetry.Power(g));PowerRow.ToolTip=g.CapSource??"驱动提供的当前功耗限制；不是历史峰值。";SetPair(MaxPowerRow,GpuTelemetry.Unit(g.PowerMax," W"));MaxPowerRow.ToolTip="驱动报告的可配置最大功耗上限；不是实时功耗，也不是实际采样峰值。";}
 public static double Number(string s){double n;return double.TryParse(s,NumberStyles.Float,CultureInfo.InvariantCulture,out n)?n:0;}
}
