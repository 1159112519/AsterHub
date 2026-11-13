using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

public sealed class TaskbarSettings {
 public static bool SystemLight(){try{return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","SystemUsesLightTheme",1))!=0;}catch{return true;}}
 public const int CellWidth=88, WideCellWidth=118, ThreeColumnTrim=10, DefaultRefreshMs=250;
 static readonly int[] AutoPageIntervals={30,60,120,300,600};
 public static int LogicalWidth(int slots,int cellWidth=CellWidth){return 18+slots/2*cellWidth;}
 public bool enabled=true,motion=true,reduced=false,background=false,autoPage=false,clickDetails=false;
 public string deviceId="local",backgroundStyle="mist";
 public int slots=4,page=0,intervalSeconds=30,refreshMs=DefaultRefreshMs;
 public string numberMotion="direct";
 // Legacy input and compatibility projection only. The layout is authoritative after migration.
 public string[][] pages=Defaults();
 public Dictionary<string,object> layout;
 public static readonly string[] Metrics={"cpu","memory","storage","gpu","down","up","cpuFrequency","gpuFrequency","gpuMemory","gpuTemperature","gpuPower","gpuFan","storageRead","storageWrite","memoryUsed","cpuTemperature","ping","loss","io"};
 public static string[][] Defaults(){return new[]{new[]{"cpu","memory","storage","gpu"},new[]{"down","up","cpuFrequency","gpuFrequency"},new[]{"gpuMemory","gpuTemperature","gpuPower","gpuFan"}};}
 internal static object Get(Dictionary<string,object> o,string key){object v;return o!=null&&o.TryGetValue(key,out v)?v:null;}
 internal static Dictionary<string,object> Obj(object v){return v as Dictionary<string,object>;}
 internal static Dictionary<string,object> Copy(object v){var d=Obj(v);return d==null?new Dictionary<string,object>():new Dictionary<string,object>(d);}
 internal static IEnumerable<object> Seq(object v){var a=v as IEnumerable;return a==null||v is string||v is IDictionary?Enumerable.Empty<object>():a.Cast<object>();}
 internal static string Str(object v){return v as string??"";}
 internal static int Int(object v,int fallback){int result;return int.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),out result)?result:fallback;}
 static string Id(object value,string fallback,HashSet<string> used){var id=Str(value);if(id.Length==0||id.Length>100||used.Contains(id))id=fallback;while(!used.Add(id))id+="-";return id;}
 internal int Revision{get{return Int(Get(layout,"revision"),0);}}
 internal Dictionary<string,object>[] LayoutPages{get{return Seq(Get(layout,"pages")).Select(Obj).ToArray();}}
 internal int PageCount{get{return LayoutPages.Length;}}
 internal string PageId(int index){var list=LayoutPages;return index>=0&&index<list.Length?Str(Get(list[index],"id")):"";}
 internal int Columns(int index){return Int(Get(Obj(Get(LayoutPages[index],"grid")),"columns"),2);}
 internal int MaxSlots{get{return Enumerable.Range(0,PageCount).Max(p=>Columns(p))*2;}}
 internal TaskbarSqueezeLayout SqueezePage(int index){return new TaskbarSqueezeLayout(Modules(index,Columns(index)*2,false));}
 internal float SqueezeWidth{get{return Enumerable.Range(0,PageCount).Max(p=>SqueezePage(p).FullWidth);}}
 internal bool HasWideCells{get{return Enumerable.Range(0,PageCount).Any(p=>Modules(p,Columns(p)*2,false).Any(m=>m!=null&&m.Parts.Length>1));}}
 internal string Key{get{return new JavaScriptSerializer().Serialize(new object[]{background,backgroundStyle,autoPage,clickDetails,intervalSeconds,refreshMs,numberMotion,deviceId,motion,reduced,Revision,Get(layout,"pages"),Get(layout,"typography"),Get(layout,"shelf"),Get(layout,"colors")});}}
 internal static int WidthFor(int columns,bool wide){return (wide?WideCellWidth:CellWidth)-(columns==3?ThreeColumnTrim:0);}
 internal static bool Fit(int requested,bool wide,double scale,int available,out int slots,out int cell,out bool compact){
  int columns=Math.Max(1,Math.Min(3,requested/2));slots=columns*2;cell=WidthFor(columns,wide);compact=false;
  Func<int,int,int> width=(n,c)=>(int)Math.Round(LogicalWidth(n,c)*scale);
  if(width(slots,cell)<=available)return true;
  compact=true;
  while(columns>=1){slots=columns*2;cell=WidthFor(columns,false);if(width(slots,cell)<=available)return true;columns--;}
  return false;
 }
 internal float Offset(string metric,string part){
  var typography=Obj(Get(layout,"typography"));var global=Obj(Get(typography,"global"));var specific=Obj(Get(Obj(Get(typography,"metrics")),metric));
  bool horizontal=part.EndsWith("X",StringComparison.Ordinal);float limit=horizontal?6:2;
  float value=ReadOffset(global,part,horizontal)+ReadOffset(specific,part,horizontal);
  return Math.Max(-limit,Math.Min(limit,value));
 }
 static float ReadOffset(Dictionary<string,object> scope,string part,bool horizontal){
  float value;
  if(!float.TryParse(Convert.ToString(Get(scope,part),CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out value)||float.IsNaN(value)||float.IsInfinity(value))return 0;
  return horizontal?(float)Math.Round(Math.Max(-6,Math.Min(6,value))*8,MidpointRounding.AwayFromZero)/8:value;
 }
 internal bool SetActive(string id){int index=Array.FindIndex(LayoutPages,p=>Str(Get(p,"id"))==id);if(index<0)return false;page=index;layout["activePageId"]=id;return true;}
 internal TaskbarModule[] Modules(int index,int displaySlots,bool hideGraphs){
  int columns=displaySlots/2;bool single=Columns(index)==3;var result=new TaskbarModule[displaySlots];
  foreach(var item in Seq(Get(LayoutPages[index],"slots")).Select(Obj)){
   int row=Int(Get(item,"row"),-1),column=Int(Get(item,"column"),-1);var data=Obj(Get(item,"module"));
   if(data!=null&&row>=0&&row<2&&column>=0&&column<columns)result[row*columns+column]=new TaskbarModule(data,hideGraphs,single);
  }
  return result;
 }
 // Colors are optional, opaque per-metric overrides; absent values keep system colors.
 static readonly string[] ColorParts={"label","reading","graph"};
 static bool TryColor(string value,out int rgb){rgb=0;return value.Length==7&&value[0]=='#'&&int.TryParse(value.Substring(1),NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out rgb);}
 static Dictionary<string,object> NormalizeColors(object value){
  var map=new Dictionary<string,object>();var input=Obj(value);
  foreach(string metric in Metrics){var row=Obj(Get(input,metric));if(row==null)continue;var clean=new Dictionary<string,object>();
   foreach(string part in ColorParts){int rgb;if(TryColor(Str(Get(row,part)),out rgb))clean[part]="#"+rgb.ToString("X6",CultureInfo.InvariantCulture);}
   if(clean.Count>0)map[metric]=clean;
  }
  return map;
 }
 internal int? ColorRgb(string metric,string part){var row=Obj(Get(Obj(Get(layout,"colors")),metric));int rgb;return TryColor(Str(Get(row,part)),out rgb)?(int?)rgb:null;}
 public TaskbarSettings Normalize(){
  var value=(TaskbarSettings)MemberwiseClone();value.deviceId=string.IsNullOrWhiteSpace(deviceId)?"local":deviceId;
  value.background=false;
  value.refreshMs=new[]{DefaultRefreshMs,500,1000,2000,3000,5000,10000}.Contains(refreshMs)?refreshMs:refreshMs==30000||refreshMs==60000?10000:DefaultRefreshMs;
  value.numberMotion=motion&&new[]{"current","direct","fade","slide"}.Contains(numberMotion)?numberMotion:"direct";
  value.backgroundStyle=new[]{"mist","graphite","ocean","forest"}.Contains(backgroundStyle)?backgroundStyle:"mist";
  value.intervalSeconds=AutoPageIntervals.OrderBy(seconds=>Math.Abs((long)seconds-intervalSeconds)).First();
  var wire=Copy(layout);var source=Seq(Get(wire,"pages")).Take(3).ToArray();
  var legacy=pages==null||pages.Length==0?Defaults():pages;bool migrate=source.Length==0;
  var pageIds=new HashSet<string>();var slotIds=new HashSet<string>();var moduleIds=new HashSet<string>();
  var normalized=new List<Dictionary<string,object>>();var shelf=Copy(Get(wire,"shelf"));
  for(int p=0;p<(migrate?Math.Min(3,legacy.Length):source.Length);p++){
   var item=Copy(migrate?null:source[p]);string id=Id(Get(item,"id"),"page-"+(p+1),pageIds);item["id"]=id;
   var grid=Copy(Get(item,"grid"));int oldColumns=Math.Max(1,Math.Min(3,Int(Get(grid,"columns"),slots==6?3:slots==2?1:2)));int cols=oldColumns;grid["columns"]=cols;grid["rows"]=2;item["grid"]=grid;
   var entries=Seq(Get(item,"slots")).Select(Obj).Where(s=>s!=null).ToArray();var cells=new List<Dictionary<string,object>>();var retired=Seq(Get(item,"retiredSlots")).ToList();
   for(int i=0;i<2*oldColumns;i++){
    int row=i/oldColumns,column=i%oldColumns;var cell=Copy(entries.FirstOrDefault(e=>Int(Get(e,"row"),-1)==row&&Int(Get(e,"column"),-1)==column));
    string sid=Id(Get(cell,"id"),id+"-r"+row+"c"+column,slotIds);cell["id"]=sid;cell["row"]=row;cell["column"]=column;
    var module=Copy(Get(cell,"module"));if(migrate&&legacy[p]!=null&&i<legacy[p].Length)module["metricId"]=legacy[p][i];
    string metric=Str(Get(module,"metricId"));
    if(metric.Length==0)cell["module"]=null;
    else{
     module["id"]=Id(Get(module,"id"),"module-"+sid,moduleIds);
     var parts=Seq(Get(module,"parts")).Select(Str).Where(s=>s.Length>0).Distinct().Take(2).ToArray();module["parts"]=parts.Length==0?new[]{"number"}:parts;
     var name=Copy(Get(module,"name"));if(Str(Get(name,"variant"))=="")name["variant"]=TaskbarModule.DefaultVariant(metric);module["name"]=name;cell["module"]=module;
    }
    if(column<cols)cells.Add(cell);
    else{retired.Add(cell);if(metric.Length>0&&!shelf.ContainsKey(metric))shelf[metric]=module;}
   }
   if(retired.Count>0)item["retiredSlots"]=retired.ToArray();
   item["slots"]=cells.ToArray();normalized.Add(item);
  }
  wire["schemaVersion"]=Math.Max(2,Int(Get(wire,"schemaVersion"),2));wire["revision"]=Math.Max(0,Int(Get(wire,"revision"),0));wire["pages"]=normalized.ToArray();wire["shelf"]=shelf;wire["typography"]=Copy(Get(wire,"typography"));wire["colors"]=NormalizeColors(Get(wire,"colors"));
  value.layout=wire;if(!value.SetActive(Str(Get(wire,"activePageId"))))value.SetActive(value.PageId(Math.Max(0,Math.Min(value.PageCount-1,page))));
  value.slots=value.MaxSlots;value.pages=Enumerable.Range(0,value.PageCount).Select(p=>value.Modules(p,value.Columns(p)*2,false).Select(m=>m==null?null:m.Metric).ToArray()).ToArray();return value;
 }
}
internal sealed class TaskbarModule {
 internal readonly Dictionary<string,object> Data;readonly bool compact,single;
 internal TaskbarModule(Dictionary<string,object> data,bool compact,bool single=false){Data=data;this.compact=compact;this.single=single;}
 internal string Id{get{return TaskbarSettings.Str(TaskbarSettings.Get(Data,"id"));}}
 internal string Metric{get{return TaskbarSettings.Str(TaskbarSettings.Get(Data,"metricId"));}}
 internal string[] Parts{get{
  var parts=TaskbarSettings.Seq(TaskbarSettings.Get(Data,"parts")).Select(TaskbarSettings.Str).ToArray();
  if(compact||parts.Length==0)return new[]{"number"};
  if(single&&parts.Length>1)return new[]{parts.Contains("number")?"number":parts[0]};
  return parts;
 }}
 internal static string DefaultVariant(string id){return id=="up"||id=="down"?"arrow":id.StartsWith("cpu")||id.StartsWith("gpu")&&id!="gpuMemory"&&id!="gpuFan"&&id!="gpuPower"||id=="storageRead"||id=="storageWrite"?"english":"chinese";}
 internal string Label(string fallback){
  var name=TaskbarSettings.Obj(TaskbarSettings.Get(Data,"name"));string v=TaskbarSettings.Str(TaskbarSettings.Get(name,"variant"));
  if(Metric=="down"||Metric=="up")return v=="chinese"?(Metric=="up"?"上传":"下载"):(Metric=="up"?"↑":"↓");
  if(Metric=="memory"&&v=="ram")return "RAM";if(Metric=="storage"&&v=="ssd")return "SSD";
  if(v=="icon")return "◇";
  if(Metric.StartsWith("cpu"))return "CPU";
  if(Metric=="gpu"||Metric=="gpuTemperature"||Metric=="gpuFrequency")return "GPU";
  if(v=="english"){switch(Metric){case "memory":return "MEM";case "storage":return "Disk";case "gpuMemory":return "VRAM";case "memoryUsed":return "Used";case "gpuFan":return "Fan";case "gpuPower":return "Power";case "ping":return "Ping";case "loss":return "Loss";case "io":return "I/O";}}
  if(v=="short"){if(Metric=="storageRead")return "读";if(Metric=="storageWrite")return "写";}
  if(v=="chinese"){if(Metric=="storageRead")return "读取";if(Metric=="storageWrite")return "写入";}
  return fallback.Length==0?"未知":fallback;
 }
}
public sealed class TaskbarValue {
 public string id,label,unit;public double? value;public int decimals;public DateTime timeUtc;public bool stale,animate=true;
 public string Text{get{return stale||!value.HasValue||double.IsNaN(value.Value)||double.IsInfinity(value.Value)?"—":value.Value.ToString("F"+(Math.Abs(value.Value)>=100?0:decimals),CultureInfo.InvariantCulture);}}
 // Presentation conversion does not mutate source telemetry or its published value/unit.
 internal TaskbarDisplay GetDisplay(){
  if(stale||!value.HasValue||double.IsNaN(value.Value)||double.IsInfinity(value.Value))return new TaskbarDisplay{Text="—",Unit="",Value=0,Decimals=0};
  double number=value.Value;string suffix=unit??"";string[] units=null;double radix=1000;
  if(suffix.EndsWith("/s",StringComparison.Ordinal)){units=new[]{"B/s","K/s","M/s","G/s","T/s","P/s","E/s"};radix=1024;}
  else if(suffix=="GHz"||suffix=="THz"||suffix=="PHz"||suffix=="EHz")units=new[]{"GHz","THz","PHz","EHz"};
  else if(suffix=="GB"||suffix=="TB"||suffix=="PB"||suffix=="EB"){units=new[]{"GB","TB","PB","EB"};radix=1024;}
  else if(suffix=="W"||suffix=="kW"||suffix=="MW"||suffix=="GW")units=new[]{"W","kW","MW","GW"};
  int index=units==null?-1:Array.IndexOf(units,suffix);if(index>=0){while(Math.Abs(number)>=radix&&index<units.Length-1){number/=radix;index++;}suffix=units[index];}
  int places=Math.Abs(number)>=100?0:Math.Max(0,Math.Min(3,decimals));return new TaskbarDisplay{Text=number.ToString("F"+places,CultureInfo.InvariantCulture),Unit=suffix,Value=number,Decimals=places};
 }
 public static string PlaceKey(string text,int at){int dot=text.IndexOf('.');if(dot<0)dot=text.Length;return at<dot?"i"+(dot-at-1):at==dot?"dot":"f"+(at-dot);}
 public static bool GlyphChanges(string previous,string next,int nextIndex){var key=PlaceKey(next,nextIndex);for(int i=0;i<previous.Length;i++)if(PlaceKey(previous,i)==key)return previous[i]!=next[nextIndex];return true;}
}
internal struct TaskbarDisplay {internal string Text,Unit;internal double Value;internal int Decimals;}
public sealed class TaskbarSnapshot {
 public double[] cpuCores=new double[0];public string device="本机",error="";public DateTime timeUtc;public Dictionary<string,TaskbarValue> values=new Dictionary<string,TaskbarValue>();
 internal string detailGpu,detailVolume;
 internal readonly Dictionary<string,TaskbarValue> detailValues=new Dictionary<string,TaskbarValue>();
 internal TaskbarValue ReadDetail(string id){TaskbarValue value;return detailValues.TryGetValue(id,out value)?value:Read(id);}
 public TaskbarValue Read(string id){TaskbarValue v;string vname;if(id!=null&&values.TryGetValue(id,out v))return v;var names=new Dictionary<string,string>{{"cpu","CPU"},{"memory","内存"},{"storage","存储"},{"gpu","GPU"},{"down","↓"},{"up","↑"},{"cpuFrequency","CPU"},{"gpuFrequency","GPU"},{"gpuMemory","显存"},{"gpuTemperature","GPU"},{"io","负载"},{"ping","延迟"},{"loss","丢包"},{"cpuTemperature","CPU"},{"gpuPower","功耗"},{"gpuFan","风扇"},{"memoryUsed","已用"},{"memoryFree","可用"},{"storageFree","可用"},{"connections","连接"},{"storageRead","R"},{"storageWrite","W"}};return new TaskbarValue{id=id,label=id!=null&&names.TryGetValue(id,out vname)?vname:"",unit="",stale=true};}
}
