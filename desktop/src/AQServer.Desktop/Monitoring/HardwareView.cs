using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
public static class HardwareView {
 static string Count(int? n){return n.HasValue?n.Value.ToString(CultureInfo.InvariantCulture):"未提供";}
 static void Line(StackPanel p,string label,string value){if(string.IsNullOrWhiteSpace(value))return;var row=new Grid{Margin=new Thickness(0,5,0,5)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(190)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});var l=Theme.Text(label,13,Theme.Muted);var v=Theme.Text(value,14,Theme.Ink);v.TextWrapping=TextWrapping.Wrap;v.TextTrimming=TextTrimming.None;Grid.SetColumn(v,1);row.Children.Add(l);row.Children.Add(v);p.Children.Add(row);}
 public static UIElement BuildView(HardwareInfo info,bool cpu){return Build(info,cpu);}
 public static Border Build(HardwareInfo info,bool cpu){var p=new StackPanel();var h=info??new HardwareInfo{Note="等待采集硬件规格"};
  var heading=Theme.Text(cpu?"CPU 硬件规格":"内存硬件规格",16,Theme.Ink,false,true);heading.Margin=new Thickness(0,0,0,12);heading.ToolTip=h.Source+"\n"+h.Note+(h.CollectedUtc==default(DateTime)?"":"\n读取时间 "+h.CollectedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));p.Children.Add(heading);
  if(cpu){Line(p,"型号",h.CpuModel);Line(p,"物理插槽 / 物理核心",Count(h.PhysicalSockets)+" / "+Count(h.PhysicalCores));Line(p,"系统线程 / 当前可用线程",Count(h.SystemThreads)+" / "+Count(h.AvailableThreads));}
  else{Line(p,"总容量",h.MemoryBytes.HasValue?(h.MemoryBytes.Value/1073741824.0).ToString("0.##")+" GiB":"未提供");Line(p,"已读取内存条",h.MemoryModules.Count==0?"规格未提供 / 无权限":h.MemoryModules.Count.ToString());foreach(var m in h.MemoryModules){Line(p,m.Slot,(m.CapacityBytes>0?(m.CapacityBytes/1073741824.0).ToString("0.##")+" GiB":"容量未提供")+" · "+m.Generation);Line(p,"厂商 / 料号",m.Manufacturer+" / "+m.PartNumber);Line(p,"额定 / 配置速率",m.RatedSpeed+" / "+m.ConfiguredSpeed);}}
  if(!string.IsNullOrEmpty(h.Error))Line(p,"采集状态",h.Error);
  return new Border{Background=Theme.Surface,BorderBrush=Theme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(22),Margin=new Thickness(0,8,0,8),Child=p};
 }

}
