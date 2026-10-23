using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

// A vertical history layout. Only the edit handles participate in dragging.
// SetOrder restores settings without firing OrderChanged; user moves fire it once.
public sealed class ArchiveLayout : Grid {
 readonly string[] names={"curve","process","hardware"};
 readonly List<string> order=new List<string>();
 readonly Dictionary<string,Module> modules=new Dictionary<string,Module>(StringComparer.OrdinalIgnoreCase);
 readonly StackPanel stack=new StackPanel();
 readonly CheckBox edit=new CheckBox{Content="编辑模块顺序",Margin=new Thickness(0,4,0,10)};
 string dragging; int insertion=-1; bool changing;
 readonly DispatcherTimer edgeScroll=new DispatcherTimer(DispatcherPriority.Input);ScrollViewer dragScroll;
 sealed class Module {
  public Grid Card; public ContentControl Body; public DockPanel Tools;
  public Border Marker; public TranslateTransform Offset=new TranslateTransform();
  public Button Up,Down;
 }
 public event Action<string[]> OrderChanged;
 public string[] Order {get{return order.ToArray();}}
 public bool IsEditing {get{return edit.IsChecked==true;}set{edit.IsChecked=value;}}
 public ArchiveLayout(){
  RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  edit.FontFamily=Theme.Chinese;Children.Add(edit);SetRow(stack,1);Children.Add(stack);
  edgeScroll.Interval=TimeSpan.FromMilliseconds(24);edgeScroll.Tick+=(s,e)=>ScrollAtEdge();
  foreach(string name in names){order.Add(name);modules.Add(name,Create(name));}
  edit.Checked+=(s,e)=>UpdateEditing();edit.Unchecked+=(s,e)=>{CancelDrag();UpdateEditing();};
  Unloaded+=(s,e)=>CancelDrag();
  Rebuild();UpdateEditing();
 }
 Module Create(string name){
  var m=new Module();m.Card=new Grid{Margin=new Thickness(0,0,0,12),RenderTransform=m.Offset};
  m.Card.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  m.Card.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
  m.Tools=new DockPanel{Margin=new Thickness(0,0,0,6)};
  var actions=new StackPanel{Orientation=Orientation.Horizontal};DockPanel.SetDock(actions,Dock.Right);
  m.Up=new Button{Content="上移 ↑",Padding=new Thickness(9,3,9,3),Margin=new Thickness(4,0,0,0)};
  m.Down=new Button{Content="下移 ↓",Padding=new Thickness(9,3,9,3),Margin=new Thickness(4,0,0,0)};
  m.Up.FontFamily=m.Down.FontFamily=Theme.Chinese;
  m.Up.Click+=(s,e)=>Move(name,order.IndexOf(name)-1);m.Down.Click+=(s,e)=>Move(name,order.IndexOf(name)+1);
  actions.Children.Add(m.Up);actions.Children.Add(m.Down);m.Tools.Children.Add(actions);
  var handle=new Thumb{Height=28,MinWidth=120,Cursor=Cursors.SizeNS,ToolTip="按住拖动，调整模块顺序",Focusable=true};
  // A template keeps the drag hit target isolated from charts and table rows.
  var border=new FrameworkElementFactory(typeof(Border));
  border.SetValue(Border.BackgroundProperty,Theme.Raised.CloneCurrentValue());
  border.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));
  var text=new FrameworkElementFactory(typeof(TextBlock));
  text.SetValue(TextBlock.TextProperty,"⠿  "+Title(name));
  text.SetValue(TextBlock.FontFamilyProperty,Theme.Chinese);
  text.SetValue(TextBlock.MarginProperty,new Thickness(10,4,10,4));
  text.SetValue(TextBlock.ForegroundProperty,Theme.Ink.CloneCurrentValue());
  border.AppendChild(text);handle.Template=new ControlTemplate(typeof(Thumb)){VisualTree=border};
  handle.DragStarted+=(s,e)=>{if(!IsEditing)return;CancelDrag();dragging=name;insertion=-1;FindScrollHost();};
  handle.DragDelta+=(s,e)=>{if(dragging==name)Indicate(Mouse.GetPosition(stack).Y);};
  handle.DragCompleted+=(s,e)=>{if(dragging!=name)return;int slot=insertion;CancelDrag();if(e.Canceled||slot<0)return;int old=order.IndexOf(name);if(slot>old)slot--;if(slot>=0&&slot<order.Count&&slot!=old)Reorder(name,slot);};
  handle.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){CancelDrag();e.Handled=true;}};
  m.Tools.Children.Add(handle);m.Card.Children.Add(m.Tools);
  m.Body=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch};SetRow(m.Body,1);m.Card.Children.Add(m.Body);
  m.Marker=new Border{Height=3,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
  m.Marker.Background=Theme.Teal;
  SetRowSpan(m.Marker,2);Panel.SetZIndex(m.Marker,10);m.Card.Children.Add(m.Marker);
  return m;
 }
 static string Title(string name){return name=="curve"?"历史曲线":name=="process"?"历史进程":"硬件与显存";}
 public void SetModules(Dictionary<string,UIElement> value){
  if(value==null)throw new ArgumentNullException("value");
  // Keep the same card/transform/handle when a snapshot refresh replaces its body.
  CancelDrag();
  foreach(string name in names){UIElement child;value.TryGetValue(name,out child);var m=modules[name];if(!ReferenceEquals(m.Body.Content,child))m.Body.Content=child;m.Card.Visibility=child==null?Visibility.Collapsed:Visibility.Visible;}
  UpdateEditing();
 }
 public void SetOrder(string[] value){
  CancelDrag();var next=new List<string>();
  if(value!=null)foreach(string name in value){if(name==null)continue;string key=names.FirstOrDefault(x=>string.Equals(x,name,StringComparison.OrdinalIgnoreCase));if(key!=null&&!next.Contains(key))next.Add(key);}
  foreach(string name in names)if(!next.Contains(name))next.Add(name);
  if(order.SequenceEqual(next))return;order.Clear();order.AddRange(next);Rebuild();
 }
 void UpdateEditing(){foreach(string name in names){var m=modules[name];m.Tools.Visibility=IsEditing?Visibility.Visible:Visibility.Collapsed;int i=order.IndexOf(name);m.Up.IsEnabled=i>0;m.Down.IsEnabled=i<order.Count-1;}}
 void Rebuild(){stack.Children.Clear();foreach(string name in order)stack.Children.Add(modules[name].Card);UpdateEditing();}
 public void Move(string key,int targetIndex){if(!IsEditing||key==null)return;string name=order.FirstOrDefault(x=>string.Equals(x,key,StringComparison.OrdinalIgnoreCase));if(name==null||targetIndex<0||targetIndex>=order.Count||order.IndexOf(name)==targetIndex)return;Reorder(name,targetIndex);}
 void Reorder(string name,int target){
  if(changing)return;changing=true;
  try{
   var before=new Dictionary<string,double>();bool animate=IsLoaded&&SystemParameters.ClientAreaAnimation;
   foreach(string key in order){var m=modules[key];if(animate&&m.Card.IsVisible)before[key]=m.Card.TranslatePoint(new Point(0,0),stack).Y;m.Offset.BeginAnimation(TranslateTransform.YProperty,null);m.Offset.Y=0;}
   order.Remove(name);order.Insert(target,name);Rebuild();
   if(animate){stack.UpdateLayout();foreach(var entry in before){var m=modules[entry.Key];double distance=entry.Value-m.Card.TranslatePoint(new Point(0,0),stack).Y;if(Math.Abs(distance)<.5)continue;m.Offset.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(distance,0,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop});}}
  }finally{changing=false;}
  var notify=OrderChanged;if(notify!=null)notify(Order);
 }
 void Indicate(double y){
  foreach(var m in modules.Values)m.Marker.Visibility=Visibility.Collapsed;
  var visible=order.Where(x=>modules[x].Card.Visibility==Visibility.Visible).ToList();if(visible.Count==0)return;
  string anchor=visible[visible.Count-1];bool after=true;insertion=order.IndexOf(anchor)+1;
  foreach(string name in visible){var card=modules[name].Card;double top=card.TranslatePoint(new Point(0,0),stack).Y;if(y<top+card.ActualHeight/2){anchor=name;after=false;insertion=order.IndexOf(name);break;}}
  var marker=modules[anchor].Marker;marker.VerticalAlignment=after?VerticalAlignment.Bottom:VerticalAlignment.Top;marker.Visibility=Visibility.Visible;
 }
 void FindScrollHost(){DependencyObject parent=this;while(parent!=null){parent=VisualTreeHelper.GetParent(parent);var scroll=parent as ScrollViewer;if(scroll!=null){dragScroll=scroll;edgeScroll.Start();return;}}}
 void ScrollAtEdge(){if(dragging==null||!IsEditing||dragScroll==null){CancelDrag();return;}double y=Mouse.GetPosition(dragScroll).Y;double height=dragScroll.ViewportHeight;if(dragScroll.CanContentScroll)height=dragScroll.ActualHeight;double delta=y<38?-12:y>height-38?12:0;if(delta!=0&&!dragScroll.CanContentScroll)dragScroll.ScrollToVerticalOffset(Math.Max(0,Math.Min(dragScroll.ScrollableHeight,dragScroll.VerticalOffset+delta)));Indicate(Mouse.GetPosition(stack).Y);}
 void CancelDrag(){edgeScroll.Stop();dragScroll=null;dragging=null;insertion=-1;foreach(var m in modules.Values)m.Marker.Visibility=Visibility.Collapsed;}
}


