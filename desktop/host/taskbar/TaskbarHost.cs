using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Timer=System.Windows.Forms.Timer;
internal sealed class TaskbarController:IDisposable {
 readonly object gate=new object();readonly Func<TaskbarSnapshot> read;readonly Action settings,restore;readonly Action<string,int> pageChanged;readonly Action<string,string> detail;readonly Thread thread;TaskbarSettings options;volatile bool stop;int pendingFlip;
 volatile string state="disabled",reason="";int visiblePage;
 internal TaskbarController(TaskbarSettings options,Func<TaskbarSnapshot> read,Action settings,Action restore,Action contextMenu=null,Action<string,int> pageChanged=null,Action<string,string> detail=null){this.pageChanged=pageChanged;this.detail=detail;this.options=options.Normalize();this.read=read;this.settings=contextMenu??delegate{};this.restore=restore;thread=new Thread(Run){IsBackground=true,Name="AsterHub taskbar"};thread.SetApartmentState(ApartmentState.STA);thread.Start();}
 internal void FlipPage(int delta){Interlocked.Add(ref pendingFlip,delta);}
 internal void Configure(TaskbarSettings value){lock(gate)options=value.Normalize();}
 internal object Status(){return new{state=state,reason=reason,page=visiblePage};}
 void Run(){var context=TaskbarLocator.SetThreadDpiAwarenessContext((IntPtr)(-4));try{using(var host=new TaskbarLoop(this))Application.Run(host);}catch(Exception ex){DiagnosticsLog.Write("taskbar-loop",ex);state="unavailable";reason=ex.Message;}finally{if(context!=IntPtr.Zero)TaskbarLocator.SetThreadDpiAwarenessContext(context);}}
 public void Dispose(){stop=true;if(Thread.CurrentThread!=thread)thread.Join(1500);}
 sealed class TaskbarLoop:ApplicationContext {
  readonly TaskbarController owner;readonly Timer watch=new Timer{Interval=160};readonly Stopwatch clock=Stopwatch.StartNew();readonly object probeGate=new object();TaskbarLocator.Layout pending,last;TaskbarSurface surface;volatile bool probing,disposed;long nextProbe,nextRead,nextDisplay,nextFlip;TaskbarSettings applied;int page=-1,probeGeneration,pendingGeneration;
  internal TaskbarLoop(TaskbarController owner){this.owner=owner;watch.Tick+=(s,e)=>Tick();watch.Start();}
  void Flip(int delta,TaskbarSettings options){int count=options.PageCount;if(count<2)return;page=(page+delta%count+count)%count;string active=options.PageId(page);owner.visiblePage=page;if(surface!=null)surface.SetPage(page);if(owner.pageChanged!=null)owner.pageChanged(active,options.Revision);nextFlip=clock.ElapsedMilliseconds+options.intervalSeconds*1000L;}
  void Drop(){if(surface!=null){surface.Dispose();surface=null;}last=null;}
  void Tick(){
   if(owner.stop){watch.Stop();Drop();ExitThread();return;}
   TaskbarSettings options;lock(owner.gate)options=owner.options;
   if(!options.enabled){watch.Interval=750;Drop();owner.state="disabled";owner.reason="";return;}watch.Interval=160;
   if(!ReferenceEquals(options,applied)){applied=options;nextRead=nextDisplay=0;page=options.page;probeGeneration++;lock(probeGate)pending=null;if(surface!=null)surface.Configure(options,page);nextProbe=0;nextFlip=clock.ElapsedMilliseconds+options.intervalSeconds*1000L;}
   if(surface!=null&&(!TaskbarSurface.IsWindow(surface.Handle)||TaskbarLocator.FindWindow("Shell_TrayWnd",null)!=last.Parent||!TaskbarLocator.IsWindowVisible(last.Parent)||!surface.IsAttachedTo(last.Parent)))Drop();
   TaskbarLocator.Layout proof;lock(probeGate){proof=pendingGeneration==probeGeneration?pending:null;pending=null;}
   if(proof!=null){
    if(!proof.Valid&&proof.Reason.StartsWith("discovery:")&&surface!=null){owner.reason="任务栏布局探测稍后重试";}
     else if(!proof.Valid){Drop();owner.state="waiting";owner.reason=proof.Reason.StartsWith("no verified")?"任务栏左侧空间不足；调整系统任务栏布局后会自动重试":"任务栏暂不可用；恢复后会自动连接";}
    else if(proof.Parent==TaskbarLocator.FindWindow("Shell_TrayWnd",null)){
     try{if(surface==null){surface=new TaskbarSurface(proof,options,page,delta=>Flip(delta,applied),owner.settings,owner.restore,owner.detail);}surface.Place(proof);last=proof;owner.state=surface.HasRoom?"attached":"waiting";owner.reason=surface.HasRoom?"":"任务栏左侧空间不足；空间恢复后会自动显示";owner.visiblePage=page;}
     catch(Exception ex){Drop();owner.state="waiting";owner.reason="任务栏暂不可用："+ex.Message;}
    }
   }
   if(!probing&&clock.ElapsedMilliseconds>=nextProbe){probing=true;nextProbe=clock.ElapsedMilliseconds+250;int generation=probeGeneration;ThreadPool.QueueUserWorkItem(_=>{try{var result=TaskbarLocator.Read(options);lock(probeGate)if(!disposed){pending=result;pendingGeneration=generation;}}catch(Exception ex){lock(probeGate)if(!disposed){pending=new TaskbarLocator.Layout{Reason="discovery: "+ex.GetType().Name};pendingGeneration=generation;}}finally{probing=false;}});}
   int manual=Interlocked.Exchange(ref owner.pendingFlip,0);if(surface!=null&&manual!=0)Flip(manual,options);
   if(surface!=null&&options.autoPage&&clock.ElapsedMilliseconds>=nextFlip)Flip(1,options);
   if(surface!=null)surface.AdvanceIdle();
   if(surface!=null&&clock.ElapsedMilliseconds>=nextRead){long now=clock.ElapsedMilliseconds;nextRead=now+TaskbarSettings.DefaultRefreshMs;try{var snapshot=owner.read();surface.Observe(snapshot);if(now>=nextDisplay){nextDisplay=now+Math.Max(TaskbarSettings.DefaultRefreshMs,options.refreshMs);surface.Sample(snapshot,false);}}catch(Exception ex){owner.reason="监测数据尚未就绪："+ex.Message;}}
  }
  protected override void Dispose(bool disposing){disposed=true;watch.Stop();watch.Dispose();Drop();base.Dispose(disposing);}
 }
}
internal sealed class TaskbarSurface:NativeWindow,IDisposable {
 [DllImport("user32.dll")]internal static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
 [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int hh,uint flags);
 [StructLayout(LayoutKind.Sequential)]struct MouseTrack{public uint size,flags;public IntPtr window;public uint time;}
 [DllImport("user32.dll")]static extern bool TrackMouseEvent(ref MouseTrack track);
 [StructLayout(LayoutKind.Sequential)]struct PointN{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)]struct Blend{public byte op,flags,alpha,format;}
 [DllImport("user32.dll",SetLastError=true)]static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,IntPtr pos,ref PointN size,IntPtr src,ref PointN source,uint key,ref Blend blend,uint flags);
 [DllImport("gdi32.dll")]static extern IntPtr CreateCompatibleDC(IntPtr dc);
 [DllImport("gdi32.dll")]static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr obj);
 [DllImport("gdi32.dll")]static extern bool DeleteDC(IntPtr dc);
 TaskbarHoverWheel hoverWheel;readonly Action<int> flip;readonly Action settings;readonly Action<string,string> detail;readonly TaskbarFrameClock frames;readonly Stopwatch motion=new Stopwatch();TaskbarSettings options;int page,w,h,x=-1,y=-1,wheel,hover=-1;bool dark,disposed,pageMotion;Bitmap oldPage,newPage,oldBlur,newBlur,pageBase;int direction=1;TaskbarSnapshot current,previous,queued;string fingerprint="";TaskbarPainter painter;TaskbarLocator.Layout display;readonly TaskbarHistory history=new TaskbarHistory();readonly bool interactiveDetails,reuseDetails;
 readonly Stopwatch uiClock=Stopwatch.StartNew();readonly TaskbarRailMotion rail=new TaskbarRailMotion();TaskbarMetricPopup popup,detailRenderer;
 readonly TaskbarSqueezeMotion squeeze=new TaskbarSqueezeMotion();TaskbarSqueezeLayout squeezeLayout;TaskbarSqueezeFrame paintedLayout;float dpiScale=1,paintedShift;bool shown;
 internal bool HasRoom{get{return display!=null&&Room>TaskbarSqueezeLayout.Start&&squeezeLayout!=null&&(options.reduced?Room>=squeezeLayout.MinimumRoom(true):Math.Max(Room,squeeze.Value(uiClock.ElapsedMilliseconds))>squeezeLayout.MinimumRoom(false));}}
 float Room{get{return display==null?0:display.W/dpiScale;}}
 float PresentationRoom(long now){return Math.Min(squeezeLayout.FullWidth,squeeze.Value(now));}
 TaskbarSqueezeFrame Geometry(float room){
  var frame=squeezeLayout.Frame(room,options.reduced);
  for(int col=0;col<squeezeLayout.Columns;col++)if(frame.Alpha[col]>0&&!squeezeLayout.Fits(frame,col,0,Room))return squeezeLayout.Frame(Room,options.reduced);
  return frame;
 }
 void ResetPageGeometry(){squeezeLayout=painter==null||current==null?options.SqueezePage(page):painter.MeasurePage(current,options,page);paintedLayout=null;}

 internal TaskbarSurface(TaskbarLocator.Layout layout,TaskbarSettings options,int page,Action<int> flip,Action settings,Action restore,Action<string,string> detail=null,bool interactiveDetails=true,bool? reuseDetails=null){this.interactiveDetails=interactiveDetails;this.reuseDetails=reuseDetails??interactiveDetails;this.flip=flip;this.detail=detail;this.settings=settings;this.options=options;this.page=page;
  IntPtr previousContext=TaskbarLocator.SetThreadDpiAwarenessContext(TaskbarLocator.GetWindowDpiAwarenessContext(layout.Parent));try{CreateHandle(new CreateParams{Caption="AsterHub Taskbar Monitor",Parent=layout.Parent,Style=0x44000000,ExStyle=0x08080000,X=layout.X,Y=layout.Y,Width=Math.Max(1,layout.W),Height=layout.H});}finally{TaskbarLocator.SetThreadDpiAwarenessContext(previousContext);}
  if(GetParent(Handle)!=layout.Parent)throw new InvalidOperationException("任务栏父窗口校验失败");frames=new TaskbarFrameClock(Handle);hoverWheel=new TaskbarHoverWheel(Handle,layout.Parent);
  QueueDetailWarmup();
 }
 internal bool IsAttachedTo(IntPtr parent){return !disposed&&IsWindow(Handle)&&GetParent(Handle)==parent;}
 static bool Dark(){return !TaskbarSettings.SystemLight();}
 internal void Configure(TaskbarSettings value,int selected){CloseDetail();if(value.PageCount<2)rail.Reset();if(options.deviceId!=value.deviceId){history.Clear();latestDetail=null;}options=value;QueueDetailWarmup();page=selected;hover=-1;fingerprint="";previous=null;current=null;queued=null;ResetPageGeometry();pageMotion=false;waitingPage=-1;ClearPages();frames.Stop();motion.Reset();}
 internal void Place(TaskbarLocator.Layout layout){
  bool theme=Dark();float scale=layout.Scale>0?layout.Scale:layout.H/38f;long now=uiClock.ElapsedMilliseconds;
  bool first=display==null,resize=first||w!=Math.Max(1,layout.W)||h!=layout.H||dpiScale!=scale;
  bool rebuild=first||h!=layout.H||dark!=theme||dpiScale!=scale;
  if(squeezeLayout==null)ResetPageGeometry();
  if(!resize&&!rebuild&&x==layout.X&&y==layout.Y){display=layout;squeeze.Set(Room,now,options.reduced);if(shown==HasRoom)return;ApplyWindowPosition();if(shown)Present(1);return;}
  CloseDetail();hover=-1;
  if(pageMotion&&(resize||rebuild)){
   pageMotion=false;motion.Reset();ClearPages();
   if(waitingPage>=0){page=waitingPage;waitingPage=-1;ResetPageGeometry();}
   if(queued!=null){current=queued;queued=null;fingerprint=Key(current);ResetPageGeometry();}previous=null;
  }
  display=layout;x=layout.X;y=layout.Y;w=Math.Max(1,layout.W);h=layout.H;dark=theme;dpiScale=scale;
  squeeze.Set(Room,now,options.reduced||rebuild);
  if(rebuild){if(painter!=null)painter.Dispose();painter=new TaskbarPainter(w,h,dark){History=history};ResetPageGeometry();}
  else {painter.Resize(w,h);if(current!=null&&squeezeLayout.Cells==null)ResetPageGeometry();}
  // Only the proven free pixels are assigned to the HWND, including while the outgoing lane is in flight.
  ApplyWindowPosition();
  if(shown){Present(motion.IsRunning?TaskbarPainter.Ease(Math.Min(1,motion.Elapsed.TotalMilliseconds/MotionDuration)):1);if(squeeze.Active(now)||OtherMoving()||motion.IsRunning)frames.Start();}
  else if(!motion.IsRunning)frames.Stop();
 }
 void ApplyWindowPosition(){
  bool visible=HasRoom;
  if(!SetWindowPos(Handle,IntPtr.Zero,x,y,w,h,visible?0x50u:0x90u))throw new InvalidOperationException("任务栏位置更新失败");
  shown=visible;
 }
 string Key(TaskbarSnapshot sample){var values=options.pages[page];string key=page+"/"+options.slots+"/"+history.Version;foreach(var id in values){var v=sample.Read(id).GetDisplay();key+="/"+id+":"+v.Text+":"+v.Unit;}return key;}
 TaskbarSnapshot latestDetail;
 internal void Observe(TaskbarSnapshot sample){latestDetail=sample;history.Observe(sample);if(popup!=null&&!popup.IsDisposed)popup.RefreshMetric(sample,history);}
 internal void Sample(TaskbarSnapshot sample,bool observe=true){if(observe)Observe(sample);history.Add(sample,false);string key=Key(sample);if(motion.IsRunning){queued=sample;return;}if(key==fingerprint)return;Apply(sample,key);}
 void Apply(TaskbarSnapshot sample,string key){previous=current;current=sample;fingerprint=key;ResetPageGeometry();if(display!=null&&shown!=HasRoom)ApplyWindowPosition();bool animate=false;if(HasRoom&&previous!=null&&options.motion&&!options.reduced&&options.numberMotion!="direct")foreach(string id in options.pages[page]){var next=current.Read(id);var old=previous.Read(id);if(next.animate&&!next.stale&&!old.stale&&next.unit==old.unit&&next.Text!=old.Text)animate=true;}
  if(!options.reduced&&ChartsMoving())frames.Start();
  if(animate){pageMotion=false;motion.Restart();frames.Start();Present(0);}else{previous=null;Present(1);}
 }
 void ClearPages(){foreach(var image in new[]{oldPage,newPage,oldBlur,newBlur,pageBase})if(image!=null)image.Dispose();oldPage=newPage=oldBlur=newBlur=pageBase=null;}
 int waitingPage=-1;
 internal void SetPage(int value){if(value==page)return;CloseDetail();ShowRail();Bitmap outgoing=null;if(pageMotion&&motion.IsRunning&&painter!=null&&current!=null){double progress=TaskbarPainter.Ease(Math.Min(1,motion.Elapsed.TotalMilliseconds/420));outgoing=RenderFrame(progress);}
  waitingPage=-1;ClearPages();direction=(value-page+options.PageCount)%options.PageCount==1?1:-1;
  oldPage=outgoing??(painter==null||current==null||!HasRoom||squeeze.Active(uiClock.ElapsedMilliseconds)?null:NeutralPage());page=value;ResetPageGeometry();hover=-1;fingerprint=current==null?"":Key(current);previous=null;queued=null;
  if(!options.reduced&&oldPage!=null){newPage=NeutralPage();oldBlur=TaskbarPainter.BlurPage(oldPage);newBlur=TaskbarPainter.BlurPage(newPage);pageBase=PaintLive(1,Geometry(PresentationRoom(uiClock.ElapsedMilliseconds)),0,true,false);pageMotion=true;motion.Restart();frames.Start();Present(0);}else {pageMotion=false;motion.Stop();if(!OtherMoving())frames.Stop();Present(1);ClearPages();}
 }
 bool ChartsMoving(){if(!HasRoom||squeezeLayout==null||options.reduced)return false;var frame=Geometry(PresentationRoom(uiClock.ElapsedMilliseconds));for(int i=0;i<squeezeLayout.Modules.Length;i++){var m=squeezeLayout.Modules[i];if(frame.Alpha[i%squeezeLayout.Columns]>0&&m!=null&&m.Parts.Any(p=>p!="number")&&history.Moving(m.Metric))return true;}return false;}
 Bitmap PaintLive(double progress,TaskbarSqueezeFrame frame,float shift,bool chrome=true,bool content=true){painter.HoverSlot=hover;return painter.RenderSqueeze(current,previous,options,page,progress,frame,dpiScale,shift,chrome,content);}
 Bitmap NeutralPage(){return PaintLive(1,Geometry(PresentationRoom(uiClock.ElapsedMilliseconds)),0,false);}
 double RailProgress{get{return options.PageCount>1?rail.Progress:0;}}
 float RailShift{get{return -12*(float)(1-RailProgress);}}
 void ShowRail(){if(options.PageCount<2)return;rail.Reveal(uiClock.ElapsedMilliseconds,options.reduced);if(!options.reduced)frames.Start();else Present(1);}
 internal void AdvanceIdle(){
  long now=uiClock.ElapsedMilliseconds;
  // Sampling/idle ticks also settle overdue digit frames if the high-rate message clock stalls.
  if(motion.IsRunning&&motion.Elapsed.TotalMilliseconds>=MotionDuration)Frame();
  if(squeeze.Active(now)&&HasRoom)frames.Start();
  if(options.PageCount<2||!HasRoom)return;
  if(rail.Active(now)){if(options.reduced){rail.Update(now,true);Present(1);}else frames.Start();}
 }
 bool OtherMoving(){long now=uiClock.ElapsedMilliseconds;return HasRoom&&(squeeze.Active(now)||ChartsMoving()||options.PageCount>1&&rail.Active(now));}
 int MotionDuration{get{return pageMotion?420:240;}}
 void Frame(){
  long now=uiClock.ElapsedMilliseconds;rail.Update(now,options.reduced);
   if(display!=null&&shown&&!HasRoom){ApplyWindowPosition();frames.Stop();motion.Reset();return;}
  if(!motion.IsRunning){Present(1);if(!OtherMoving())frames.Stop();return;}
  double progress=motion.Elapsed.TotalMilliseconds/MotionDuration;
  if(progress<1){Present(TaskbarPainter.Ease(progress));return;}
  // Render the terminal frame with settled glyph geometry before accepting another sample.
  motion.Stop();previous=null;pageMotion=false;ClearPages();
  Present(1);if(!OtherMoving())frames.Stop();
  if(waitingPage>=0){int nextPage=waitingPage;waitingPage=-1;SetPage(nextPage);return;}
  if(queued!=null){var next=queued;queued=null;Sample(next);}
 }
 internal event Action<long,double> FramePresented;
 Bitmap RenderFrame(double progress){
  var frame=Geometry(PresentationRoom(uiClock.ElapsedMilliseconds));float shift=RailShift;
  if(hover>=0&&(frame.Departing[hover%frame.Layout.Columns]||!frame.Layout.Fits(frame,hover%frame.Layout.Columns,shift,Room)))hover=-1;
  bool slide=pageMotion&&progress<1&&pageBase!=null&&oldPage!=null;
  var bitmap=slide?(Bitmap)pageBase.Clone():PaintLive(progress,frame,shift);
  using(var g=Graphics.FromImage(bitmap)){
   if(slide){double blur=Math.Sin(progress*Math.PI);float travel=w*.14f,dx=shift*dpiScale;float oldX=dx+(float)(-direction*travel*progress),newX=dx+(float)(direction*travel*(1-progress));TaskbarPainter.Layer(g,oldPage,oldX,(1-progress)*(1-blur));TaskbarPainter.Layer(g,oldBlur,oldX,(1-progress)*blur);TaskbarPainter.Layer(g,newPage,newX,progress*(1-blur));TaskbarPainter.Layer(g,newBlur,newX,progress*blur);}
   painter.DrawPageIndicator(g,page,options.PageCount,dpiScale,RailProgress*frame.Alpha[0]);
   // Alpha-zero pixels hit Explorer. Fill only visible components, preserving the gaps.
   if(options.clickDetails&&!slide&&frame.Layout.Cells!=null){
    using(var hitBrush=new SolidBrush(Color.FromArgb(1,0,0,0))){
     float sy=h/38f;
     for(int i=0;i<frame.Layout.Modules.Length;i++){
      float left,right;if(!frame.Layout.HitBounds(frame,i,shift,Room,out left,out right))continue;
      float x=frame.X[i%frame.Layout.Columns]+shift,top=2+17*(i/frame.Layout.Columns);
      foreach(var area in frame.Layout.Cells[i].HitAreas){
       int x0=(int)Math.Ceiling(Math.Max(left,x+area.Left)*dpiScale),x1=(int)Math.Ceiling(Math.Min(right,x+area.Right)*dpiScale);
       int y0=(int)Math.Ceiling((top+area.Top)*sy),y1=(int)Math.Ceiling((top+area.Bottom)*sy);
       if(x1>x0&&y1>y0)g.FillRectangle(hitBrush,x0,y0,x1-x0,y1-y0);
      }
     }
    }
   }
  }
  paintedLayout=frame;paintedShift=shift;return bitmap;
 }
 void Present(double progress){long started=Stopwatch.GetTimestamp();if(painter==null||current==null||!HasRoom||!IsWindow(Handle))return;using(var bitmap=RenderFrame(progress)){
   IntPtr dc=CreateCompatibleDC(IntPtr.Zero),handle=bitmap.GetHbitmap(Color.FromArgb(0)),saved=SelectObject(dc,handle);try{var size=new PointN{X=w,Y=h};var source=new PointN();var blend=new Blend{alpha=255,format=1};if(!UpdateLayeredWindow(Handle,IntPtr.Zero,IntPtr.Zero,ref size,dc,ref source,0,ref blend,2))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"任务栏绘制失败");}finally{SelectObject(dc,saved);DeleteObject(handle);DeleteDC(dc);}}
  var observer=FramePresented;if(observer!=null)observer(Stopwatch.GetTimestamp(),(Stopwatch.GetTimestamp()-started)*1000.0/Stopwatch.Frequency);
 }
 int HitSlot(IntPtr point){
  if(!HasRoom||paintedLayout==null||pageMotion||paintedLayout.Layout.Cells==null)return -1;
  float px=(short)(point.ToInt64()&65535)/dpiScale,py=(short)((point.ToInt64()>>16)&65535)*38f/h;
  return paintedLayout.Layout.Hit(paintedLayout,paintedShift,px,py,Room);
 }
 bool HitPageIndicator(IntPtr point){
  if(paintedLayout==null||pageMotion||options.PageCount<2)return false;
  double alpha=RailProgress*paintedLayout.Alpha[0];if(alpha<.25)return false;
  float px=(short)(point.ToInt64()&65535)/dpiScale,py=(short)((point.ToInt64()>>16)&65535)*38f/h;
  float cx=7.5f+(float)(5*(1-alpha)),size=(float)(.55+.45*alpha);
  for(int i=0;i<options.PageCount;i++){
   float cy=19+(i-(options.PageCount-1)/2f)*6,dx=(px-cx)/((i==page?1.5f:1)*size),dy=(py-cy)/((i==page?2f:1)*size);
   if(dx*dx+dy*dy<=1)return true;
  }
  return false;
 }
 void Hover(int at){if(!options.clickDetails)at=-1;if(hover==at)return;hover=at;if(!motion.IsRunning)Present(1);}
 void CloseDetail(){if(popup==null)return;var old=popup;popup=null;if(!old.IsDisposed)old.Close();}
 void ShowDetail(int at){
  if(!options.clickDetails)return;
  var frame=paintedLayout;if(frame==null||frame.Layout.Cells==null||frame.Layout.Cells[at]==null)return;var module=frame.Layout.Modules[at];if(module==null)return;
  string metric=module.Metric,device=options.deviceId;
  string primary=TaskbarMetricPopup.PrimaryMetric(metric);
  if(popup!=null&&popup.Metric==primary){if(popup.TriggerMetric==metric)CloseDetail();else popup.TriggerMetric=metric;return;}CloseDetail();
  int cols=frame.Layout.Columns;float sx=dpiScale,sy=h/38f;var cell=frame.Layout.Cells[at];
  var point=new PointN{X=(int)Math.Round((frame.X[at%cols]+paintedShift+cell.Left)*sx),Y=(int)Math.Round((2+at/cols*17)*sy)};
  if(!ClientToScreen(Handle,ref point))return;
  var anchor=new Rectangle(point.X,point.Y,(int)Math.Round((cell.Right-cell.Left)*sx),(int)Math.Round(17*sy));
  var widget=new PointN();if(!ClientToScreen(Handle,ref widget))return;
  try{
   float dpi=TaskbarLocator.GetDpiForWindow(Handle)/96f;var bounds=new Rectangle(widget.X,widget.Y,w,h);Action open=()=>{if(detail!=null)detail(primary,device);};
   var opened=reuseDetails?PrepareDetail():null;bool detailDark=NativeWindowTheme.TaskbarDetailDark;
   if(opened!=null)opened.Begin(metric,anchor,bounds,detailDark,dpi,open);
   else{opened=new TaskbarMetricPopup(metric,anchor,bounds,detailDark,dpi,open,interactiveDetails);opened.FormClosed+=DetailClosed;}
   popup=opened;opened.RefreshMetric(latestDetail??current,history);opened.Show();
  }catch(Exception ex){DiagnosticsLog.Write("taskbar-detail",ex);CloseDetail();}
 }
 void QueueDetailWarmup(){
  if(!reuseDetails||!options.clickDetails||detailRenderer!=null)return;
  System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{if(!disposed&&options.clickDetails)PrepareDetail();}));
 }
 TaskbarMetricPopup PrepareDetail(){
  if(detailRenderer!=null&&!detailRenderer.IsDisposed)return detailRenderer;
  try{detailRenderer=TaskbarMetricPopup.Prepare(interactiveDetails);detailRenderer.FormClosed+=DetailClosed;return detailRenderer;}
  catch(Exception ex){DiagnosticsLog.Write("taskbar-detail-warmup",ex);return null;}
 }
 void DetailClosed(object sender,EventArgs e){if(ReferenceEquals(popup,sender))popup=null;}

 [DllImport("user32.dll")]static extern bool ClientToScreen(IntPtr h,ref PointN point);
 long lastContextRequest;
  protected override void WndProc(ref Message m){if(m.Msg==TaskbarFrameClock.Message){try{if(!disposed)Frame();}finally{frames.Acknowledge();}return;}if(m.Msg==0x0200){if(!options.clickDetails)return;int at=HitSlot(m.LParam);if(at>=0&&hover<0){var track=new MouseTrack{size=(uint)Marshal.SizeOf(typeof(MouseTrack)),flags=2,window=Handle};TrackMouseEvent(ref track);}Hover(at);return;}if(m.Msg==0x02A3){Hover(-1);return;}if(m.Msg==0x21){m.Result=(IntPtr)3;return;}if(m.Msg==0x14){m.Result=(IntPtr)1;return;}if(m.Msg==0x020A||m.Msg==TaskbarHoverWheel.Message){wheel+=m.Msg==TaskbarHoverWheel.Message?m.WParam.ToInt32():(short)((m.WParam.ToInt64()>>16)&65535);int steps=wheel/120;if(steps!=0){wheel-=steps*120;ShowRail();flip(-steps);}return;}if(m.Msg==0x0204){lastContextRequest=0;m.Result=IntPtr.Zero;return;}if(m.Msg==0x0205||m.Msg==0x007B||m.Msg==TaskbarHoverWheel.ContextMessage){long now=Stopwatch.GetTimestamp();if(m.Msg!=0x007B||m.LParam.ToInt64()==-1||lastContextRequest==0||(now-lastContextRequest)/(double)Stopwatch.Frequency>.25){lastContextRequest=now;CloseDetail();settings();}m.Result=IntPtr.Zero;return;}if(m.Msg==0x0202){int at=HitSlot(m.LParam);if(at>=0){ShowDetail(at);return;}if(HitPageIndicator(m.LParam))flip(1);return;}base.WndProc(ref m);}
 public void Dispose(){if(disposed)return;disposed=true;CloseDetail();if(detailRenderer!=null){detailRenderer.Dispose();detailRenderer=null;}if(hoverWheel!=null)hoverWheel.Dispose();frames.Stop();frames.Dispose();ClearPages();if(painter!=null)painter.Dispose();if(Handle!=IntPtr.Zero&&IsWindow(Handle))DestroyHandle();}
}
