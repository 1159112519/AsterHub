using System;
using System.Collections.Generic;
using System.Drawing;

// Each saved column is two fixed slots. Pressure only changes their presentation.
internal sealed class TaskbarSqueezeLayout {
 internal const float Start=18, MaxColumnWidth=116, GraphSpace=32, MinGap=4, MaxGap=12, FadeSpan=14, Edge=14, InkPadding=1;
 internal readonly TaskbarModule[] Modules;
 internal readonly int Columns;
 internal readonly float[] Left,Right;
 internal readonly TaskbarCellInk[] Cells;
 readonly float[] prefix;
 internal float FullWidth{get{return Width(Columns,MaxGap);}}
 internal float MinimumRoom(bool reduced){return Width(1,0)-(reduced?0:FadeSpan);}
 internal TaskbarSqueezeLayout(TaskbarModule[] modules,TaskbarCellInk[] cells=null){
  Modules=modules;Columns=modules.Length/2;Cells=cells;Left=new float[Columns];Right=new float[Columns];prefix=new float[Columns+1];
  for(int col=0;col<Columns;col++){
   bool found=false;float reserve=0;
   for(int i=col;i<modules.Length;i+=Columns){
    if(cells!=null){
     if(cells[i]==null)continue;
     if(!found){Left[col]=cells[i].Left;Right[col]=cells[i].Right;found=true;}
     else{Left[col]=Math.Min(Left[col],cells[i].Left);Right[col]=Math.Max(Right[col],cells[i].Right);}
    }else if(modules[i]!=null){int charts=0;foreach(string part in modules[i].Parts)if(part!="number")charts++;reserve=Math.Max(reserve,charts*GraphSpace);}
   }
   // Empty positions keep their column slot; the width bound is also used before a sample is available.
   if(!found)Right[col]=cells==null?MaxColumnWidth+reserve:12;
   prefix[col+1]=prefix[col]+Right[col]-Left[col];
  }
 }
 float Width(int count,float gap){return Start+Edge+prefix[count]+Math.Max(0,count-1)*gap;}
 internal TaskbarSqueezeFrame Frame(float room,bool reduced,bool recovering=false){
  int count=Columns;float gap=0,fade=0;
  while(count>0){
   float minimum=Width(count,recovering?MaxGap:MinGap);
   if(room>=minimum){gap=count<2?0:recovering?MaxGap:Math.Min(MaxGap,MinGap+(room-minimum)/(count-1));break;}
   if(!reduced&&room>minimum-FadeSpan){gap=count<2?0:recovering?MaxGap:MinGap;fade=Smooth((minimum-room)/FadeSpan);break;}
   if(!recovering&&!reduced&&count>2&&room>Width(count-1,MaxGap)){
    float end=minimum-FadeSpan,next=Width(count-1,MaxGap);
    gap=MinGap+(MaxGap-MinGap)*Smooth((end-room)/Math.Max(1,end-next));count--;break;
   }
   count--;
  }
  var frame=new TaskbarSqueezeFrame(this);
  for(int i=0;i<count;i++){
   bool leaving=i==count-1&&fade>0;
   // Constrain the left slide to free space: the whole title stays clear of its neighbour.
   float travel=i==0?7:MinGap-InkPadding;
   frame.X[i]=Start+prefix[i]+i*gap-Left[i]-(leaving?travel*fade:0);
   frame.Alpha[i]=leaving?1-fade:1;
   frame.Blur[i]=leaving?2.8f*fade:0;
   frame.Departing[i]=leaving;
  }
  return frame;
 }
 internal TaskbarSqueezeFrame CompactFrame(){return Frame(Width(Columns,MinGap),false);}
 internal bool Fits(TaskbarSqueezeFrame frame,int column,float shift,float room){
  return frame.Alpha[column]>0&&frame.X[column]+shift+Left[column]>=InkPadding&&frame.X[column]+shift+Right[column]+InkPadding<=room;
 }
 internal bool HitBounds(TaskbarSqueezeFrame frame,int slot,float shift,float room,out float left,out float right){
  left=right=0;int column=slot%Columns;var ink=Cells==null?null:Cells[slot];
  if(frame.Departing[column]||!Fits(frame,column,shift,room)||Modules[slot]==null||(Cells!=null&&ink==null))return false;
  left=Math.Max(0,frame.X[column]+shift+(ink==null?Left[column]:ink.Left)-2);
  right=Math.Min(room,frame.X[column]+shift+(ink==null?Right[column]:ink.Right)+2);
  return left<right;
 }
 internal int Hit(TaskbarSqueezeFrame frame,float shift,float x,float y,float room){
  if(x<0||x>=room||y<2||y>=36)return -1;
  int row=(int)((y-2)/17);
  for(int column=0;column<Columns;column++){
   int slot=row*Columns+column;float left,right;
   if(!HitBounds(frame,slot,shift,room,out left,out right)||x<left||x>=right)continue;
   if(Cells==null)return slot;
   float localX=x-frame.X[column]-shift,localY=y-2-row*17;
   foreach(var area in Cells[slot].HitAreas)if(area.Contains(localX,localY))return slot;
  }
  return -1;
 }
 internal static float Clamp(float t){return Math.Max(0,Math.Min(1,t));}
 internal static float Smooth(float t){t=Clamp(t);return t*t*(3-2*t);}
}
internal sealed class TaskbarCellInk {
 internal float Left=float.PositiveInfinity,Right=float.NegativeInfinity;
 internal float NumberX,NumberTrack,UnitX,LabelX;
 internal float[] GraphX;
 internal string Text;
 internal readonly List<RectangleF> HitAreas=new List<RectangleF>(5);
 internal void HitArea(float x,float top,float width,float bottom){
  if(width<=0||bottom<=top)return;
  const float padding=.5f;
  HitAreas.Add(RectangleF.FromLTRB(x-padding,Math.Max(0,top-padding),x+width+padding,Math.Min(17,bottom+padding)));
 }
 internal void Include(float x,float width){if(width>0){Left=Math.Min(Left,x);Right=Math.Max(Right,x+width);}}
}
internal sealed class TaskbarSqueezeFrame {
 internal readonly TaskbarSqueezeLayout Layout;
 internal readonly float[] X,Alpha,Blur;
 internal readonly bool[] Departing;
 internal TaskbarSqueezeFrame(TaskbarSqueezeLayout layout){Layout=layout;int n=layout.Columns;X=new float[n];Alpha=new float[n];Blur=new float[n];Departing=new bool[n];}
}
// Sensor distance is not animation time. Keep every column's visual state when Explorer
// jumps across several layout thresholds, and retarget from the currently displayed frame.
internal sealed class TaskbarSqueezeMotion {
 float from,to;long start;int duration;bool ready,recovering;
 TaskbarSqueezeFrame origin,last;
 internal void ResetVisual(){origin=last=null;recovering=false;}
 internal bool Active(long now){return duration>0&&now-start<duration;}
 internal float Value(long now){return Active(now)?from+(to-from)*(float)TaskbarPainter.Ease(Math.Max(0,now-start)/(double)duration):to;}
 internal void Set(float room,long now,bool reduced){
  if(!ready||reduced){ready=true;from=to=room;duration=0;origin=last=null;recovering=false;return;}
  if(Math.Abs(to-room)<.25f)return;
  recovering=room>to;from=Value(now);origin=last;to=room;start=now;
  duration=from==to?0:420;
 }
 internal TaskbarSqueezeFrame Frame(TaskbarSqueezeLayout layout,float room,long now,bool reduced){
  // Retain survivor spacing while restoring columns; reversing the compression curve
  // would tighten it again before each new column and visibly bounce.
  var target=layout.Frame(room,reduced,recovering);
  // A release can interrupt a retirement before the next full column fits. Preserve
  // its current visibility instead of first fading it out and bringing it back.
  if(recovering&&last!=null&&last.Layout.Columns==layout.Columns)for(int i=0;i<layout.Columns;i++)if(target.Alpha[i]<last.Alpha[i]){target.Alpha[i]=last.Alpha[i];target.X[i]=last.X[i];target.Blur[i]=last.Blur[i];target.Departing[i]=last.Departing[i];}
  if(reduced||!Active(now)||origin==null||origin.Layout.Columns!=layout.Columns)return last=target;
  float time=TaskbarSqueezeLayout.Clamp((now-start)/(float)duration);
  bool exiting=false,entering=false;
  for(int i=0;i<layout.Columns;i++){exiting|=origin.Alpha[i]>target.Alpha[i]+.001f;entering|=origin.Alpha[i]+.001f<target.Alpha[i];}
  var compact=layout.CompactFrame();var result=new TaskbarSqueezeFrame(layout);
  for(int i=0;i<layout.Columns;i++){
   float a=origin.Alpha[i],b=target.Alpha[i];
   float sourceX=a>0?origin.X[i]:target.X[i]-7;
   float targetX=b>0?target.X[i]:Math.Min(sourceX,compact.X[i])-3;
   float movement=(float)TaskbarPainter.Ease(time);
   if(!recovering&&exiting&&!entering){
    // First tighten the gaps, then retire entire columns, then settle the survivors.
    float tighten=(float)TaskbarPainter.Ease(Math.Min(1,time/.24f));
    float settle=(float)TaskbarPainter.Ease(Math.Max(0,(time-.78f)/.22f));
    float tightX=Math.Min(sourceX,compact.X[i]);
    result.X[i]=sourceX+(tightX-sourceX)*tighten+(targetX-tightX)*settle;
    movement=(float)TaskbarPainter.Ease(TaskbarSqueezeLayout.Clamp((time-.16f)/.68f));
    if(b<a)result.X[i]-=3*movement*(1-settle);
   }else result.X[i]=sourceX+(targetX-sourceX)*movement;
   result.Alpha[i]=a+(b-a)*movement;
   result.Blur[i]=2.8f*(1-result.Alpha[i]);
   result.Departing[i]=result.Alpha[i]<.9999f||target.Departing[i]||b<a;
   // Survivors must fit the new proven free area even if Explorer moved in one step.
   // Only retiring columns may be squashed by the compositor; active glyphs stay unchanged.
   if(!result.Departing[i])result.X[i]=Math.Min(result.X[i],room-layout.Right[i]-TaskbarSqueezeLayout.InkPadding);
  }
  return last=result;
 }
}

// Explorer can briefly move its first button backwards while a group closes.
// Reclaim confirmed free pixels after a short stable window; pressure is immediate.
internal sealed class TaskbarRoomStability {
 float accepted,candidate;long since;bool ready,pending;
 internal void Reset(){ready=pending=false;}
 internal float Accept(float room,long now){
  if(!ready){ready=true;accepted=room;return room;}
  if(room<accepted){accepted=room;pending=false;return room;}
  if(room<=accepted+.25f){pending=false;return accepted;}
  if(!pending){pending=true;candidate=room;since=now;}
  else if(room<candidate-.25f){candidate=room;since=now;}
  if(now-since>=160){accepted=Math.Min(room,candidate);pending=false;}
  return accepted;
 }
}
