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

public static class Snapshot {
 public static void Save(Grid root,string path,int width,int height,int dpi,Action afterLayout){double w=width-48,h=height-48;root.Margin=new Thickness(0);root.Width=w;root.Height=h;root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();if(afterLayout!=null)afterLayout();root.Measure(new Size(w,h));root.Arrange(new Rect(0,0,w,h));root.UpdateLayout();var scene=new DrawingVisual();using(var dc=scene.RenderOpen()){dc.DrawRectangle(Theme.Bg,null,new Rect(0,0,width,height));var brush=new VisualBrush(root){Stretch=Stretch.None,AlignmentX=AlignmentX.Left,AlignmentY=AlignmentY.Top,AutoLayoutContent=false};dc.DrawRectangle(brush,null,new Rect(24,24,w,h));}var bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*dpi/96.0),(int)Math.Ceiling(height*dpi/96.0),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(scene);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using(var f=File.Create(path))png.Save(f);}
}
