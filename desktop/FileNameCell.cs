using System;using System.ComponentModel;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
public sealed class FileNameCell:Grid {
 FileEntry entry;readonly TextBlock name;readonly IconView arrow,symbol;bool listening;
 public FileNameCell(){ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});Margin=new Thickness(12,0,12,0);arrow=new IconView("ChevronRight",11);Children.Add(arrow);symbol=new IconView("Folder",17,Theme.Teal);Grid.SetColumn(symbol,1);Children.Add(symbol);name=Theme.Text("",15,Theme.Ink);Grid.SetColumn(name,2);Children.Add(name);DataContextChanged+=(s,e)=>Attach(e.NewValue as FileEntry);Loaded+=(s,e)=>Attach(DataContext as FileEntry);Unloaded+=(s,e)=>Detach();}
 void Detach(){if(entry!=null&&listening)entry.PropertyChanged-=Changed;listening=false;}
 void Attach(FileEntry next){Detach();entry=next;if(entry!=null){entry.PropertyChanged+=Changed;listening=true;}Update();}
 void Changed(object sender,PropertyChangedEventArgs args){Update();}
 void Update(){if(entry==null)return;Margin=new Thickness(12+entry.Depth*16,0,12,0);name.Text=entry.Name;name.ToolTip=entry.FullPath;arrow.Visibility=entry.IsDirectory?Visibility.Visible:Visibility.Hidden;arrow.Symbol=entry.Expanded?"ChevronDown":"ChevronRight";arrow.InvalidateVisual();symbol.Symbol=entry.IsDirectory?"Folder":"FileText";symbol.Stroke=entry.IsDirectory?Theme.Teal:Theme.Muted;symbol.InvalidateVisual();}
}
