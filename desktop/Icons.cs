// Lucide icon geometry, ISC license; see UiAssets/LUCIDE-LICENSE.txt.
using System;using System.Windows;using System.Windows.Media;
public sealed class IconView:FrameworkElement{public string Symbol;public Brush Stroke;public IconView(string symbol,double size=20,Brush color=null){Symbol=symbol;Width=Height=size;Stroke=color??Theme.Muted;VerticalAlignment=VerticalAlignment.Center;}protected override void OnRender(DrawingContext dc){var pen=new Pen(Stroke,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));switch(Symbol){case "LayoutDashboard":
dc.DrawRoundedRectangle(null,pen,new Rect(3,3,7,9),1,1);dc.DrawRoundedRectangle(null,pen,new Rect(14,3,7,5),1,1);dc.DrawRoundedRectangle(null,pen,new Rect(14,12,7,9),1,1);dc.DrawRoundedRectangle(null,pen,new Rect(3,16,7,5),1,1);break;
case "Info":dc.DrawEllipse(null,pen,new Point(12,12),10,10);dc.DrawGeometry(null,pen,Geometry.Parse("M12 16V12 M12 8H12.01"));break;
case "Power":dc.DrawGeometry(null,pen,Geometry.Parse("M12 2V12 M18.4 6.6A9 9 0 1 1 5.63 6.64"));break;
case "LogOut":dc.DrawGeometry(null,pen,Geometry.Parse("M16 17L21 12L16 7 M21 12H9 M9 21H5A2 2 0 0 1 3 19V5A2 2 0 0 1 5 3H9"));break;
case "Server":
dc.DrawRoundedRectangle(null,pen,new Rect(2,2,20,8),2,2);
dc.DrawRoundedRectangle(null,pen,new Rect(2,14,20,8),2,2);
dc.DrawLine(pen,new Point(6,6),new Point(6.01,6));
dc.DrawLine(pen,new Point(6,18),new Point(6.01,18));
break;
case "Laptop":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M18 5a2 2 0 0 1 2 2v8.526a2 2 0 0 0 .212.897l1.068 2.127a1 1 0 0 1-.9 1.45H3.62a1 1 0 0 1-.9-1.45l1.068-2.127A2 2 0 0 0 4 15.526V7a2 2 0 0 1 2-2z"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M20.054 15.987H3.946"));
break;
case "Plus":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M5 12h14"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M12 5v14"));
break;
case "Database":
dc.DrawEllipse(null,pen,new Point(12,5),9,3);
dc.DrawGeometry(null,pen,Geometry.Parse(@"M3 5V19A9 3 0 0 0 21 19V5"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M3 12A9 3 0 0 0 21 12"));
break;
case "Settings2":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M14 17H5"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M19 7h-9"));
dc.DrawEllipse(null,pen,new Point(17,17),3,3);
dc.DrawEllipse(null,pen,new Point(7,7),3,3);
break;
case "SlidersHorizontal":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M10 5H3"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M12 19H3"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M14 3v4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M16 17v4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 12h-9"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 19h-5"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 5h-7"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M8 10v4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M8 12H3"));
break;
case "PictureInPicture2":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 9V6a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v10c0 1.1.9 2 2 2h4"));
dc.DrawRoundedRectangle(null,pen,new Rect(12,13,10,7),2,2);
break;
case "ArrowDownUp":
dc.DrawGeometry(null,pen,Geometry.Parse(@"m3 16 4 4 4-4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M7 20V4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"m21 8-4-4-4 4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M17 4v16"));
break;
case "FolderOpen":
dc.DrawGeometry(null,pen,Geometry.Parse(@"m6 14 1.5-2.9A2 2 0 0 1 9.24 10H20a2 2 0 0 1 1.94 2.5l-1.54 6a2 2 0 0 1-1.95 1.5H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h3.9a2 2 0 0 1 1.69.9l.81 1.2a2 2 0 0 0 1.67.9H18a2 2 0 0 1 2 2v2"));
break;
case "FolderTree":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M20 10a1 1 0 0 0 1-1V6a1 1 0 0 0-1-1h-2.5a1 1 0 0 1-.8-.4l-.9-1.2A1 1 0 0 0 15 3h-2a1 1 0 0 0-1 1v5a1 1 0 0 0 1 1Z"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M20 21a1 1 0 0 0 1-1v-3a1 1 0 0 0-1-1h-2.9a1 1 0 0 1-.88-.55l-.42-.85a1 1 0 0 0-.92-.6H13a1 1 0 0 0-1 1v5a1 1 0 0 0 1 1Z"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M3 5a2 2 0 0 0 2 2h3"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M3 3v13a2 2 0 0 0 2 2h3"));
break;
case "Columns2":
dc.DrawRoundedRectangle(null,pen,new Rect(3,3,18,18),2,2);
dc.DrawGeometry(null,pen,Geometry.Parse(@"M12 3v18"));
break;
case "PanelRight":
dc.DrawRoundedRectangle(null,pen,new Rect(3,3,18,18),2,2);
dc.DrawGeometry(null,pen,Geometry.Parse(@"M15 3v18"));
break;
case "Ellipsis":
dc.DrawEllipse(null,pen,new Point(12,12),1,1);
dc.DrawEllipse(null,pen,new Point(19,12),1,1);
dc.DrawEllipse(null,pen,new Point(5,12),1,1);
break;
case "X":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M18 6 6 18"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"m6 6 12 12"));
break;
case "ChevronDown":
dc.DrawGeometry(null,pen,Geometry.Parse(@"m6 9 6 6 6-6"));
break;
case "ChevronRight":
dc.DrawGeometry(null,pen,Geometry.Parse(@"m9 18 6-6-6-6"));
break;
case "CircleCheck":
dc.DrawEllipse(null,pen,new Point(12,12),10,10);
dc.DrawGeometry(null,pen,Geometry.Parse(@"m9 12 2 2 4-4"));
break;
case "Upload":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M12 3v12"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"m17 8-5-5-5 5"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"));
break;
case "Download":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M12 15V3"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"m7 10 5 5 5-5"));
break;
case "RefreshCw":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 3v5h-5"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M8 16H3v5"));
break;
case "FileText":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M6 22a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h8a2.4 2.4 0 0 1 1.704.706l3.588 3.588A2.4 2.4 0 0 1 20 8v12a2 2 0 0 1-2 2z"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M14 2v5a1 1 0 0 0 1 1h5"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M10 9H8"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M16 13H8"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"M16 17H8"));
break;
case "Folder":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M20 20a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.9a2 2 0 0 1-1.69-.9L9.6 3.9A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2Z"));
break;
case "Terminal":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M12 19h8"));
dc.DrawGeometry(null,pen,Geometry.Parse(@"m4 17 6-6-6-6"));
break;
case "Circle":
dc.DrawEllipse(null,pen,new Point(12,12),10,10);
break;
case "Check":
dc.DrawGeometry(null,pen,Geometry.Parse(@"M20 6 9 17l-5-5"));
break;
}dc.Pop();}}
