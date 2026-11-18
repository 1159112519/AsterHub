using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Web.Script.Serialization;
internal static class TaskbarSqueezeTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static Dictionary<string,object> D(params object[] a){var d=new Dictionary<string,object>();for(int i=0;i<a.Length;i+=2)d[(string)a[i]]=a[i+1];return d;}
 internal static TaskbarSettings Options(){var slots=new List<object>();string[] ids={"cpu","memory","gpu","cpuFrequency","gpuTemperature","gpuPower"};for(int i=0;i<6;i++)slots.Add(D("row",i/3,"column",i%3,"module",D("metricId",ids[i],"name",D("variant","english"),"parts",new[]{"number"})));return new TaskbarSettings{layout=D("pages",new[]{D("id","one","grid",D("columns",3),"slots",slots.ToArray())})}.Normalize();}
 internal static TaskbarSnapshot Sample(){var s=new TaskbarSnapshot{timeUtc=DateTime.UtcNow};foreach(string id in TaskbarSettings.Metrics)s.values[id]=new TaskbarValue{id=id,label=id=="memory"?"MEM":id.StartsWith("cpu")?"CPU":"GPU",value=48,unit=id.EndsWith("Temperature")?"°C":id.EndsWith("Frequency")?"GHz":"%",timeUtc=s.timeUtc};return s;}
 static int Main(string[] args){Directory.CreateDirectory(args[1]);var o=Options();var sample=Sample();foreach(float scale in new[]{1f,1.25f,2f})using(var p=new TaskbarPainter(480,(int)(38*scale),true,args[0])){
  var layout=p.MeasurePage(sample,o,0);float full=layout.FullWidth;float narrow=TaskbarSqueezeLayout.Start+TaskbarSqueezeLayout.Edge+(layout.Right[0]-layout.Left[0])+(layout.Right[1]-layout.Left[1])+TaskbarSqueezeLayout.MaxGap;
  var m=new TaskbarSqueezeMotion();m.Set(full,0,false);m.Frame(layout,full,0,false);m.Set(narrow,10,false);float last=1;int partial=0;var alphas=new List<float>();
  for(int t=10;t<=442;t+=16){var f=m.Frame(layout,narrow,t,false);if(f.Alpha[2]>0&&f.Alpha[2]<1)partial++;Check(f.Alpha[2]<=last+.001f,"monotonic exit "+scale+" "+t);last=f.Alpha[2];alphas.Add(last);if(t==170){Check(f.Departing[2]&&f.Alpha[2]>.1f,"large width step retains an outgoing column");Check(f.Blur[2]>0,"outgoing Gaussian blur");}p.Resize((int)Math.Ceiling(narrow*scale),(int)(38*scale));using(var bmp=p.RenderSqueeze(sample,null,o,0,1,f,scale,0)){if(t==74||t==170||t==298||t==442)bmp.Save(Path.Combine(args[1],"squeeze-"+scale+"-"+t+".png"),ImageFormat.Png);}}
  Check(partial>=10,"exit remains visible across many native frames "+scale);Check(last==0,"exit settles "+scale);
  m.Set(full,450,false);var before=m.Frame(layout,full,530,false);m.Set(narrow,530,false);var after=m.Frame(layout,narrow,530,false);Check(Math.Abs(before.Alpha[2]-after.Alpha[2])<.001f&&Math.Abs(before.X[2]-after.X[2])<.001f,"mid-animation reversal is continuous "+scale);
  m.Set(full,1000,true);Check(!m.Active(1000)&&m.Frame(layout,full,1000,true).Alpha[2]==1,"reduced motion snaps intentionally "+scale);
  File.WriteAllText(Path.Combine(args[1],"squeeze-alpha-"+scale+".json"),new JavaScriptSerializer().Serialize(alphas));
 }return 0;}
}
internal static class UiLanguage {internal static string Text(string text){return text;}}
