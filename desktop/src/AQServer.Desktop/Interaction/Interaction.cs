using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
public sealed class HoverReading {
 public DateTime TargetUtc,SampleUtc;public double Value=double.NaN;public string Metric,Error,Detail;public bool Gap;
 public string Text {get{if(!Gap&&!string.IsNullOrEmpty(Detail))return SampleUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")+"\n"+Detail;if(Gap)return TargetUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")+" · 本机时间\n该位置无有效采样"+(!string.IsNullOrEmpty(Error)?"\n采集失败："+Error:"")+"\n最近记录 "+SampleUtc.ToLocalTime().ToString("HH:mm:ss.fff");return SampleUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff")+" · 实际采样\n"+Metric+"  "+(double.IsNaN(Value)?"未提供":Value.ToString("0.0",CultureInfo.InvariantCulture)+"%")+"\n本机时间 · 最近真实采样点";}}
}
public static class ChartHover {
 public static int Nearest<T>(IList<T> data,long target,Func<T,long> tick){if(data==null||data.Count==0)return -1;int lo=0,hi=data.Count-1;while(lo<hi){int mid=lo+(hi-lo)/2;if(tick(data[mid])<target)lo=mid+1;else hi=mid;}if(lo>0&&Math.Abs(tick(data[lo-1])-target)<=Math.Abs(tick(data[lo])-target))return lo-1;return lo;}
 public static void Draw(DrawingContext dc,FrameworkElement element,HoverReading reading,Point mouse,double left,double top,double w,double h,long from,long to){if(reading==null)return;double x=left+w*Math.Max(0,Math.Min(1,(reading.TargetUtc.Ticks-from)/(double)Math.Max(1,to-from)));var pen=new Pen(Theme.Teal,1){DashStyle=DashStyles.Dash};dc.DrawLine(pen,new Point(x,top),new Point(x,top+h));if(!reading.Gap&&!double.IsNaN(reading.Value)){double px=left+w*(reading.SampleUtc.Ticks-from)/(double)Math.Max(1,to-from),py=top+h*(1-Math.Max(0,Math.Min(100,reading.Value))/100);dc.DrawRectangle(Theme.Green,new Pen(Theme.Ink,1),new Rect(px-3,py-3,6,6));}
 var text=new FormattedText(reading.Text,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface(Theme.Chinese.Source),11,Theme.Ink,VisualTreeHelper.GetDpi(element).PixelsPerDip);text.MaxTextWidth=Math.Max(30,Math.Min(290,element.ActualWidth-24));text.MaxTextHeight=Math.Max(24,element.ActualHeight-24);text.Trimming=TextTrimming.CharacterEllipsis;double bw=text.Width+20,bh=text.Height+16,bx=Math.Max(2,Math.Min(element.ActualWidth-bw-2,mouse.X+14)),by=Math.Max(2,Math.Min(element.ActualHeight-bh-2,mouse.Y-bh-10));dc.DrawRectangle(Theme.Surface,new Pen(Theme.Teal,1),new Rect(bx,by,bw,bh));dc.DrawText(text,new Point(bx+10,by+8));}
}
