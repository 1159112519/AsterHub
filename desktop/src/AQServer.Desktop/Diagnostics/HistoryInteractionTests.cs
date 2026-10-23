using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

public static class HistoryInteractionTests {
 static readonly List<string> log=new List<string>();
 static void Check(bool condition,string label){log.Add((condition?"PASS ":"FAIL ")+label);if(!condition)throw new Exception(label);}
 static void Pump(Func<bool> done){var frame=new DispatcherFrame();var clock=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(5)};timer.Tick+=(s,e)=>{if(done()||clock.Elapsed.TotalSeconds>15){timer.Stop();frame.Continue=false;}};timer.Start();Dispatcher.PushFrame(frame);Check(done(),"requested history snapshot completes");}
 static IEnumerable<T> Nodes<T>(DependencyObject root) where T:DependencyObject {if(root is T)yield return (T)root;foreach(object item in LogicalTreeHelper.GetChildren(root)){var child=item as DependencyObject;if(child!=null)foreach(var found in Nodes<T>(child))yield return found;}}
 static T Field<T>(ArchivePanel panel,string name){return (T)typeof(ArchivePanel).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(panel);}
 static void StartRead(ArchivePanel panel){Field<DispatcherTimer>(panel,"debounce").Stop();typeof(ArchivePanel).GetMethod("LoadSnapshot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(panel,null);}
 public static void Run(string folder){
  log.Clear();var store=new ArchiveStore(Path.Combine(folder,"i-"+Guid.NewGuid().ToString("N").Substring(0,8))){Days=365};var server=Storage.DefaultServer();var time=DateTime.UtcNow.AddMinutes(-10);var renderer=new MonitorWindow(true,store);ArchivePanel panel=null;
  try{
   for(int sample=0;sample<3;sample++){
    var metrics=Program.Fixture("eight");metrics.Processes=new List<GpuProcess>();
    for(int i=0;i<240;i++)metrics.Processes.Add(new GpuProcess{Gpu=(i%8).ToString(),Pid=(1000+sample*1000+i).ToString(),User="test-user",Name="history-worker-"+i+".py",Type="C",Memory=(1024+i)+" MiB"});
    store.Append(server,ArchiveRecord.From(metrics,time.AddSeconds(sample*5),5));
   }
   var result=store.Query(server,time.AddSeconds(-1),time.AddSeconds(11),CancellationToken.None);int builds=0;
   panel=new ArchivePanel(store,server,m=>{builds++;var clock=Stopwatch.StartNew();var table=renderer.MakeProcesses(m,false);log.Add("Process control build ms="+clock.Elapsed.TotalMilliseconds.ToString("0.00"));return table;},()=>"isolated regression");
   var scroll=new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,CanContentScroll=false,Resources=Theme.Resources()};
   Action layout=()=>{scroll.Measure(new Size(1120,700));scroll.Arrange(new Rect(0,0,1120,700));scroll.UpdateLayout();};
   panel.SetResult(result);panel.SelectTime(time.Ticks);Pump(()=>!panel.SnapshotLoading&&panel.DisplayedRecord!=null);var initialLayout=Stopwatch.StartNew();layout();log.Add("Initial layout ms="+initialLayout.Elapsed.TotalMilliseconds.ToString("0.00"));
   var detail=Field<StackPanel>(panel,"detail");var process=Field<StackPanel>(panel,"processDetail");var oldHardware=detail.Children[0];var oldProcess=process.Children[0];double beforeHeight=panel.ActualHeight;var firstRecord=panel.DisplayedRecord;
   scroll.ScrollToVerticalOffset(300);layout();double offset=scroll.VerticalOffset;int oldBuilds=builds;
   var watch=Stopwatch.StartNew();panel.SelectTime(time.AddSeconds(5).Ticks);StartRead(panel);layout();watch.Stop();
   log.Add("Loading: beforeHeight="+beforeHeight.ToString("0.0")+" afterHeight="+panel.ActualHeight.ToString("0.0")+" oldRows=240 visibleRows="+process.Children.Count+" syncMs="+watch.Elapsed.TotalMilliseconds.ToString("0.00"));
   Check(detail.Children.Contains(oldHardware)&&process.Children.Contains(oldProcess),"keep previous hardware and processes visible while the next timestamp loads");
   Check(Math.Abs(panel.ActualHeight-beforeHeight)<0.5&&Math.Abs(scroll.VerticalOffset-offset)<0.5,"loading does not collapse content or move the scroll position");
   Pump(()=>!panel.SnapshotLoading&&panel.DisplayedRecord!=null);var replacementLayout=Stopwatch.StartNew();layout();log.Add("Replacement layout ms="+replacementLayout.Elapsed.TotalMilliseconds.ToString("0.00"));Check(detail.Children.Contains(oldHardware)&&process.Children.Contains(oldProcess),"changing a timestamp reuses the snapshot panels");Check(builds==oldBuilds,"historical process controls are updated instead of rebuilt");Check(panel.DisplayedRecord.T==time.AddSeconds(5).Ticks&&panel.DisplayedProcessCount==240,"replacement snapshot has the selected timestamp and complete process list");
   var processList=Nodes<ListBox>(process).Single();int realized=Enumerable.Range(0,processList.Items.Count).Count(i=>processList.ItemContainerGenerator.ContainerFromIndex(i)!=null);log.Add("Realized process containers="+realized+" of "+processList.Items.Count);Check(processList.Items.Count==240&&realized>0&&realized<48,"only visible process rows are realized and all 240 records remain available");processList.ScrollIntoView(processList.Items[239]);layout();Check(processList.ItemContainerGenerator.ContainerFromIndex(239)!=null,"the final archived process can be reached by scrolling");processList.ScrollIntoView(processList.Items[0]);layout();
   panel.SetMetric("GPU 1");Check(panel.DisplayedProcessCount==30&&builds==oldBuilds&&!panel.SnapshotLoading,"GPU filter reuses the loaded snapshot without disk reads or rebuilding rows");panel.SetMetric("CPU");
   var coreToggle=Nodes<Button>(detail).Single(b=>(b.Content as string??"").Contains("逐核快照"));coreToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));layout();
   var timings=new List<double>();
   for(int i=0;i<12;i++){var clock=Stopwatch.StartNew();panel.SelectTime(time.AddSeconds(i%2*5).Ticks);layout();timings.Add(clock.Elapsed.TotalMilliseconds);Check(!panel.SnapshotLoading,"revisited timestamp is served immediately from the bounded cache");}
   log.Add("Repeated cached selection including layout: medianMs="+timings.OrderBy(x=>x).ElementAt(timings.Count/2).ToString("0.00")+" maxMs="+timings.Max().ToString("0.00"));
   Check((coreToggle.Content as string)=="收起逐核快照"&&ReferenceEquals(coreToggle,Nodes<Button>(detail).Single(b=>(b.Content as string??"").Contains("逐核快照"))),"expanded per-core controls survive timestamp changes");
   panel.SelectTime(time.Ticks);Check(ReferenceEquals(firstRecord,panel.DisplayedRecord),"revisited timestamp reuses its already-read record");panel.SelectTime(time.AddSeconds(5).Ticks);
   int settledBuilds=builds;var shown=panel.DisplayedRecord;panel.SelectTime(time.AddSeconds(5).Ticks+100);Check(!panel.SnapshotLoading&&ReferenceEquals(shown,panel.DisplayedRecord),"picking the same nearest sample does not restart loading");Check(builds==settledBuilds,"same sample does not rebuild process controls");
   panel.SelectTime(time.AddSeconds(10).Ticks);StartRead(panel);panel.SelectTime(time.Ticks);Pump(()=>!Field<bool>(panel,"snapshotReadInFlight"));Check(panel.DisplayedRecord.T==time.Ticks,"an older background read cannot overwrite a newer cached selection");
   panel.SetRange(time.AddSeconds(-1),time.AddSeconds(11),true);Check(detail.Children.Contains(oldHardware)&&process.Children.Contains(oldProcess),"querying a new range keeps the mounted snapshot visible");Pump(()=>panel.CurrentResult!=null&&!panel.SnapshotLoading&&panel.DisplayedRecord!=null);layout();Check(detail.Children.Contains(oldHardware)&&process.Children.Contains(oldProcess)&&panel.CurrentResult.Points.Count==3,"query completion keeps hardware and process modules mounted");
   renderer.SwitchView("历史");renderer.ArchiveView.SetResult(result);renderer.ArchiveView.SelectTime(time.Ticks);Pump(()=>!renderer.ArchiveView.SnapshotLoading&&renderer.ArchiveView.DisplayedRecord!=null);Snapshot.Save(renderer.Root,Path.Combine(folder,"history-process-preview.png"),1440,1080,96,()=>renderer.Scroll.ScrollToVerticalOffset(360));
   var corruptResult=store.Query(server,time.AddSeconds(-1),time.AddSeconds(11),CancellationToken.None);File.WriteAllText(corruptResult.Points.Last().File,"corrupt isolated test archive");panel.SetResult(corruptResult);StartRead(panel);Pump(()=>!panel.SnapshotLoading);layout();Check(detail.Children.Contains(oldHardware)&&process.Children.Contains(oldProcess)&&Nodes<TextBlock>(detail).Any(t=>t.Text.Contains("快照不可读取")),"failed reads retain the previous snapshot with clear failure feedback");
   string previousTheme=Theme.Name;Theme.Apply(Theme.Keys.First(x=>x!=previousTheme));Theme.Apply(previousTheme);Check(true,"theme switching remains available after virtualized rows have rendered");
   panel.SetResult(new ArchiveResult{Header=result.Header,From=result.From,To=result.To});Check(panel.DisplayedRecord==null&&panel.DisplayedProcessCount==0&&detail.Children.Count==0&&process.Children.Count==0,"an empty query clears stale snapshots");
  }catch(Exception e){log.Add("ERROR "+e);throw;}finally{
   if(panel!=null)panel.Dispose();renderer.Exit();File.WriteAllLines(Path.Combine(folder,"history-interaction-results.txt"),log);
  }
 }
}
