using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

// Uses the real archive and analysis bridge. The host shell replaces only WPF and the unused SSH recorder branch.
public sealed partial class WorkbenchWindow {
 readonly ArchiveService archive;
 readonly Server server=new Server{IsLocal=true,Name="fixture"};
 readonly SemaphoreSlim reads=new SemaphoreSlim(1);
 WorkbenchWindow(ArchiveService writer){archive=writer;}
 Server Requested(Dictionary<string,object> args){return server;}
 static List<ArchivePoint> RecorderHistory(Server s,List<ArchivePoint> p,DateTime f,DateTime t,CancellationToken c){throw new InvalidOperationException();}
 static double Milliseconds(DateTime value){return (value-new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc)).TotalMilliseconds;}
 static object Finite(double value){return double.IsInfinity(value)||double.IsNaN(value)?null:(object)value;}
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr handle);
 static void Diagnostics(){
  int observed=0,legacy=0,limited=0,denied=0,exited=0,other=0;
  foreach(var process in Process.GetProcesses())using(process){
   if(process.Id==0)continue;observed++;
   try{var start=process.StartTime;var cpu=process.TotalProcessorTime;var working=process.WorkingSet64;legacy++;}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}
   IntPtr handle=OpenProcess(0x1000,false,process.Id);
   if(handle==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(error==5)denied++;else if(error==87)exited++;else other++;}
   else{limited++;CloseHandle(handle);}
  }
  Console.WriteLine("LOCAL_PROCESS_ACCESS_CONTEXT="+Environment.UserName+";OBSERVED="+observed+";LEGACY_ALL_FIELDS="+legacy+";LIMITED_QUERY="+limited+";ACCESS_DENIED="+denied+";EXITED_OR_INVALID="+exited+";OTHER_OPEN_FAILURE="+other);
 }
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 Dictionary<string,object> Query(string metric){var serializer=ArchiveStore.Json();var data=QueryResourceAnalysis(new Dictionary<string,object>{{"metric",metric},{"minutes",3}}).GetAwaiter().GetResult();var response=(Dictionary<string,object>)serializer.DeserializeObject(serializer.Serialize(data));return (Dictionary<string,object>)((object[])response["points"]).Single();}
 static Dictionary<string,object>[] Rows(Dictionary<string,object> sample,string field){return ((object[])sample[field]).Cast<Dictionary<string,object>>().ToArray();}
 public static int Main(string[] args){try{
  using(var writer=new ArchiveService(2,128,args[0])){
   var host=new WorkbenchWindow(writer);
   var metrics=new Metrics{Cpu="25%",Mem="512 / 1024 MiB",ResourceProcesses=new List<ResourceProcess>()};
   // Deliberately add lowest values first. A global top-N would lose GPU 13.
   for(int card=0;card<4;card++){
    string gpu=(10+card).ToString();metrics.Gpus.Add(new Gpu{Index=gpu,Uuid="card-"+card,Name="fixture",Used="512",Total="1024"});
    for(int i=0;i<20;i++)metrics.Processes.Add(new GpuProcess{Gpu=gpu,Pid=(100+i).ToString(),IdentityToken="fixture:"+i,Name="worker",Memory=i+" MiB"});
   }
   var json=ArchiveStore.Json();var raw=json.Serialize(ArchiveRecord.From(metrics,DateTime.UtcNow.AddSeconds(-2),5));
   // Feed the added archive fields via JSON so the same test also runs against the pre-change model.
   var fixture=(Dictionary<string,object>)json.DeserializeObject(raw);
   var gp=((object[])fixture["Processes"]).Cast<Dictionary<string,object>>().ToArray();
   gp[79]["Path"]="/srv/card 13/worker";
   gp[78]["Name"]="/display-label/looks-like-a-path";
   fixture["ResourceProcesses"]=new object[]{new{Pid=77,Identity="77:123",Name="worker",Path=@"C:\two words\worker.exe",Cpu=2,ResidentBytes=1024},new{Pid=78,Identity="78:456",Name="cpu-only",Cpu=3}};
   var record=json.Deserialize<ArchiveRecord>(json.Serialize(fixture));
   writer.Store.Append(host.server,record);
   var gpuRows=Rows(host.Query("gpu"),"gpuProcesses");
   Check(gpuRows.Length==64&&gpuRows.GroupBy(r=>r["Gpu"]).All(g=>g.Count()==16),"four cards must each retain their own 16 ranked rows");
   var pathRow=gpuRows.Single(r=>(string)r["Gpu"]=="13"&&(string)r["Pid"]=="119");
   Check((string)pathRow["Path"]=="/srv/card 13/worker","explicit archive GPU path returned; high-usage row remains ranked");
   Check(gpuRows.Single(r=>(string)r["Gpu"]=="13"&&(string)r["Pid"]=="118")["Path"]==null,"display name is not an executable path");
   var cpu=Rows(host.Query("cpu"),"processes");var memory=Rows(host.Query("memory"),"processes");
   Check(cpu.Length==2&&memory.Length==1&&(string)memory[0]["Path"]==@"C:\two words\worker.exe","unknown working set is not zero or a condition for CPU ranking");
   // A long-query fixture must remain serializable, preserving the first/latest
   // sample and all four cards while reducing time density only when necessary.
   foreach(var p in record.Processes)p.Path="/srv/"+new string('x',1800)+"/worker";
   var oldest=DateTime.UtcNow.AddSeconds(-155);
   for(int i=0;i<150;i++){record.T=oldest.AddSeconds(i).Ticks;writer.Store.Append(host.server,record);}
   var longData=host.QueryResourceAnalysis(new Dictionary<string,object>{{"metric","gpu"},{"minutes",3}}).GetAwaiter().GetResult();
   string payload=json.Serialize(longData);
   var longResponse=(Dictionary<string,object>)json.DeserializeObject(payload);var longPoints=(object[])longResponse["points"];
   Check((bool)longResponse["thinned"]&&payload.Length<3900000,"large multi-GPU response fits host serializer budget");
   Check(longPoints.Cast<Dictionary<string,object>>().All(p=>Rows(p,"gpuProcesses").Length==64),"payload thinning keeps each card per retained historical sample");
   Console.WriteLine("GPU_LONG_QUERY_POINTS="+longPoints.Length+";JSON_CHARS="+payload.Length);
   // Real local process, same security context, no elevation or WMI requirement.
   using(var self=Process.GetCurrentProcess()){
    var timer=Stopwatch.StartNew();var sampler=typeof(DeviceCapture).Assembly.GetType("LocalResourceProcesses",true);var rows=(List<ResourceProcess>)sampler.GetMethod("Sample",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(Activator.CreateInstance(sampler),null);timer.Stop();
    var sampled=rows.SingleOrDefault(p=>p.Pid==self.Id);
    var sampleJson=sampled==null?null:(Dictionary<string,object>)json.DeserializeObject(json.Serialize(sampled));
    Check(sampleJson!=null&&sampleJson.ContainsKey("Path")&&StringComparer.OrdinalIgnoreCase.Equals(sampleJson["Path"],self.MainModule.FileName),"native local process path matches real running executable");
    var enriched=new List<GpuProcess>{new GpuProcess{Pid=self.Id.ToString(),Name="self"}};
    ProcessMetadata.Enrich(enriched);
    Check(StringComparer.OrdinalIgnoreCase.Equals(enriched[0].Path,self.MainModule.FileName)&&!string.IsNullOrEmpty(enriched[0].IdentityToken),"live GPU metadata identity and executable path");
    Console.WriteLine("LOCAL_QUERY_SAMPLE_MS="+timer.ElapsedMilliseconds);
   }
  }
  Diagnostics();
  Console.WriteLine("ANALYSIS_ARCHIVE_PROCESS_PATHS_CARD_CAPS_AND_NATIVE_SAMPLE=PASS");return 0;}catch(Exception e){Console.Error.WriteLine(e.GetType().Name+": "+e.Message+Environment.NewLine+e.StackTrace);return 1;}
 }
}
