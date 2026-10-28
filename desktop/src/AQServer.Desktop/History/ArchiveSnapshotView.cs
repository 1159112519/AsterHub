using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

// Hardware and process hosts are mounted once. Only changed values are updated.
internal sealed class ArchiveSnapshotView {
 public readonly Border Hardware;
 public readonly StackPanel Processes=new StackPanel();
 readonly Func<Metrics,Border> renderProcesses;
 readonly Server server;
 readonly TextBlock title,values,error,noGpu,processTitle,legacy,filter;
 readonly StackPanel readings=new StackPanel(),gpuRows=new StackPanel();
 readonly List<GpuRow> gpus=new List<GpuRow>();
 readonly WrapPanel cores=new WrapPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,12,0,0)};
 readonly List<CoreCell> coreViews=new List<CoreCell>();
 readonly Button coreToggle;
 readonly ContentControl table=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch};
 ArchiveRecord record;Metrics metrics;bool coresExpanded;string processMetric;
 sealed class GpuRow {
  public readonly Grid Element=new Grid{Margin=new Thickness(0,0,0,8),MinHeight=32};
  readonly TextBlock[] texts=new TextBlock[5];
  public GpuRow(bool header){
   double[] widths={58,-1,74,172,64};for(int i=0;i<widths.Length;i++)Element.ColumnDefinitions.Add(new ColumnDefinition{Width=widths[i]<0?new GridLength(1,GridUnitType.Star):new GridLength(widths[i])});
   for(int i=0;i<texts.Length;i++){var t=Theme.Text("",14,header?Theme.Muted:Theme.Ink,false);t.Margin=new Thickness(0,8,10,8);t.TextAlignment=i==1?TextAlignment.Left:TextAlignment.Center;texts[i]=t;Grid.SetColumn(t,i);Element.Children.Add(t);}
   if(header)Set(new[]{"GPU","型号","利用率","显存 MiB","温度"});
  }
  void Set(string[] values){for(int i=0;i<texts.Length;i++){if(texts[i].Text!=values[i]){texts[i].Text=values[i];texts[i].ToolTip=values[i];}}}
  public void Update(Gpu gpu){Set(new[]{gpu.Index,gpu.Name,gpu.Util+"%",gpu.Used+" / "+gpu.Total,gpu.Temp+"°C"});}
 }
 sealed class CoreCell {
  public readonly Border Element;readonly TextBlock name,value;
  public CoreCell(){var content=new StackPanel();name=Theme.Text("",11,Theme.Muted,true);value=Theme.Text("",17,Theme.Green,true);value.Margin=new Thickness(0,8,0,0);content.Children.Add(name);content.Children.Add(value);Element=new Border{Width=100,Padding=new Thickness(10),Margin=new Thickness(0,0,10,10),Background=Theme.Raised,Child=content};}
  public void Update(CpuCore core){string label="CPU "+core.Index;if(name.Text!=label)name.Text=label;label=core.Util.ToString("0.0")+"%";if(value.Text!=label)value.Text=label;value.Foreground=core.Util>=85?Theme.Amber:Theme.Green;}
 }
 public ArchiveSnapshotView(Server device,Func<Metrics,Border> renderer){
  server=device;renderProcesses=renderer;var content=new StackPanel();
  title=Theme.Text("",17,Theme.Ink,false,true);title.TextWrapping=TextWrapping.Wrap;content.Children.Add(title);
  error=Theme.Text("",13,Theme.Red);error.TextWrapping=TextWrapping.Wrap;error.Margin=new Thickness(0,16,0,0);error.Visibility=Visibility.Collapsed;content.Children.Add(error);
  values=Theme.Text("",14,Theme.Muted);values.TextWrapping=TextWrapping.Wrap;values.Margin=new Thickness(0,14,0,16);readings.Children.Add(values);
  noGpu=Theme.Text("当时未检测到 NVIDIA GPU",13,Theme.Muted);readings.Children.Add(noGpu);
  gpuRows.Children.Add(new GpuRow(true).Element);readings.Children.Add(gpuRows);
  coreToggle=Theme.Button("展开逐核快照",()=>{coresExpanded=!coresExpanded;UpdateCores();});coreToggle.HorizontalAlignment=HorizontalAlignment.Left;coreToggle.Margin=new Thickness(0,16,0,0);readings.Children.Add(coreToggle);readings.Children.Add(cores);content.Children.Add(readings);
  Hardware=new Border{Background=Theme.Surface,BorderBrush=Theme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(16),Margin=new Thickness(0,0,0,16),Child=content};
  processTitle=Theme.Text("",13,Theme.Teal,false,true);processTitle.TextWrapping=TextWrapping.Wrap;processTitle.Margin=new Thickness(0,16,0,8);Processes.Children.Add(processTitle);
  legacy=Theme.Text("这条旧记录未保存进程明细，无法还原当时的进程。",13,Theme.Muted);legacy.TextWrapping=TextWrapping.Wrap;Processes.Children.Add(legacy);
  filter=Theme.Text("",12,Theme.Muted);filter.Margin=new Thickness(0,0,0,8);Processes.Children.Add(filter);Processes.Children.Add(table);
 }
 public int Update(ArchiveRecord next,string selectedMetric){
  bool changed=!ReferenceEquals(record,next);record=next;
  if(changed){
   metrics=record.Metrics();title.Text="历史快照 · "+ArchivePanel.LocalTime(record.T);
   bool failed=!string.IsNullOrEmpty(record.Error);error.Visibility=failed?Visibility.Visible:Visibility.Collapsed;error.Text=failed?"当时采集失败："+record.Error:"";readings.Visibility=failed?Visibility.Collapsed:Visibility.Visible;Processes.Visibility=failed?Visibility.Collapsed:Visibility.Visible;
   if(!failed){
    string label="CPU "+metrics.Cpu+" · "+metrics.CpuCores.Count+" 个逻辑处理器 · 内存 "+ResourceSample.MemoryLabel(metrics.Mem);if(metrics.Network!=null)label+="\n网络 ↓ "+PeripheralMetrics.Bytes(metrics.Network.DownloadBytesPerSecond,true)+"   ↑ "+PeripheralMetrics.Bytes(metrics.Network.UploadBytesPerSecond,true);var volume=PeripheralMetrics.PrimaryVolume(metrics);if(volume!=null)label+=" · 存储 "+PeripheralMetrics.Capacity(volume.UsedBytes,volume.TotalBytes)+" ("+volume.UsedPercent.ToString("0.0")+"%)";if(values.Text!=label)values.Text=label;
    noGpu.Visibility=metrics.Gpus.Count==0?Visibility.Visible:Visibility.Collapsed;gpuRows.Visibility=metrics.Gpus.Count==0?Visibility.Collapsed:Visibility.Visible;
    while(gpus.Count>metrics.Gpus.Count){gpuRows.Children.RemoveAt(gpuRows.Children.Count-1);gpus.RemoveAt(gpus.Count-1);}
    for(int i=0;i<metrics.Gpus.Count;i++){if(i>=gpus.Count){var row=new GpuRow(false);gpus.Add(row);gpuRows.Children.Add(row.Element);}gpus[i].Update(metrics.Gpus[i]);}
    coreToggle.Visibility=metrics.CpuCores.Count==0?Visibility.Collapsed:Visibility.Visible;UpdateCores();
    processTitle.Text="进程采样时刻："+ArchivePanel.LocalTime(record.T)+" · "+server.Name;
   }
  }
  if(!string.IsNullOrEmpty(record.Error))return 0;
  bool missing=record.Processes==null;legacy.Visibility=missing?Visibility.Visible:Visibility.Collapsed;table.Visibility=missing?Visibility.Collapsed:Visibility.Visible;
  string gpuId=selectedMetric.StartsWith("GPU ")?selectedMetric.Substring(4):null;filter.Visibility=!missing&&gpuId!=null?Visibility.Visible:Visibility.Collapsed;
  if(gpuId!=null)filter.Text="筛选 GPU "+gpuId+" · 切换到 GPU 总览可查看全部进程";
  if(missing)return 0;
  var filtered=gpuId==null?metrics.Processes:metrics.Processes.Where(p=>p.Gpu==gpuId).ToList();
  if(changed||processMetric!=selectedMetric){
   var data=new Metrics{Cpu=metrics.Cpu,Cores=metrics.Cores,Mem=metrics.Mem,Disk=metrics.Disk,Hardware=metrics.Hardware,Gpus=metrics.Gpus,Processes=filtered};
   var reusable=table.Content as ArchiveProcessTable;if(reusable!=null)reusable.Update(data);else table.Content=renderProcesses(data);processMetric=selectedMetric;
  }
  return filtered.Count;
 }
 void UpdateCores(){
  bool visible=coresExpanded&&metrics!=null&&metrics.CpuCores.Count>0;cores.Visibility=visible?Visibility.Visible:Visibility.Collapsed;coreToggle.Content=coresExpanded?"收起逐核快照":"展开逐核快照";
  if(!visible)return;
  while(coreViews.Count>metrics.CpuCores.Count){cores.Children.RemoveAt(cores.Children.Count-1);coreViews.RemoveAt(coreViews.Count-1);}
  for(int i=0;i<metrics.CpuCores.Count;i++){if(i>=coreViews.Count){var cell=new CoreCell();coreViews.Add(cell);cores.Children.Add(cell.Element);}coreViews[i].Update(metrics.CpuCores[i]);}
 }
}
