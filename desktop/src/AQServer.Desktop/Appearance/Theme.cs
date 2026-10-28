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

public static partial class Theme {
 public static readonly SolidColorBrush Bg=B("#0C1218"),Surface=B("#131E26"),Raised=B("#1B2A32"),Line=B("#31454E"),Ink=B("#E9F0ED"),Muted=B("#98ADA9"),Green=B("#A1EF9B"),Amber=B("#E7CB75"),Red=B("#FA9981"),Teal=B("#7FBDB9");

 public static string Name="forest";
 public static readonly string[] Keys={"forest","ocean","graphite","light"};
 public static readonly string[] Labels={"苔绿 · 夜色","蓝调 · 深海","暖灰 · 工作台","浅色 · 纸面"};
 public static void Apply(string name){int i=Array.IndexOf(Keys,name);if(i<0)i=0;Name=Keys[i];TerminalBg.Color=(Color)ColorConverter.ConvertFromString(Name=="light"?"#F1F3F6":Name=="graphite"?"#272224":Name=="ocean"?"#131E30":"#111B20");Sidebar.Color=(Color)ColorConverter.ConvertFromString(Name=="light"?"#F0F1F4":Name=="graphite"?"#383132":Name=="ocean"?"#0D1829":"#101C22");AccentSoft.Color=(Color)ColorConverter.ConvertFromString(Name=="light"?"#E4EDFF":Name=="graphite"?"#554947":Name=="ocean"?"#203852":"#213C38");Accent.Color=(Color)ColorConverter.ConvertFromString(Name=="light"?"#476B9A":Name=="graphite"?"#DDC3B3":Name=="forest"?"#A8C4B3":"#A2BBDC");string[][] palettes={
 new[]{"#0C1218","#131E26","#1B2A32","#31454E","#E9F0ED","#98ADA9","#A1EF9B","#E7CB75","#FA9981","#7FBDB9"},
 new[]{"#0B1224","#121F35","#1C2E49","#354C6C","#E8F2FF","#9DB2CC","#76CEFF","#F0D28A","#FF9E9E","#93AEFF"},
 new[]{"#2C2729","#332D2F","#40383A","#53494C","#F0E5D6","#B8ACA3","#A9C5AE","#E1C28B","#EBABA0","#B5C6CD"},
 new[]{"#FBFBFD","#FFFFFF","#F0F1F4","#E1E4EA","#20232A","#687180","#338160","#A97729","#C74D56","#416BA4"}};
 SolidColorBrush[] brushes={Bg,Surface,Raised,Line,Ink,Muted,Green,Amber,Red,Teal};for(int n=0;n<brushes.Length;n++)brushes[n].Color=(Color)ColorConverter.ConvertFromString(palettes[i][n]);if(Application.Current!=null)Application.Current.Resources=Resources();
 }
 public static object Parse(string xaml){string[] old={"#0C1218","#131E26","#1B2A32","#31454E","#E9F0ED","#98ADA9","#A1EF9B","#E7CB75","#FA9981","#7FBDB9","#40585F","#263B43","#36524F","#365F50","#263E42","#294D40","#49656B"};SolidColorBrush[] b={Bg,Surface,Raised,Line,Ink,Muted,Green,Amber,Red,Teal,Line,Raised,Raised,Line,Raised,Line,Muted};for(int n=0;n<old.Length;n++)xaml=xaml.Replace(old[n],"__COLOR"+n+"__");for(int n=0;n<old.Length;n++)xaml=xaml.Replace("__COLOR"+n+"__",b[n].Color.ToString());return XamlReader.Parse(xaml);}
 public static readonly FontFamily Chinese=new FontFamily("Microsoft YaHei UI, Segoe UI"),Mono=new FontFamily("Segoe UI Variable Text, Segoe UI, Microsoft YaHei UI");
 public static SolidColorBrush B(string hex){var b=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));return b;}
 public static TextBlock Text(string value,double size,Brush color,bool mono=false,bool bold=false){return new TextBlock{Text=value??"",FontFamily=mono?Mono:Chinese,FontSize=Math.Max(12,size),LineStackingStrategy=LineStackingStrategy.MaxHeight,Foreground=color,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,VerticalAlignment=VerticalAlignment.Center};}
 public static Button Button(string title,Action action){var button=new Button{Content=title,MinHeight=40,Padding=new Thickness(14,7,14,7),FontFamily=Chinese,FontWeight=FontWeights.Medium,FontSize=15,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};button.Click+=(a,b)=>action();Motion.Feedback(button);return button;}
 public static Grid Pair(string label,string value,Brush color=null){var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});var l=Text(label,12,Muted);var v=Text(value,13,color??Ink,true,true);v.HorizontalAlignment=HorizontalAlignment.Right;v.Margin=new Thickness(12,0,0,0);Grid.SetColumn(v,1);row.Children.Add(l);row.Children.Add(v);return row;}
 public static ResourceDictionary Resources(){var resources=(ResourceDictionary)Parse(File.ReadAllText(Path.Combine(AppPaths.InstallRoot,"resources","ui","Controls.xaml"),Encoding.UTF8));resources["ActionHoverBrush"]=Raised.CloneCurrentValue();return resources;}
}
