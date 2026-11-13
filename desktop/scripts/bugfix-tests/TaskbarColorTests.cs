using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

internal static class TaskbarColorTests {
 static void Require(bool ok,string name){if(!ok)throw new InvalidOperationException(name);Console.WriteLine("PASS "+name);}
 static Dictionary<string,object> Dict(params object[] pairs){var result=new Dictionary<string,object>();for(int i=0;i<pairs.Length;i+=2)result[(string)pairs[i]]=pairs[i+1];return result;}
 static TaskbarSettings Setup(string metric,string name,params string[] parts){
  return new TaskbarSettings{layout=Dict("pages",new[]{Dict("id","one","grid",Dict("columns",1),"slots",new[]{Dict("row",0,"column",0,"module",Dict("metricId",metric,"name",Dict("variant",name),"parts",parts))})})}.Normalize();
 }
 static TaskbarSettings Colored(TaskbarSettings basis,string metric){
  var serializer=new JavaScriptSerializer();var options=serializer.Deserialize<TaskbarSettings>(serializer.Serialize(basis));
  options.layout["colors"]=Dict(metric,Dict("label","#FF3322","reading","#22FF33","graph","#2244FF","extra","#123456"),"unknown",Dict("label","#FFFFFF"),"gpuPower",Dict("label","#33AAAA55"));
  return options.Normalize();
 }
 static TaskbarSnapshot Sample(string id,double value,int seconds){var s=new TaskbarSnapshot{timeUtc=new DateTime(2026,1,1,0,0,seconds,DateTimeKind.Utc),cpuCores=new[]{12d,80,36,70}};s.values[id]=new TaskbarValue{id=id,label=id=="up"?"↑":"CPU",unit=id=="up"?"K/s":"%",value=value,timeUtc=s.timeUtc};return s;}
 static int Count(Bitmap image,int part){int n=0;for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var c=image.GetPixel(x,y);if(c.A>54&&(part==0?c.R>180&&c.G<100&&c.B<100:part==1?c.G>180&&c.R<100&&c.B<100:c.B>180&&c.R<100&&c.G<120))n++;}return n;}
 static bool SameInk(Bitmap a,Bitmap b){for(int y=0;y<a.Height;y++)for(int x=0;x<a.Width;x++)if((a.GetPixel(x,y).A>100)!=(b.GetPixel(x,y).A>100))return false;return true;}
 static int Main(string[] args){
  var basis=Setup("cpu","english","number","bars");var options=Colored(basis,"cpu");
  Require(options.Key!=basis.Key&&options.ColorRgb("cpu","label")==0xff3322,"color participates in desktop save revision and reconfigure");
  Require(options.ColorRgb("cpu","extra")==null&&options.ColorRgb("unknown","label")==null&&options.ColorRgb("gpuPower","label")==null,"reject unknown metrics, properties and alpha");
  var json=new JavaScriptSerializer();Require(json.Deserialize<TaskbarSettings>(json.Serialize(options)).Normalize().Key==options.Key,"round-trip config keeps color and layout");
  Require(basis.ColorRgb("cpu","label")==null,"old config follows defaults");
  Require(Setup("memory","ram","number").Modules(0,2,false)[0].Label("内存")=="RAM"&&Setup("storage","ssd","number").Modules(0,2,false)[0].Label("存储")=="SSD","new names are opt-in native variants");
  Require(Setup("memory","english","number").Modules(0,2,false)[0].Label("内存")=="MEM"&&Setup("storage","english","number").Modules(0,2,false)[0].Label("存储")=="Disk","existing aliases are unchanged");
  var now=Sample("cpu",48,4);var prior=Sample("cpu",27,1);
  foreach(double scale in new[]{1d,1.25d})using(var painter=new TaskbarPainter((int)(136*scale),(int)(38*scale),scale==1,args[0])){
   painter.CellWidth=TaskbarSettings.WideCellWidth;
   using(var plain=painter.Render(now,prior,basis,0,1,false))using(var tinted=painter.Render(now,prior,options,0,1,false)){
    Require(Count(tinted,0)>8&&Count(tinted,1)>12&&Count(tinted,2)>5,"name, reading, unit and bars use independent colors at "+scale);
    Require(SameInk(plain,tinted),"custom colors preserve static ink geometry at "+scale);
    tinted.Save(Path.Combine(args[1],"taskbar-bars-"+scale+".png"),ImageFormat.Png);
   }
   var animated=options;animated.numberMotion="current";
   using(var image=painter.Render(now,prior,animated,0,.62,false))Require(Count(image,1)>5,"changing and resting digits share reading color at "+scale);
   var arrow=Colored(Setup("up","arrow","number"),"up");using(var image=painter.Render(Sample("up",1.4,4),null,arrow,0,1,false))Require(Count(image,0)>4,"arrow uses name color at "+scale);
   var icon=Colored(Setup("cpu","icon","line"),"cpu");painter.History=new TaskbarHistory();painter.History.Add(prior);painter.History.Add(now);
   using(var image=painter.Render(now,prior,icon,0,1,false)){
    Require(Count(image,0)>4&&Count(image,2)>4,"name icon and line stroke use overrides at "+scale);
    image.Save(Path.Combine(args[1],"taskbar-line-"+scale+".png"),ImageFormat.Png);
   }
  }
  return 0;
 }
}
