using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
public sealed class WorkspaceLayout : Grid {
 readonly Border rail;readonly ListBox devices;readonly Border content;bool changing;public Action<Server> DeviceSelected;
 public WorkspaceLayout(){ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});var pane=new DockPanel();var heading=Theme.Text("设备",12,Theme.Muted,false,true);heading.Margin=new Thickness(12,12,0,10);DockPanel.SetDock(heading,Dock.Top);pane.Children.Add(heading);devices=new ListBox{BorderThickness=new Thickness(0),Background=Theme.Surface,HorizontalContentAlignment=HorizontalAlignment.Stretch};devices.SelectionChanged+=(s,e)=>{if(!changing&&devices.SelectedItem is ListBoxItem){var selected=((ListBoxItem)devices.SelectedItem).Tag as Server;if(selected!=null&&DeviceSelected!=null)DeviceSelected(selected);}};pane.Children.Add(devices);rail=new Border{Background=Theme.Surface,BorderBrush=Theme.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,12,0),Child=pane};rail.Visibility=Visibility.Collapsed;Children.Add(rail);content=new Border();Grid.SetColumn(content,1);Children.Add(content);SizeChanged+=(s,e)=>{bool show=false;rail.Visibility=show?Visibility.Visible:Visibility.Collapsed;ColumnDefinitions[0].Width=new GridLength(show?160:0);};}
 public void SetDevices(IEnumerable<Server> servers,Server selected){changing=true;devices.Items.Clear();foreach(var server in servers){var lines=new StackPanel();lines.Children.Add(Theme.Text(server.Name,13,Theme.Ink,false,true));var host=Theme.Text(server.IsLocal?"本机设备":server.Host,11,Theme.Muted);host.Margin=new Thickness(0,4,0,0);lines.Children.Add(host);var item=new ListBoxItem{Tag=server,Content=lines,ToolTip=server.IsLocal?Environment.MachineName:server.User+"@"+server.Host+":"+server.Port};devices.Items.Add(item);if(object.ReferenceEquals(server,selected))devices.SelectedItem=item;}changing=false;}
 public void ShowContent(UIElement element){if(!object.ReferenceEquals(content.Child,element))content.Child=element;}
 public void RefreshTheme(){Resources=Theme.Resources();}
}
