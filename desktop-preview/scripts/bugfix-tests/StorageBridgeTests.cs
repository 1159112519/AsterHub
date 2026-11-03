using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
public static class StorageBridgeTests {
 static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 static object Get(object value,string name){return value.GetType().GetProperty(name).GetValue(value,null);}
 [STAThread] public static int Main(){string fixture=Path.Combine(Path.GetTempPath(),"asterhub-storage-bridge-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);var lifetime=new CancellationTokenSource();var slots=new SemaphoreSlim(2,2);
 try{Directory.CreateDirectory(Path.Combine(fixture,"models"));File.WriteAllText(Path.Combine(fixture,"file.txt"),"fixture");var window=(WorkbenchWindow)FormatterServices.GetUninitializedObject(typeof(WorkbenchWindow));typeof(WorkbenchWindow).GetField("devices",Hidden).SetValue(window,new PreviewDevices(fixture));typeof(WorkbenchWindow).GetField("storageGate",Hidden).SetValue(window,new object());typeof(WorkbenchWindow).GetField("fileReadSlots",Hidden).SetValue(window,slots);typeof(WorkbenchWindow).GetField("fileLifetime",Hidden).SetValue(window,lifetime);
 var task=(Task<object>)typeof(WorkbenchWindow).GetMethod("StorageRequest",Hidden).Invoke(window,new object[]{"storage.directories",new Dictionary<string,object>{{"deviceId","local"},{"path",fixture}}});var result=task.GetAwaiter().GetResult();if((string)Get(result,"path")!=fixture||(string)Get(result,"pathStyle")!="windows-local")throw new Exception("wrong local namespace");var roots=((IEnumerable)Get(result,"roots")).Cast<object>().Select(x=>(string)Get(x,"path")).ToArray();var actual=DriveInfo.GetDrives().Where(x=>x.IsReady).Select(x=>x.RootDirectory.FullName).ToArray();if(!roots.SequenceEqual(actual))throw new Exception("drive list does not match ready local drives");var items=((IEnumerable)Get(result,"items")).Cast<object>().ToArray();if(items.Length!=1||(string)Get(items[0],"name")!="models")throw new Exception("directory picker must list only folders");Console.WriteLine("PASS actual storage.directories bridge returns fixture folders and real ready drive roots: "+string.Join(",",roots));return 0;
 }catch(Exception ex){Console.WriteLine("FAIL actual storage bridge: "+ex);return 1;}finally{lifetime.Dispose();slots.Dispose();if(Path.GetDirectoryName(fixture)==Path.GetTempPath().TrimEnd('\\'))Directory.Delete(fixture,true);}}
}
