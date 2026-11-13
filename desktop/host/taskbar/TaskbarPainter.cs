using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
internal sealed class TaskbarPainter:IDisposable {
 const int S=4;int width,height;readonly bool dark;readonly PrivateFontCollection latin=new PrivateFontCollection(),medium=new PrivateFontCollection(),han=new PrivateFontCollection();readonly Dictionary<string,Bitmap> glyphs=new Dictionary<string,Bitmap>();readonly Dictionary<Bitmap,int> inkTop=new Dictionary<Bitmap,int>();readonly Dictionary<Font,int> referenceBottom=new Dictionary<Font,int>();
 readonly Dictionary<Tuple<char,float,int>,DigitInk> digitInk=new Dictionary<Tuple<char,float,int>,DigitInk>();
 readonly List<DigitDraw> digitQueue=new List<DigitDraw>(32);
 sealed class DigitInk {internal Bitmap Image;internal int Foot,Offset;}
 struct DigitDraw {internal DigitInk Ink;internal int X,Y;internal double Opacity;internal Rectangle Clip;internal Color? Tint;}
 float renderScaleX;
 internal int HoverSlot=-1,DisplaySlots=0,CellWidth=TaskbarSettings.CellWidth;internal bool Compact=false;internal float ContentShift=0;internal TaskbarHistory History;
 readonly Color labelColor,valueColor,accent,unitColor;readonly Font labelFont,valueFont,hanFont,unitFont;
 internal TaskbarPainter(int width,int height,bool dark,string resourceRoot=null){this.width=width;this.height=height;this.dark=dark;string root=resourceRoot??AppDomain.CurrentDomain.BaseDirectory;latin.AddFontFile(Path.Combine(root,"resources/fonts/TaskbarInter-Regular.ttf"));medium.AddFontFile(Path.Combine(root,"resources/fonts/TaskbarInter-Medium.ttf"));han.AddFontFile(Path.Combine(root,"resources/workbench/fonts/harmonyos-sans-sc-regular.ttf"));labelFont=new Font(latin.Families[0],11.5f*S,FontStyle.Regular,GraphicsUnit.Pixel);valueFont=new Font(medium.Families[0],12.5f*S,FontStyle.Regular,GraphicsUnit.Pixel);hanFont=new Font(han.Families[0],11.5f*S,FontStyle.Regular,GraphicsUnit.Pixel);unitFont=new Font(latin.Families[0],10.5f*S,FontStyle.Regular,GraphicsUnit.Pixel);labelColor=dark?Color.FromArgb(225,226,230):Color.FromArgb(71,99,117);valueColor=dark?Color.FromArgb(225,226,230):Color.FromArgb(31,55,73);unitColor=dark?Color.FromArgb(179,181,188):Color.FromArgb(71,99,117);accent=dark?Color.FromArgb(130,215,233):Color.FromArgb(25,131,156);}
 internal static double Ease(double p){if(p<=0||p>=1)return p;double l=0,r=1,t=0;for(int i=0;i<18;i++){t=(l+r)/2;double x=3*(1-t)*(1-t)*t*.22+3*(1-t)*t*t*.28+t*t*t;if(x<p)l=t;else r=t;}return 3*(1-t)*(1-t)*t*.74+3*(1-t)*t*t+t*t*t;}
 Bitmap Ink(string text,Font font,Color color){string key=text+"/"+font.FontFamily.Name+"/"+font.Size+"/"+color.ToArgb();Bitmap cached;if(glyphs.TryGetValue(key,out cached))return cached;if(glyphs.Count>=192){foreach(var v in glyphs.Values)v.Dispose();glyphs.Clear();inkTop.Clear();}
  using(var source=new Bitmap(Math.Max(40,(int)(text.Length*font.Size+24)),90,PixelFormat.Format32bppArgb)){
   using(var g=Graphics.FromImage(source)){g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;using(var b=new SolidBrush(color))g.DrawString(text,font,b,4,2,StringFormat.GenericTypographic);}
   int left=source.Width,top=source.Height,right=0,bottom=0;var rect=new Rectangle(0,0,source.Width,source.Height);var data=source.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var pixels=new byte[source.Width*source.Height*4];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);source.UnlockBits(data);
   for(int y=0;y<source.Height;y++)for(int x=0;x<source.Width;x++)if(pixels[(y*source.Width+x)*4+3]>0){left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x+1);bottom=Math.Max(bottom,y+1);}
   if(right<=left){left=top=0;right=1;bottom=1;}
   cached=new Bitmap(right-left,bottom-top,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(cached))g.DrawImage(source,new Rectangle(0,0,right-left,bottom-top),new Rectangle(left,top,right-left,bottom-top),GraphicsUnit.Pixel);
   inkTop[cached]=top;
  }
  glyphs[key]=cached;return cached;
 }
 static void Blur(Bitmap bitmap,int radius,Color color){int w=bitmap.Width,h=bitmap.Height;var d=bitmap.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppArgb);var bytes=new byte[w*h*4];Marshal.Copy(d.Scan0,bytes,0,bytes.Length);var a=new int[w*h];var b=new int[a.Length];for(int i=0;i<a.Length;i++)a[i]=bytes[i*4+3];int size=radius*2+1;
  for(int pass=0;pass<3;pass++){
   for(int y=0;y<h;y++){int sum=0;for(int x=-radius;x<=radius;x++)if(x>=0&&x<w)sum+=a[y*w+x];for(int x=0;x<w;x++){b[y*w+x]=sum/size;int add=x+radius+1,remove=x-radius;if(add<w)sum+=a[y*w+add];if(remove>=0)sum-=a[y*w+remove];}}
   for(int x=0;x<w;x++){int sum=0;for(int y=-radius;y<=radius;y++)if(y>=0&&y<h)sum+=b[y*w+x];for(int y=0;y<h;y++){a[y*w+x]=sum/size;int add=y+radius+1,remove=y-radius;if(add<h)sum+=b[add*w+x];if(remove>=0)sum-=b[remove*w+x];}}
  }
  for(int i=0;i<a.Length;i++){bytes[i*4]=color.B;bytes[i*4+1]=color.G;bytes[i*4+2]=color.R;bytes[i*4+3]=(byte)a[i];}Marshal.Copy(bytes,0,d.Scan0,bytes.Length);bitmap.UnlockBits(d);
 }
 int ReferenceBottom(Font font){int bottom;if(referenceBottom.TryGetValue(font,out bottom))return bottom;
  using(var source=new Bitmap(400,90,PixelFormat.Format32bppArgb)){using(var graphics=Graphics.FromImage(source)){graphics.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;using(var brush=new SolidBrush(Color.White))graphics.DrawString(font==hanFont?"内存":"0123456789",font,brush,4,2,StringFormat.GenericTypographic);}
   bottom=0;var data=source.LockBits(new Rectangle(0,0,source.Width,source.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);var pixels=new byte[source.Width*source.Height*4];Marshal.Copy(data.Scan0,pixels,0,pixels.Length);source.UnlockBits(data);for(int y=source.Height-1;y>=0&&bottom==0;y--)for(int x=0;x<source.Width;x++)if(pixels[(y*source.Width+x)*4+3]>0){bottom=y+1;break;}
  }referenceBottom[font]=bottom;return bottom;
 }
 void Put(Graphics g,string text,Font font,Color color,float x,float bottom){if(string.IsNullOrEmpty(text))return;var align=Ink(text.Replace("/",""),font,color);int foot=inkTop[align]+align.Height;var ink=Ink(text,font,color);float y=bottom+(inkTop[ink]-foot)/(float)S;g.DrawImage(ink,new RectangleF(x,y,ink.Width/(float)S,ink.Height/(float)S));}
 // Rasterize each digit once in output pixels. Anchor the last visible ink row,
 // then composite at integer coordinates after the rest of the surface is downsampled.
 // A change in X, glyph shape, or animation mode cannot resample its resting Y.
 DigitInk PhysicalDigit(char glyph,int blur){
  var key=Tuple.Create(glyph,renderScaleX,blur);DigitInk cached;if(digitInk.TryGetValue(key,out cached))return cached;
  var source=Ink(glyph.ToString(),valueFont,valueColor);float sy=height/38f;
  if(blur>0){
   var rest=PhysicalDigit(glyph,0);const int pad=18;int px=(int)Math.Round(pad*renderScaleX/S),py=(int)Math.Round(pad*sy/S);
   using(var padded=new Bitmap(source.Width+2*pad,source.Height+2*pad,PixelFormat.Format32bppArgb)){
    using(var g=Graphics.FromImage(padded))g.DrawImageUnscaled(source,pad,pad);
    Blur(padded,blur==2?5:2,valueColor);
    var softened=new Bitmap(rest.Image.Width+2*px,(int)Math.Round(source.Height*sy/S)+2*py,PixelFormat.Format32bppPArgb);
    using(var g=Graphics.FromImage(softened)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(padded,new Rectangle(0,0,softened.Width,softened.Height),0,0,padded.Width,padded.Height,GraphicsUnit.Pixel);}
    return digitInk[key]=new DigitInk{Image=softened,Foot=rest.Foot+py,Offset=rest.Offset};
   }
  }
  int w=Math.Max(1,(int)Math.Round(source.Width*renderScaleX/S)),h=Math.Max(1,(int)Math.Round(source.Height*sy/S));
  var image=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
  using(var g=Graphics.FromImage(image)){g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(source,new Rectangle(0,0,w,h),0,0,source.Width,source.Height,GraphicsUnit.Pixel);}
  int foot=0,faint=0;for(int y=0;y<h;y++)for(int x=0;x<w;x++){int alpha=image.GetPixel(x,y).A;if(alpha>0)faint=y+1;if(alpha>100)foot=y+1;}
  if(foot==0)foot=Math.Max(1,faint);
  bool numeric=glyph>='0'&&glyph<='9'||glyph=='.';
  int offset=numeric?0:(int)Math.Round((inkTop[source]+source.Height-ReferenceBottom(valueFont))*sy/S);
  if(foot<h){var cropped=image.Clone(new Rectangle(0,0,w,foot),PixelFormat.Format32bppPArgb);image.Dispose();image=cropped;}
  digitInk[key]=cached=new DigitInk{Image=image,Foot=foot,Offset=offset};return cached;
 }
 void Digit(char glyph,float x,float bottom,double opacity,float displacement,Rectangle clip,int blur=0,Color? tint=null){
  if(opacity<=0||clip.Width<=0||clip.Height<=0)return;var ink=PhysicalDigit(glyph,blur);float sy=height/38f;
  int left=(int)Math.Round(x*renderScaleX+(Advance(glyph)*renderScaleX-ink.Image.Width)/2f);
  int foot=(int)Math.Round(bottom*sy)+ink.Offset;
  // Displacement is relative to the same anchored position and never saved in the glyph cache.
  digitQueue.Add(new DigitDraw{Ink=ink,X=left,Y=foot-ink.Foot+(int)Math.Round(displacement*sy),Opacity=opacity,Clip=clip,Tint=tint});
 }
 void CompositeDigits(Graphics g){
  g.InterpolationMode=InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.Half;
  using(var alpha=new ImageAttributes())foreach(var item in digitQueue){
   var state=g.Save();g.SetClip(item.Clip,CombineMode.Intersect);var image=item.Ink.Image;
   if(item.Opacity>=1&&!item.Tint.HasValue)g.DrawImageUnscaled(image,item.X,item.Y);
   else{var matrix=new ColorMatrix();matrix.Matrix33=(float)item.Opacity;
    // Tint the existing alpha mask, including the blurred transition. Keep cache keys,
    // physical glyph anchor and hit geometry independent from the user's color.
    if(item.Tint.HasValue){var tint=item.Tint.Value;matrix.Matrix00=matrix.Matrix11=matrix.Matrix22=0;matrix.Matrix40=tint.R/255f;matrix.Matrix41=tint.G/255f;matrix.Matrix42=tint.B/255f;}
    alpha.SetColorMatrix(matrix);g.DrawImage(image,new Rectangle(item.X,item.Y,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,alpha);}
   g.Restore(state);
  }
 }
 internal void Resize(int w,int h){width=w;height=h;}
 internal Bitmap Render(TaskbarSnapshot sample,TaskbarSnapshot previous,TaskbarSettings options,int page,double progress,bool chrome=true,bool content=true){
  int slots=DisplaySlots==0?options.MaxSlots:DisplaySlots;
  return RenderCore(sample,previous,options,progress,chrome,content,options.Modules(page,slots,Compact),width/(float)TaskbarSettings.LogicalWidth(slots,CellWidth),ContentShift,null,-1);
 }
 void TextHit(TaskbarCellInk cell,string text,Font font,Color color,float x,float bottom){
  if(string.IsNullOrEmpty(text))return;
  var ink=Ink(text,font,color);var align=Ink(text.Replace("/",""),font,color);
  float top=bottom+(inkTop[ink]-inkTop[align]-align.Height)/(float)S;
  cell.HitArea(x,top,ink.Width/(float)S,top+ink.Height/(float)S);
 }
 internal TaskbarSqueezeLayout MeasurePage(TaskbarSnapshot sample,TaskbarSettings options,int page){
  var modules=options.Modules(page,options.Columns(page)*2,false);var cells=new TaskbarCellInk[modules.Length];
  for(int i=0;i<modules.Length;i++)if(modules[i]!=null)cells[i]=MeasureCell(sample,options,modules,i);
  return new TaskbarSqueezeLayout(modules,cells);
 }
 TaskbarCellInk MeasureCell(TaskbarSnapshot sample,TaskbarSettings options,TaskbarModule[] modules,int i){
  var module=modules[i];string id=module.Metric;var now=sample.Read(id);var display=now.GetDisplay();
  string label=module.Label(now.label??"");var parts=module.Parts;
  bool network=id=="up"||id=="down",arrow=label=="↑"||label=="↓",icon=label=="◇";
  float labelWidth=arrow?7:icon?11.5f:InkWidth(label,IsAscii(label)?labelFont:hanFont,labelColor);
  float labelTrack=arrow||icon?18:network?39:label.Length>3?34:28;
  // Use the preview tracks, extending them for longer custom labels before placing components.
  float next=Math.Max(labelTrack,labelWidth+3);
  float track=Advance('0')*(network?4:3.6f);
  var cell=new TaskbarCellInk{NumberTrack=track,LabelX=options.Offset(id,"labelX"),GraphX=new float[parts.Length],Text=""};
  cell.Include(cell.LabelX,labelWidth);float precedingRight=cell.LabelX+labelWidth;
  float labelBottom=13.5f+options.Offset(id,"label");
  if(arrow||icon)cell.HitArea(cell.LabelX,labelBottom-(arrow?8.6f:10.5f),labelWidth,labelBottom+(icon?1.5f:.6f));
  else TextHit(cell,label,IsAscii(label)?labelFont:hanFont,labelColor,cell.LabelX,labelBottom);
  for(int j=0;j<parts.Length;j++){
   if(parts[j]=="number"){
    float nudge=network?0:-Advance('0');
    // Reserve the widest fitted number before shifting it. A shorter sample must never move its unit.
    float origin=Math.Max(next,precedingRight+3-nudge);
    cell.NumberX=Math.Max(precedingRight+3,origin+nudge+options.Offset(id,"numberX"));
    cell.UnitX=Math.Max(cell.NumberX+track+2,origin+nudge+track+3+options.Offset(id,"unitX"));
    float extraShift=ReadoutLeftShift(id,cell.NumberX,track,precedingRight);
    cell.NumberX-=extraShift;cell.UnitX-=extraShift;
    cell.Text=FitNumber(display,track);
    float advance=NumberWidth(cell.Text),left=cell.NumberX+track-advance;
    float numberLeft=float.PositiveInfinity,numberRight=float.NegativeInfinity,numberTop=float.PositiveInfinity,numberFoot=float.NegativeInfinity;
    float numberBottom=13.5f+options.Offset(id,"number");
    foreach(char glyph in cell.Text){
     float step=Advance(glyph);var shape=Ink(glyph.ToString(),valueFont,valueColor);float ink=shape.Width/(float)S,inkLeft=left+(step-ink)/2;
     cell.Include(inkLeft,ink);left+=step;
     bool numeric=glyph>='0'&&glyph<='9'||glyph=='.';
     float foot=numberBottom+(numeric?0:(inkTop[shape]+shape.Height-ReferenceBottom(valueFont))/(float)S);
     numberLeft=Math.Min(numberLeft,inkLeft);numberRight=Math.Max(numberRight,inkLeft+ink);
     numberTop=Math.Min(numberTop,foot-shape.Height/(float)S);numberFoot=Math.Max(numberFoot,foot);
    }
    cell.HitArea(numberLeft,numberTop,numberRight-numberLeft,numberFoot);
    cell.Include(cell.UnitX,InkWidth(display.Unit,unitFont,unitColor));
    TextHit(cell,display.Unit,unitFont,unitColor,cell.UnitX,13.5f+options.Offset(id,"unit"));
    precedingRight=cell.UnitX+extraShift+Math.Max(16,UnitSpace(id));next=precedingRight+5;
   }else{
    float gx=Math.Max(precedingRight+3.6f,next+.75f+options.Offset(id,"graphX"));cell.GraphX[j]=gx;
    float graphY=10+options.Offset(id,"graph"),graphLeft=gx,graphWidth=22,graphTop=graphY-5.6f,graphBottom=graphY+3.6f;
    bool cores=parts[j]=="bars"&&id=="cpu"&&sample.cpuCores.Length>0;
    bool ring=parts[j]=="bars"&&(id=="gpu"||id=="memory"||id=="gpuMemory");
    bool ticks=parts[j]=="bars"&&now.unit=="%"&&id!="cpu"&&!ring;
    if(now.stale||!now.value.HasValue||double.IsNaN(now.value.Value)||double.IsInfinity(now.value.Value)||(!cores&&!ring&&!ticks&&(History==null||History.Read(id,true).Length<2))){
     // Keep the existing sizing rules, but only the dash itself is clickable.
     if(now.stale||!now.value.HasValue||parts[j]!="bars")cell.Include(gx,InkWidth("—",unitFont,labelColor));else cell.Include(gx,22);
     TextHit(cell,"—",unitFont,labelColor,gx,graphY+3);
    }else{
     if(ring){graphLeft+=3.4f;graphWidth=10.2f;graphBottom=graphY+4.6f;}
     else if(cores){graphTop=graphY-6;graphBottom=graphY+3;}
     else if(ticks){graphLeft-=.55f;graphWidth=21.8f;graphTop=graphY-2.55f;}
     cell.Include(graphLeft,graphWidth);cell.HitArea(graphLeft,graphTop,graphWidth,graphBottom);
    }
    precedingRight=gx+23;next=gx+TaskbarSqueezeLayout.GraphSpace-.75f;
   }
  }
  if(float.IsInfinity(cell.Left))cell.Include(0,1);
  return cell;
 }
 internal Bitmap RenderSqueeze(TaskbarSnapshot sample,TaskbarSnapshot previous,TaskbarSettings options,int page,double progress,TaskbarSqueezeFrame frame,float scale,float shift,bool chrome=true,bool content=true){
  var layout=frame.Layout;var modules=layout.Modules;
  var output=RenderCore(sample,previous,options,progress,chrome,content,modules,scale,shift,frame,-1);
  if(content)for(int i=0;i<layout.Columns;i++){
   if(!frame.Departing[i]||!layout.Fits(frame,i,shift,width/scale))continue;
   float x=frame.X[i]+shift+layout.Left[i],end=frame.X[i]+shift+layout.Right[i];
   using(var layer=RenderCore(sample,previous,options,progress,false,true,modules,scale,shift,frame,i)){
    int pad=(int)Math.Ceiling(frame.Blur[i]*scale*3)+2;
    var bounds=Rectangle.Intersect(new Rectangle(0,0,width,height),Rectangle.FromLTRB((int)Math.Floor(x*scale)-pad,0,(int)Math.Ceiling(end*scale)+pad,height));
    // Clip only the blurred halo, outside the departing column's own ink and clear of survivors.
    float mask=i==0?float.NegativeInfinity:(frame.X[i-1]+shift+layout.Right[i-1])*scale;
    FilterMotion(layer,bounds,frame.Blur[i]*scale,frame.Alpha[i],mask,scale);
    using(var g=Graphics.FromImage(output))g.DrawImageUnscaled(layer,0,0);
   }
  }
  return output;
 }
 Bitmap RenderCore(TaskbarSnapshot sample,TaskbarSnapshot previous,TaskbarSettings options,double progress,bool chrome,bool content,TaskbarModule[] modules,float scale,float shift,TaskbarSqueezeFrame frame,int movingColumn){
  int slots=modules.Length,cols=slots/2;renderScaleX=scale;digitQueue.Clear();
  if(digitInk.Count>=128){foreach(var item in digitInk.Values)item.Image.Dispose();digitInk.Clear();}
  // At 200% the 2x intermediate still supplies four samples per logical pixel.
  int supersample=frame!=null&&scale>=1.75f?2:S;
  using(var high=new Bitmap(width*supersample,height*supersample,PixelFormat.Format32bppPArgb)){
   using(var g=Graphics.FromImage(high)){
    g.Clear(Color.Transparent);g.ScaleTransform(scale*supersample,height*supersample/38f);g.SmoothingMode=SmoothingMode.AntiAlias;g.InterpolationMode=InterpolationMode.HighQualityBicubic;
    if(content)for(int i=0;i<modules.Length;i++){
     int col=i%cols;
     if(frame!=null&&((movingColumn<0?frame.Departing[col]:col!=movingColumn)||!frame.Layout.Fits(frame,col,shift,width/scale)))continue;
     var module=modules[i];if(module==null)continue;string id=module.Metric;
     var now=sample.Read(id);var old=previous==null?null:previous.Read(id);var label=module.Label(now.label??"");
     var cell=frame!=null&&frame.Layout.Cells!=null?frame.Layout.Cells[i]:MeasureCell(sample,options,modules,i);
     if(cell==null)continue;
     float x=(frame==null?18+col*CellWidth:frame.X[col])+shift,top=2+i/cols*17;
     float bottom=top+13.5f+options.Offset(id,"label"),numberBottom=top+13.5f+options.Offset(id,"number"),unitBottom=top+13.5f+options.Offset(id,"unit");
     var display=now.GetDisplay();var priorDisplay=old==null?display:old.GetDisplay();var parts=module.Parts;
     float left=x+cell.Left-2,right=x+cell.Right+2;
     if(chrome&&options.clickDetails&&i==HoverSlot){
      using(var path=HoverOutline(left,top+1,Math.Max(8,right-left),15))using(var tint=new SolidBrush(Color.FromArgb(dark?38:28,accent)))g.FillPath(tint,path);
     }
     var nameInk=OverrideColor(options,id,"label")??labelColor;
     var readingInk=OverrideColor(options,id,"reading");
     var graphInk=OverrideColor(options,id,"graph");
     for(int j=0;j<parts.Length;j++)if(parts[j]!="number")DrawGraph(g,parts[j],sample,now,x+cell.GraphX[j],top+10+options.Offset(id,"graph"),options.reduced,graphInk);
     float labelX=x+cell.LabelX;
     if(label=="↑"||label=="↓"){float sign=label=="↑"?-1:1,arrowY=bottom-4,tip=arrowY+sign*4;using(var pen=new Pen(nameInk,1.2f)){pen.StartCap=pen.EndCap=LineCap.Round;g.DrawLine(pen,labelX+3.5f,arrowY-sign*4,labelX+3.5f,tip);g.DrawLines(pen,new[]{new PointF(labelX+.75f,tip-sign*2.75f),new PointF(labelX+3.5f,tip),new PointF(labelX+6.25f,tip-sign*2.75f)});}}
     else if(label=="◇")DrawMetricIcon(g,id,labelX,bottom-10,nameInk);
     else Put(g,label,IsAscii(label)?labelFont:hanFont,nameInk,labelX,bottom);
     if(!parts.Contains("number"))continue;
     string text=cell.Text,prior=FitNumber(priorDisplay,cell.NumberTrack);float advance=NumberWidth(text),cx=x+cell.NumberX+cell.NumberTrack-advance;
     if(!string.IsNullOrEmpty(display.Unit))Put(g,display.Unit,unitFont,readingInk??unitColor,x+cell.UnitX,unitBottom);
     var clip=Rectangle.FromLTRB((int)Math.Floor((cx-1)*renderScaleX),(int)Math.Floor((top+.5f)*height/38f),(int)Math.Ceiling((cx+advance+1)*renderScaleX),(int)Math.Ceiling((top+16.5f)*height/38f));
     for(int at=0;at<text.Length;at++){
      char ch=text[at];float cellWidth=Advance(ch);bool moving=options.motion&&!options.reduced&&options.numberMotion!="direct"&&progress<1&&old!=null&&now.animate&&!now.stale&&!old.stale&&display.Unit==priorDisplay.Unit&&TaskbarValue.GlyphChanges(prior,text,at);
      if(moving){var column=Rectangle.Intersect(clip,Rectangle.FromLTRB((int)Math.Round(cx*renderScaleX),clip.Top,(int)Math.Round((cx+cellWidth)*renderScaleX),clip.Bottom));char was=' ';string key=TaskbarValue.PlaceKey(text,at);for(int j=0;j<prior.Length;j++)if(TaskbarValue.PlaceKey(prior,j)==key)was=prior[j];int direction=now.value<old.value?-1:1;float travel=options.numberMotion=="fade"?0:options.numberMotion=="slide"?8:14;int blurIn=options.numberMotion=="current"?(progress<.45?2:progress<.78?1:0):0,blurOut=options.numberMotion=="current"?(progress>.55?2:progress>.25?1:0):0;if(was!=' ')Digit(was,cx,numberBottom,1-progress,(float)(-direction*travel*progress),column,blurOut,readingInk);Digit(ch,cx,numberBottom,progress,(float)(direction*travel*(1-progress)),column,blurIn,readingInk);}
      else Digit(ch,cx,numberBottom,1,0,clip,0,readingInk);cx+=cellWidth;
     }
    }
   }
   var output=new Bitmap(width,height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(output)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.DrawImage(high,new Rectangle(0,0,width,height));CompositeDigits(g);}return output;
  }
 }
 // Separable Gaussian convolution of premultiplied pixels prevents colour fringes on Explorer.
 static void FilterMotion(Bitmap bitmap,Rectangle bounds,float sigma,float opacity,float mask,float maskWidth){
  if(bounds.Width<=0||bounds.Height<=0)return;
  int w=bounds.Width,h=bounds.Height,stride=w*4;
  var pixels=new byte[stride*h];
  var data=bitmap.LockBits(bounds,ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);
  try{
   for(int y=0;y<h;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*stride,stride);
   if(sigma>=.3f){
    int radius=(int)Math.Ceiling(sigma*3),diameter=radius*2+1,total=0;
    var weights=new int[diameter];
    for(int k=-radius;k<=radius;k++){int weight=(int)Math.Round(256*Math.Exp(-k*k/(2*sigma*sigma)));weights[k+radius]=weight;total+=weight;}
    var horizontal=new byte[pixels.Length];
    for(int y=0;y<h;y++)for(int x=0;x<w;x++){
     int b=0,g=0,r=0,a=0,at=y*stride+x*4,low=Math.Max(-radius,-x),high=Math.Min(radius,w-1-x);
     for(int k=low;k<=high;k++){
      int weight=weights[k+radius],p=at+k*4;
      b+=weight*pixels[p];g+=weight*pixels[p+1];r+=weight*pixels[p+2];a+=weight*pixels[p+3];
     }
     horizontal[at]=(byte)((b+total/2)/total);horizontal[at+1]=(byte)((g+total/2)/total);horizontal[at+2]=(byte)((r+total/2)/total);horizontal[at+3]=(byte)((a+total/2)/total);
    }
    for(int y=0;y<h;y++)for(int x=0;x<stride;x++){
     int sum=0;for(int k=Math.Max(-radius,-y);k<=Math.Min(radius,h-1-y);k++)sum+=weights[k+radius]*horizontal[(y+k)*stride+x];
     pixels[y*stride+x]=(byte)((sum+total/2)/total);
    }
   }
   for(int x=0;x<w;x++){
    float alpha=opacity*TaskbarSqueezeLayout.Clamp((bounds.Left+x+.5f-mask)/maskWidth);
    if(alpha<1)for(int y=0;y<h;y++)for(int c=0;c<4;c++){int i=y*stride+x*4+c;pixels[i]=(byte)Math.Round(pixels[i]*alpha);}
   }
   for(int y=0;y<h;y++)Marshal.Copy(pixels,y*stride,IntPtr.Add(data.Scan0,y*data.Stride),stride);
  }finally{bitmap.UnlockBits(data);}
 }
 static GraphicsPath HoverOutline(float x,float y,float width,float height){
  const float d=8;var p=new GraphicsPath();p.AddArc(x,y,d,d,180,90);p.AddArc(x+width-d,y,d,d,270,90);p.AddArc(x+width-d,y+height-d,d,d,0,90);p.AddArc(x,y+height-d,d,d,90,90);p.CloseFigure();return p;
 }
 internal void DrawPageIndicator(Graphics g,int page,int count,float scale,double alpha){
  if(count<2||alpha<=0)return;
  var state=g.Save();g.ScaleTransform(scale,height/38f);g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var muted=new SolidBrush(Color.FromArgb((int)(125*alpha),labelColor)))using(var active=new SolidBrush(Color.FromArgb((int)(255*alpha),accent))){
   for(int i=0;i<count;i++){float cy=19+(i-(count-1)/2f)*6,dx=(float)(5*(1-alpha)),size=(float)(.55+.45*alpha);if(i==page)g.FillEllipse(active,7.5f+dx-1.5f*size,cy-2*size,3*size,4*size);else g.FillEllipse(muted,7.5f+dx-size,cy-size,2*size,2*size);}
  }g.Restore(state);
 }
 static Color? OverrideColor(TaskbarSettings settings,string metric,string part){var rgb=settings.ColorRgb(metric,part);return rgb.HasValue?(Color?)Color.FromArgb(unchecked((int)0xff000000)|rgb.Value):null;}
 void DrawMetricIcon(Graphics g,string metric,float x,float y,Color ink){
  var state=g.Save();g.TranslateTransform(x+.5f,y);using(var p=new Pen(ink,1.05f)){
   p.StartCap=p.EndCap=LineCap.Round;p.LineJoin=LineJoin.Round;
   if(metric.EndsWith("Temperature")){g.DrawEllipse(p,3,5.5f,4,4);g.DrawLine(p,5,1,5,7.5f);g.DrawLine(p,7,2.5f,9,2.5f);}
   else if(metric.EndsWith("Frequency")||metric=="io")g.DrawLines(p,new[]{new PointF(0,6),new PointF(2,6),new PointF(4,1),new PointF(6,9),new PointF(8,4),new PointF(10,4)});
   else if(metric=="gpuPower")g.DrawPolygon(p,new[]{new PointF(6,0),new PointF(1,5),new PointF(4,5),new PointF(4,10),new PointF(9,4),new PointF(6,4)});
   else if(metric=="gpuFan"){g.DrawEllipse(p,3,3,4,4);for(int n=0;n<4;n++){g.DrawArc(p,3,0,6,6,200,130);g.TranslateTransform(5,5);g.RotateTransform(90);g.TranslateTransform(-5,-5);}}
   else if(metric=="storageRead"||metric=="storageWrite"){g.DrawLines(p,new[]{new PointF(1,7),new PointF(1,9),new PointF(9,9),new PointF(9,7)});int tip=metric=="storageRead"?6:1,tail=metric=="storageRead"?1:6;g.DrawLine(p,5,tail,5,tip);g.DrawLines(p,new[]{new PointF(3,tip+(tail-tip)*.4f),new PointF(5,tip),new PointF(7,tip+(tail-tip)*.4f)});}
   else if(metric=="ping"||metric=="loss"){g.DrawArc(p,0,2,10,9,216,108);g.DrawArc(p,2,5,6,5,216,108);g.DrawEllipse(p,4.7f,8,.6f,.6f);if(metric=="loss")g.DrawLine(p,1,9,9,1);}
   else if(metric.StartsWith("memory")){g.DrawRectangle(p,0,2,10,6);for(int k=2;k<=8;k+=3)g.DrawLine(p,k,4,k,6);}
   else if(metric=="storage"){g.DrawRectangle(p,0,1,10,8);g.DrawLine(p,0,6,10,6);g.DrawLine(p,7,7.6f,8,7.6f);}
   else{g.DrawRectangle(p,2,2,6,6);g.DrawRectangle(p,4,4,2,2);for(int k=3;k<=7;k+=4){g.DrawLine(p,k,0,k,2);g.DrawLine(p,k,8,k,10);g.DrawLine(p,0,k,2,k);g.DrawLine(p,8,k,10,k);}}
  }g.Restore(state);
 }
 void DrawGraph(Graphics g,string part,TaskbarSnapshot sample,TaskbarValue value,float x,float y,bool instant,Color? custom){
  Color ink=custom??accent,dimInk=custom??labelColor;
  if((part!="line"&&part!="bars")||value.stale||!value.value.HasValue||double.IsNaN(value.value.Value)||double.IsInfinity(value.value.Value)){Put(g,"—",unitFont,dimInk,x,y+3);return;}
  double ratio=Math.Max(0,Math.Min(1,value.value.Value/100));
  using(var pen=new Pen(ink,1.1f))using(var brush=new SolidBrush(ink)){
   pen.StartCap=pen.EndCap=LineCap.Round;
   if(part=="bars"&&value.id=="cpu"&&sample.cpuCores.Length>0){int n=Math.Min(12,sample.cpuCores.Length);for(int k=0;k<n;k++){double avg=sample.cpuCores.Skip(k*sample.cpuCores.Length/n).Take((k+1)*sample.cpuCores.Length/n-k*sample.cpuCores.Length/n).Average();float h=(float)Math.Max(1,Math.Min(9,avg*.09));g.FillRectangle(brush,x+k*22f/n,y+3-h,Math.Max(1,22f/n-1),h);}return;}
   if(part=="bars"&&(value.id=="gpu"||value.id=="memory"||value.id=="gpuMemory")){using(var dim=new Pen(Color.FromArgb(90,dimInk),1.2f))g.DrawEllipse(dim,x+4,y-5,9,9);g.DrawArc(pen,x+4,y-5,9,9,-90,(float)(360*ratio));return;}
   if(part=="bars"&&value.unit=="%"&&value.id!="cpu"){for(int k=0;k<10;k++){using(var mark=new Pen(k<ratio*10?ink:Color.FromArgb(90,dimInk),1.1f))g.DrawLine(mark,x+k*2.3f,y-2,x+k*2.3f,y+3);}return;}
   var points=History==null?new double[0]:History.Read(value.id,instant);if(points.Length<2){Put(g,"—",unitFont,dimInk,x,y+3);return;}
   float dx=2f,shift=(float)(History.Shift(value.id,instant)*dx);var graphClip=g.Save();g.SetClip(new RectangleF(x,y-5.6f,22,9.2f),CombineMode.Intersect);using(var path=new GraphicsPath()){for(int j=1;j<points.Length;j++){float x0=x+(j-1)*dx-shift,x1=x+j*dx-shift,y0=y+3-(float)points[j-1]*8,y1=y+3-(float)points[j]*8;path.AddBezier(x0,y0,x0+dx*.45f,y0,x1-dx*.45f,y1,x1,y1);}using(var edge=new LinearGradientBrush(new PointF(x,0),new PointF(x+22,0),ink,ink)){edge.InterpolationColors=new ColorBlend{Colors=new[]{Color.FromArgb(0,ink),ink,ink,Color.FromArgb(0,ink)},Positions=new[]{0f,.09f,.91f,1f}};using(var edgePen=new Pen(edge,1.1f)){edgePen.StartCap=edgePen.EndCap=LineCap.Round;g.DrawPath(edgePen,path);}}}g.Restore(graphClip);
  }
 }
 readonly Dictionary<string,float> unitSpaces=new Dictionary<string,float>();
 float UnitSpace(string id){float width;if(!unitSpaces.TryGetValue(id,out width)){width=UnitFamily(id).Max(unit=>InkWidth(unit,unitFont,labelColor));unitSpaces[id]=width;}return width;}
 static string[] UnitFamily(string id){switch(id){
  case "up":case "down":case "storageRead":case "storageWrite":return new[]{"B/s","K/s","M/s","G/s","T/s","P/s","E/s"};
  case "cpuFrequency":case "gpuFrequency":return new[]{"GHz","THz","PHz","EHz"};
  case "cpuTemperature":case "gpuTemperature":return new[]{"°C"};
  case "gpuPower":return new[]{"W","kW","MW","GW"};
  case "memoryUsed":case "memoryFree":return new[]{"GB","TB","PB","EB"};
  case "ping":return new[]{"ms"};default:return new[]{"%"};
 }}
 static bool IsPair(string a,string b){if((a=="cpuFrequency"&&b=="gpuFrequency")||(b=="cpuFrequency"&&a=="gpuFrequency"))return true;return a=="up"&&b=="down"||a=="down"&&b=="up"||a=="storageRead"&&b=="storageWrite"||a=="storageWrite"&&b=="storageRead";}
 float InkWidth(string text,Font font,Color color){return string.IsNullOrEmpty(text)?0:Ink(text,font,color).Width/(float)S;}
 internal static float Advance(char ch){return ch=='—'?13f:(ch=='.'?550f:1327f)*12.5f/2048;}
 // Use the widest fitting advance (one decimal point at most), not the current value.
 // Both anchors move together; the available space can never make the unit drift as digits change.
 internal static float ReadoutLeftShift(string id,float numberStart,float track,float previousRight){
  if(id=="up"||id=="down")return 0;
  float digit=Advance('0'),dot=Advance('.');
  float widest=Math.Max((float)Math.Floor((track+.001f)/digit)*digit,(float)Math.Floor(Math.Max(0,track-dot+.001f)/digit)*digit+dot);
  if(track>=Advance('—'))widest=Math.Max(widest,Advance('—'));
  return Math.Min(digit/2,Math.Max(0,numberStart+track-Math.Min(track,widest)-previousRight-1));
 }
 internal static float NumberWidth(string text){float width=0;foreach(char ch in text)width+=Advance(ch);return width;}
 internal static string FitNumber(TaskbarDisplay display,float available){
  if(NumberWidth(display.Text)<=available+.001f)return display.Text;
  for(int places=display.Decimals-1;places>=0;places--){string candidate=display.Value.ToString("F"+places,System.Globalization.CultureInfo.InvariantCulture);if(NumberWidth(candidate)<=available+.001f)return candidate;}
  foreach(string format in new[]{"0.#e0","0e0"}){string candidate=display.Value.ToString(format,System.Globalization.CultureInfo.InvariantCulture);if(NumberWidth(candidate)<=available+.001f)return candidate;}
  return "…"; // Never display a clipped prefix of a telemetry value as though it were complete.
 }
 // Tiny cached page surfaces: a three-pass box convolution approximates Gaussian blur.
 internal static Bitmap BlurPage(Bitmap source){var output=source.Clone(new Rectangle(0,0,source.Width,source.Height),PixelFormat.Format32bppPArgb);int w=output.Width,h=output.Height;var data=output.LockBits(new Rectangle(0,0,w,h),ImageLockMode.ReadWrite,PixelFormat.Format32bppPArgb);var a=new byte[w*h*4];var b=new byte[a.Length];Marshal.Copy(data.Scan0,a,0,a.Length);
  for(int pass=0;pass<3;pass++){for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<4;c++){int sum=0;for(int k=-1;k<=1;k++)sum+=a[(y*w+Math.Max(0,Math.Min(w-1,x+k)))*4+c];b[(y*w+x)*4+c]=(byte)(sum/3);}for(int y=0;y<h;y++)for(int x=0;x<w;x++)for(int c=0;c<4;c++){int sum=0;for(int k=-1;k<=1;k++)sum+=b[(Math.Max(0,Math.Min(h-1,y+k))*w+x)*4+c];a[(y*w+x)*4+c]=(byte)(sum/3);}}
  Marshal.Copy(a,0,data.Scan0,a.Length);output.UnlockBits(data);return output;
 }
 internal static void Layer(Graphics g,Bitmap image,float x,double alpha){if(image==null||alpha<=0)return;using(var attributes=new ImageAttributes()){var matrix=new ColorMatrix();matrix.Matrix33=(float)Math.Min(1,alpha);attributes.SetColorMatrix(matrix);g.DrawImage(image,new Rectangle((int)Math.Round(x),0,image.Width,image.Height),0,0,image.Width,image.Height,GraphicsUnit.Pixel,attributes);}}
 static bool IsAscii(string text){foreach(char c in text)if(c>127)return false;return true;}
 public void Dispose(){foreach(var value in digitInk.Values)value.Image.Dispose();digitInk.Clear();foreach(var b in glyphs.Values)b.Dispose();glyphs.Clear();labelFont.Dispose();valueFont.Dispose();hanFont.Dispose();unitFont.Dispose();latin.Dispose();medium.Dispose();han.Dispose();}
}
