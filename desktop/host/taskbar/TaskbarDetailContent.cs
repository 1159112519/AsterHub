using System;
using System.Globalization;
using System.Linq;

// Content follows the five detail layouts in the original taskbar prototype.
internal sealed class TaskbarDetailContent {
 internal sealed class Row {
  internal readonly string Label,Value;
  internal Row(string label,string value){Label=label;Value=value;}
 }
 internal readonly string Title,Kind,Device,Subject,Window,Scale;
 internal readonly TaskbarDisplay Main,Secondary;
 internal readonly Row[] Rows,Legend;
 internal readonly double? Fraction;
 internal readonly double[] Cores;
 internal readonly TaskbarHistory.DetailPoint[][] Series;
 internal readonly double Ceiling;
 internal readonly bool Dual;
 static readonly string[][] groups={new[]{"cpu","cpuTemperature","cpuFrequency"},new[]{"gpu","gpuFrequency","gpuMemory","gpuFan","gpuPower","gpuTemperature"},new[]{"memory","memoryUsed"},new[]{"down","up","ping","loss"},new[]{"storage","storageRead","storageWrite","io"}};
 internal static bool Plots(string metric){switch(metric){case "cpu":case "gpu":case "memory":case "up":case "down":case "storageRead":case "storageWrite":return true;default:return false;}}
 internal static string Primary(string metric){var group=groups.FirstOrDefault(g=>g.Contains(metric));return group==null?metric:group[0];}
 static string Format(TaskbarValue v){var d=v.GetDisplay();return d.Text+(string.IsNullOrEmpty(d.Unit)?"":" "+d.Unit);}
 static double? Value(TaskbarValue v){return v.GetDisplay().Text=="—"?null:v.value;}
 static string N(double n){return n.ToString("0.##",CultureInfo.InvariantCulture);}
 internal TaskbarDetailContent(string primary,TaskbarSnapshot snapshot,TaskbarHistory history){
  Kind=primary;snapshot=snapshot??new TaskbarSnapshot();Device=string.IsNullOrWhiteSpace(snapshot.device)?UiLanguage.Text("监测设备"):snapshot.device;
  TaskbarValue main=snapshot.Read(primary);
  Main=main.GetDisplay();Secondary=snapshot.Read("up").GetDisplay();
  Func<string,string,Row> row=(label,id)=>new Row(label,Format(snapshot.ReadDetail(id)));
  string[] tracks;Dual=primary=="down";Cores=new double[0];Legend=new Row[0];Subject="";
  switch(primary){
   case "cpu":
    Title=UiLanguage.Text("CPU 使用率");tracks=new[]{"cpu"};Cores=snapshot.cpuCores??new double[0];
    Rows=new[]{row(UiLanguage.Text("用户进程"),"cpuUser"),row(UiLanguage.Text("系统进程"),"cpuSystem"),new Row(UiLanguage.Text("逻辑核心"),Cores.Length==0?"—":Cores.Length.ToString(CultureInfo.InvariantCulture)),row(UiLanguage.Text("当前频率"),"cpuFrequency"),row(UiLanguage.Text("温度"),"cpuTemperature")};break;
   case "gpu":
    Title=UiLanguage.Text("GPU 使用率");Subject=snapshot.detailGpu??"";tracks=new[]{"gpu"};Fraction=Value(snapshot.Read("gpuMemory"))/100;
    Rows=new[]{row(UiLanguage.Text("温度"),"gpuTemperature"),row(UiLanguage.Text("功耗"),"gpuPower"),row(UiLanguage.Text("风扇转速"),"gpuFan"),row(UiLanguage.Text("当前频率"),"gpuFrequency")};
    Legend=new[]{row(UiLanguage.Text("已用显存"),"gpuMemoryUsed"),row(UiLanguage.Text("显存总量"),"gpuMemoryTotal")};break;
   case "memory":
    Title=UiLanguage.Text("内存占用");tracks=new[]{"memory"};Fraction=Value(main)/100;
    Rows=new[]{row(UiLanguage.Text("已用内存"),"memoryUsed"),row(UiLanguage.Text("可用内存"),"memoryFree"),row(UiLanguage.Text("物理内存"),"memoryTotal")};
    Legend=new[]{row(UiLanguage.Text("应用"),"memoryApps"),row(UiLanguage.Text("系统"),"memorySystem"),row(UiLanguage.Text("压缩"),"memoryCompressed"),row(UiLanguage.Text("可用"),"memoryFree")};break;
   case "down":
    Title=UiLanguage.Text("网络速率");tracks=new[]{"down","up"};
    Rows=new[]{row(UiLanguage.Text("往返延迟"),"ping"),row(UiLanguage.Text("丢包率"),"loss"),row(UiLanguage.Text("接口累计接收"),"networkReceived"),row(UiLanguage.Text("接口累计发送"),"networkSent")};break;
   case "storage":
    Title=UiLanguage.Text("存储占用");Subject=snapshot.detailVolume??"";tracks=new[]{"storageRead","storageWrite"};Fraction=Value(main)/100;
    Rows=new[]{row(UiLanguage.Text("读取速度"),"storageRead"),row(UiLanguage.Text("写入速度"),"storageWrite"),row(UiLanguage.Text("磁盘活动"),"io")};
    Legend=new[]{row(UiLanguage.Text("已用空间"),"storageUsed"),row(UiLanguage.Text("可用空间"),"storageFree"),row(UiLanguage.Text("总容量"),"storageTotal")};break;
   default:
    Title=UiLanguage.Text("监测详情");tracks=new[]{primary};Rows=new Row[0];break;
  }
  var samples=tracks.Select(id=>history.Detail(id)).ToArray();
  bool rates=primary=="down"||primary=="storage";
  if(rates)for(int i=0;i<samples.Length;i++)samples[i]=samples[i].Select(p=>new TaskbarHistory.DetailPoint(p.Time,p.Value*(p.Unit=="M/s"?1048576:p.Unit=="K/s"?1024:1),"B/s",p.BreakBefore)).ToArray();
  Series=samples;int count=Series.Select(a=>a.Length).DefaultIfEmpty().Max();
  Window=count<2?UiLanguage.Text("等待更多采样"):UiLanguage.Text("最近 60 秒");
  Ceiling=rates?Math.Max(1,Series.SelectMany(a=>a).Select(p=>p.Value).DefaultIfEmpty().Max()*1.08):100;
  Scale=rates?(count<2?"":N(Ceiling>=1048576?Ceiling/1048576:Ceiling/1024)+(Ceiling>=1048576?" M/s":" K/s")+UiLanguage.Text(" 上限")):"0–100 %";
  if(primary=="cpu"&&count>=2){var a=Series[0];Rows=Rows.Concat(new[]{new Row(UiLanguage.Text("记录峰值"),N(a.Max(p=>p.Value))+" %"),new Row(UiLanguage.Text("记录最低"),N(a.Min(p=>p.Value))+" %")}).ToArray();}
  Rows=Rows.Where(r=>!string.IsNullOrWhiteSpace(r.Value)&&r.Value!="—").ToArray();
  Legend=Legend.Where(r=>!string.IsNullOrWhiteSpace(r.Value)&&r.Value!="—").ToArray();
 }
}
