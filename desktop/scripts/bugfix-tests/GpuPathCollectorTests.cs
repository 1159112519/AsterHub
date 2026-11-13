using System;
using System.Text;
using System.Linq;
using System.Web.Script.Serialization;
// Compile with the real Remote.cs and Core/Storage assemblies into a private QA directory.
// Transport returns fixtures only; this never opens SSH or inspects/terminates a process.
public static class SshAuthentication {
 public static string Raw;public static string HostOptions="";
 public static bool Managed(Server s){return true;}
 public static string ExecuteMonitoring(Server s,string command,int timeout){return Raw;}
}
public static class Local {public static Metrics Collect(){throw new Exception("Local collection is not part of this fixture");}}
public static class PeripheralCollector {public const string RemoteCommand="";public static void ParseRemote(Metrics m,string section,string line){}}
public static class HardwareInventory {public static HardwareInfo Get(Server s){return null;}}
public static class GpuPathCollectorTests {
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static string B64(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
 public static void Main(){
  string first="/home/alice/模型 one/venv/bin/python",second="/srv/task two/venv/bin/python",token="deadbeef:123456:1000";
  SshAuthentication.Raw="AQGPU\n0,fixture,10,1024,8192,40,0,10,100,GPU-fixture\nAQPROC\nGPU-fixture,700,python,1024\nGPU-fixture,701,python,2048\nGPU-fixture,702,python,0\nGPU-fixture,703,legacy-python,0\nGPU-fixture,704,fallback-python,0\nGPU-fixture,705,relative-python,0\nAQCOMMANDS\n700\t"+token+"\t-\t"+B64(first)+"\n701\t"+token+"\t-\t"+B64(second)+"\n702\t"+token+"\t-\t-\n703\t"+token+"\t-\n704\t"+token+"\t-\tnot base64!\n705\t"+token+"\t-\t"+B64("relative/path")+"\nAQUSERS\n700 alice\n701 bob\nAQEND\n";
  var metrics=Remote.Collect(new Server{Host="fixture",User="alice",Port=22});
  Check(metrics.Processes.Count==6,"all raw process rows retained");
  Check(metrics.Processes[0].Name==first&&metrics.Processes[1].Name==second,"full duplicate-basename paths survive mapping");
  Check(metrics.Processes[0].Pid=="700"&&metrics.Processes[0].User=="alice"&&metrics.Processes[0].IdentityToken==token,"PID owner identity unchanged");
  Check(metrics.Processes[0].Path==first&&metrics.Processes[1].Path==second&&metrics.Processes.Skip(2).All(p=>p.Path==null),"only actual captured executable paths populate Path");
  Check(metrics.Processes[0].CommandLine==null,"path does not fabricate command line");
  Check(metrics.Processes[2].Name=="python"&&metrics.Processes[3].Name=="legacy-python"&&metrics.Processes[4].Name=="fallback-python"&&metrics.Processes[5].Name=="relative-python","missing legacy invalid metadata fall back");
  Check(metrics.Processes.All(p=>p.IdentityToken==token),"missing path does not drop safe process identity");
  var record=ArchiveRecord.From(metrics,DateTime.UtcNow,5);var json=new JavaScriptSerializer();var restored=json.Deserialize<ArchiveRecord>(json.Serialize(record)).Metrics();
  Check(restored.Processes[0].Path==first&&restored.Processes[1].Path==second&&restored.Processes[0].Name==first&&restored.Processes[1].Name==second&&restored.Processes[0].IdentityToken==token,"archive full path and identity round trip");
  Console.WriteLine("GPU_REMOTE_PATH_MAPPING_FALLBACK_IDENTITY_AND_ARCHIVE=PASS");
 }
}
