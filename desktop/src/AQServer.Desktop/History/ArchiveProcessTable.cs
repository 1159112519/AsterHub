using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

// Recycle visible process rows instead of laying out every archived process on each click.
public sealed class ArchiveProcessTable : Border {
 readonly ObservableCollection<ProcessRow> rows=new ObservableCollection<ProcessRow>();
 readonly ListBox list;
 readonly Grid header;
 readonly TextBlock count,empty;
 public sealed class ProcessRow : INotifyPropertyChanged {
  string gpu,pid,user,kind,name,memory,tip;
  public string Gpu{get{return gpu;}}public string Pid{get{return pid;}}public string User{get{return user;}}public string Kind{get{return kind;}}public string Name{get{return name;}}public string Memory{get{return memory;}}public string Tip{get{return tip;}}
  public event PropertyChangedEventHandler PropertyChanged;
  void Set(ref string field,string value,string property){value=value??"—";if(field==value)return;field=value;var changed=PropertyChanged;if(changed!=null)changed(this,new PropertyChangedEventArgs(property));}
  public void Update(GpuProcess process){
   Set(ref gpu,process.Gpu,"Gpu");Set(ref pid,process.Pid,"Pid");Set(ref user,process.User,"User");Set(ref kind,process.Type,"Kind");Set(ref name,GpuTelemetry.ProcessName(process.Name),"Name");Set(ref memory,GpuTelemetry.Memory(process.Memory),"Memory");
   Set(ref tip,(process.MemorySource??"NVIDIA · 进程显存")+(string.IsNullOrEmpty(process.SharedMemory)?"":"\n共享 GPU 内存："+process.SharedMemory)+"\nC：计算进程；C/G：Windows GPU 计数器记录。","Tip");
  }
 }
 static readonly double[] widths={40,76,100,56,-1,118};
 static readonly string[] properties={"Gpu","Pid","User","Kind","Name","Memory"};
 static Grid Grid(){var grid=new Grid{MinHeight=34};foreach(double width in widths)grid.ColumnDefinitions.Add(new ColumnDefinition{Width=width<0?new GridLength(1,GridUnitType.Star):new GridLength(width)});return grid;}
 static DataTemplate RowTemplate(){
  var grid=new FrameworkElementFactory(typeof(Grid));grid.SetValue(FrameworkElement.MinHeightProperty,34.0);
  foreach(double width in widths){var column=new FrameworkElementFactory(typeof(ColumnDefinition));column.SetValue(ColumnDefinition.WidthProperty,width<0?new GridLength(1,GridUnitType.Star):new GridLength(width));grid.AppendChild(column);}
  for(int i=0;i<properties.Length;i++){
   var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding(properties[i]));text.SetBinding(FrameworkElement.ToolTipProperty,new Binding(properties[i]));text.SetValue(TextBlock.FontFamilyProperty,Theme.Mono);text.SetValue(TextBlock.FontSizeProperty,13.0);text.SetValue(TextBlock.ForegroundProperty,(i==5?Theme.Amber:Theme.Ink).CloneCurrentValue());text.SetValue(FrameworkElement.MarginProperty,new Thickness(6,7,8,7));text.SetValue(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis);text.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);if(i==2)text.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);if(i==5)text.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Right);text.SetValue(System.Windows.Controls.Grid.ColumnProperty,i);grid.AppendChild(text);
  }
  return new DataTemplate{VisualTree=grid};
 }
 static Style RowStyle(){
  var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));style.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty,null));style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new Binding("Tip")));
  var border=new FrameworkElementFactory(typeof(Border));border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=RelativeSource.TemplatedParent});var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Stretch);border.AppendChild(presenter);style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=border}));
  var stripe=new Trigger{Property=ItemsControl.AlternationIndexProperty,Value=1};stripe.Setters.Add(new Setter(Control.BackgroundProperty,Theme.Raised.CloneCurrentValue()));style.Triggers.Add(stripe);
  foreach(var property in new[]{UIElement.IsMouseOverProperty,ListBoxItem.IsSelectedProperty}){var trigger=new Trigger{Property=property,Value=true};trigger.Setters.Add(new Setter(Control.BackgroundProperty,Theme.Raised.CloneCurrentValue()));style.Triggers.Add(trigger);}return style;
 }
 public ArchiveProcessTable(Metrics metrics){
  CornerRadius=new CornerRadius(8);Background=Theme.Surface;BorderBrush=Theme.Line;BorderThickness=new Thickness(1);Padding=new Thickness(18);Margin=new Thickness(0,8,16,0);
  var wrap=new StackPanel();var heading=new Grid{Margin=new Thickness(0,0,0,16)};
  heading.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});heading.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});heading.Children.Add(Theme.Text("GPU 进程 · 历史快照（只读）",16,Theme.Ink,false,true));
  count=Theme.Text("",12,Theme.Muted);System.Windows.Controls.Grid.SetColumn(count,1);heading.Children.Add(count);wrap.Children.Add(heading);
  header=Grid();string[] labels={"GPU","PID","用户","类型","进程名","专用显存"};
  for(int i=0;i<labels.Length;i++){var text=Theme.Text(labels[i],12,Theme.Muted);text.Margin=new Thickness(6,7,8,7);if(i==5)text.HorizontalAlignment=HorizontalAlignment.Right;System.Windows.Controls.Grid.SetColumn(text,i);header.Children.Add(text);}wrap.Children.Add(header);
  empty=Theme.Text("",13,Theme.Muted);empty.Margin=new Thickness(0,16,0,4);wrap.Children.Add(empty);
  list=new ListBox{ItemsSource=rows,ItemTemplate=RowTemplate(),ItemContainerStyle=RowStyle(),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),AlternationCount=2,HorizontalContentAlignment=HorizontalAlignment.Stretch};
  ScrollViewer.SetHorizontalScrollBarVisibility(list,ScrollBarVisibility.Disabled);ScrollViewer.SetVerticalScrollBarVisibility(list,ScrollBarVisibility.Auto);ScrollViewer.SetCanContentScroll(list,true);VirtualizingPanel.SetIsVirtualizing(list,true);VirtualizingPanel.SetVirtualizationMode(list,VirtualizationMode.Recycling);VirtualizingPanel.SetScrollUnit(list,ScrollUnit.Pixel);
  // Let the page continue scrolling when the table has reached either end.
  list.PreviewMouseWheel+=(sender,args)=>{var viewer=FindScroll(list);if(viewer==null)return;bool edge=args.Delta>0?viewer.VerticalOffset<=0:viewer.VerticalOffset>=viewer.ScrollableHeight;if(edge){args.Handled=true;RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(args.MouseDevice,args.Timestamp,args.Delta){RoutedEvent=UIElement.MouseWheelEvent,Source=this});}};
  wrap.Children.Add(list);Child=wrap;Update(metrics);
 }
 static ScrollViewer FindScroll(DependencyObject root){var found=root as ScrollViewer;if(found!=null)return found;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){found=FindScroll(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}return null;}
 public void Update(Metrics metrics){
  string label=metrics.Processes.Count+" 个 GPU 进程";if(count.Text!=label)count.Text=label;
  empty.Visibility=metrics.Processes.Count==0?Visibility.Visible:Visibility.Collapsed;empty.Text=metrics.Gpus.Count==0?"无 NVIDIA GPU 进程":"当时没有 GPU 进程";list.Visibility=metrics.Processes.Count==0?Visibility.Collapsed:Visibility.Visible;
  // A bounded viewport enables WPF recycling and keeps large snapshots from resizing the page.
  list.Height=Math.Min(12,Math.Max(1,metrics.Processes.Count))*36.0;header.Margin=new Thickness(0,0,metrics.Processes.Count>12?SystemParameters.VerticalScrollBarWidth:0,0);
  while(rows.Count>metrics.Processes.Count)rows.RemoveAt(rows.Count-1);
  for(int i=0;i<metrics.Processes.Count;i++){if(i>=rows.Count)rows.Add(new ProcessRow());rows[i].Update(metrics.Processes[i]);}
 }
}
