using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
public class PaneLayoutEntry { public string Key{get;set;} public double Value{get;set;} }
public class PaneLayoutDocument { public List<PaneLayoutEntry> Entries=new List<PaneLayoutEntry>(); }
public static class PaneLayouts {
 static Dictionary<string,double> values; static readonly object gate=new object();
 public static string PathName=Environment.GetEnvironmentVariable("AQSERVER_LAYOUT_PATH")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AQServer","layout.xml");
 static void Load(){if(values!=null)return;values=new Dictionary<string,double>();try{if(File.Exists(PathName))using(var stream=File.OpenRead(PathName)){var document=(PaneLayoutDocument)new XmlSerializer(typeof(PaneLayoutDocument)).Deserialize(stream);foreach(var entry in document.Entries)if(!string.IsNullOrEmpty(entry.Key)&&!double.IsNaN(entry.Value)&&!double.IsInfinity(entry.Value))values[entry.Key]=entry.Value;}}catch{}}
 public static double Get(string key,double fallback){lock(gate){Load();double value;return values.TryGetValue(key,out value)?value:fallback;}}
 public static void Set(string key,double value){lock(gate){Load();values[key]=value;Flush();}}
 public static void Reset(string key){lock(gate){Load();values.Remove(key);Flush();}}
 public static void Reload(){lock(gate){values=null;Load();}}
 static void Flush(){try{Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(PathName)));string pending=PathName+".tmp";using(var stream=File.Create(pending))new XmlSerializer(typeof(PaneLayoutDocument)).Serialize(stream,new PaneLayoutDocument{Entries=values.Select(x=>new PaneLayoutEntry{Key=x.Key,Value=x.Value}).ToList()});if(File.Exists(PathName))File.Replace(pending,PathName,null);else File.Move(pending,PathName);}catch{}}
 public static ControlTemplate DividerTemplate(){return (ControlTemplate)Theme.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Control'><Grid Background='Transparent'><Border Name='line' Width='1' Background='{TemplateBinding Foreground}' Opacity='0.3' HorizontalAlignment='Center' Margin='0,10,0,10'/></Grid><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='line' Property='Opacity' Value='0.95'/><Setter TargetName='line' Property='Width' Value='2'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='line' Property='Opacity' Value='0'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");}
 public static void RefreshTree(DependencyObject root){var splitter=root as PaneDivider;if(splitter!=null)splitter.Foreground=Theme.Muted.CloneCurrentValue();var handle=root as PaneEdgeHandle;if(handle!=null)handle.Foreground=Theme.Muted.CloneCurrentValue();for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)RefreshTree(VisualTreeHelper.GetChild(root,i));}
 public static double Clamp(double value,double min,double max){return Math.Max(min,Math.Min(Math.Max(min,max),value));}
}
public sealed class PaneDivider : GridSplitter,IDisposable {
 readonly Grid grid;readonly int left,right;readonly string key;readonly double fallback,minLeft,minRight;readonly bool fixedRight;
 PaneDivider(Grid host,int first,int last,string name,double initial,double leftMin,double rightMin,bool pixels){grid=host;left=first;right=last;key=name;fallback=initial;minLeft=leftMin;minRight=rightMin;fixedRight=pixels;ResizeDirection=GridResizeDirection.Columns;ResizeBehavior=GridResizeBehavior.PreviousAndNext;HorizontalAlignment=HorizontalAlignment.Stretch;VerticalAlignment=VerticalAlignment.Stretch;Background=Brushes.Transparent;Foreground=Theme.Muted.CloneCurrentValue();Cursor=Cursors.SizeWE;Focusable=true;Template=PaneLayouts.DividerTemplate();ToolTip="拖动调整宽度 · 双击恢复默认";Grid.SetColumn(this,first+1);host.Children.Add(this);Restore(false);grid.SizeChanged+=Changed;Loaded+=(s,e)=>RefreshLimits();DragCompleted+=(s,e)=>Commit();KeyUp+=(s,e)=>{if(e.Key==Key.Left||e.Key==Key.Right)Commit();};MouseDoubleClick+=(s,e)=>{Restore(true);e.Handled=true;};}
 public static PaneDivider Ratio(Grid grid,int left,int right,string key,double ratio,double minLeft=220,double minRight=220){return new PaneDivider(grid,left,right,key,ratio,minLeft,minRight,false);}
 public static PaneDivider Right(Grid grid,int left,int right,string key,double pixels,double minLeft=300,double minRight=180){return new PaneDivider(grid,left,right,key,pixels,minLeft,minRight,true);}
 void Changed(object s,SizeChangedEventArgs e){RefreshLimits();}
 public void RefreshLimits(){if(Visibility!=Visibility.Visible||grid.ActualWidth<=0)return;double usable=Math.Max(0,grid.ActualWidth-grid.ColumnDefinitions[left+1].ActualWidth);grid.ColumnDefinitions[left].MinWidth=Math.Min(minLeft,usable*.4);grid.ColumnDefinitions[right].MinWidth=Math.Min(minRight,usable*.4);if(fixedRight)grid.ColumnDefinitions[right].MaxWidth=Math.Max(grid.ColumnDefinitions[right].MinWidth,Math.Min(500,usable-grid.ColumnDefinitions[left].MinWidth));}
 public void Restore(bool reset){if(reset)PaneLayouts.Reset(key);double value=PaneLayouts.Get(key,fallback);if(fixedRight){grid.ColumnDefinitions[left].Width=new GridLength(1,GridUnitType.Star);grid.ColumnDefinitions[right].Width=new GridLength(PaneLayouts.Clamp(value,minRight,500));}else{value=PaneLayouts.Clamp(value,.15,.85);grid.ColumnDefinitions[left].Width=new GridLength(value,GridUnitType.Star);grid.ColumnDefinitions[right].Width=new GridLength(1-value,GridUnitType.Star);}RefreshLimits();}
 public void Commit(){grid.UpdateLayout();double first=grid.ColumnDefinitions[left].ActualWidth,last=grid.ColumnDefinitions[right].ActualWidth;if(first<=0||last<=0)return;if(fixedRight)PaneLayouts.Set(key,last);else{double ratio=first/(first+last);PaneLayouts.Set(key,ratio);grid.ColumnDefinitions[left].Width=new GridLength(ratio,GridUnitType.Star);grid.ColumnDefinitions[right].Width=new GridLength(1-ratio,GridUnitType.Star);}}
 public void Dispose(){grid.SizeChanged-=Changed;grid.Children.Remove(this);}
}
public sealed class PaneEdgeHandle : Thumb {
 readonly string key;readonly double fallback,minimum,maximum,sign;readonly Func<double> read,available;readonly Action<double> write;
 public PaneEdgeHandle(string name,double initial,double min,double max,Func<double> getter,Action<double> setter,Func<double> limit=null,double direction=1){key=name;fallback=initial;minimum=min;maximum=max;read=getter;write=setter;available=limit;sign=direction;Width=8;HorizontalAlignment=HorizontalAlignment.Right;VerticalAlignment=VerticalAlignment.Stretch;Cursor=Cursors.SizeWE;Background=Brushes.Transparent;Foreground=Theme.Muted.CloneCurrentValue();Template=PaneLayouts.DividerTemplate();ToolTip="拖动调整宽度 · 双击恢复默认";DragDelta+=(s,e)=>ResizeBy(e.HorizontalChange);DragCompleted+=(s,e)=>PaneLayouts.Set(key,read());MouseDoubleClick+=(s,e)=>{PaneLayouts.Reset(key);write(PaneLayouts.Clamp(fallback,minimum,Limit));e.Handled=true;};}
 double Limit{get{return available==null?maximum:Math.Min(maximum,available());}}
 public void ResizeBy(double delta){write(PaneLayouts.Clamp(read()+delta*sign,minimum,Limit));}
}

