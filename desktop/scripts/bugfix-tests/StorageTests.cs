using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
public static class DeviceRegistry {public static string Id(Server s){return s.IsLocal?"local":s.Host;}}
public static class StorageRegressionTests {
 static int failed;static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
 static void Equal(string expected,string actual){if(expected!=actual)throw new Exception("expected "+expected+", got "+actual);}
 static string Resolve(bool local,string home,string input){
#if BASELINE
 return string.IsNullOrWhiteSpace(input)?home:FileTransferCopy.Normalize(local,input);
#else
 return StoragePaths.Resolve(local,home,input);
#endif
 }
 static void Reject(bool local,string home,string value){try{Resolve(local,home,value);}catch(IOException){return;}throw new Exception("unsafe path accepted: "+value);}
 public static int Main(){
 Check("bare local D: selects D drive root",()=>Equal(@"D:\",Resolve(true,@"C:\Users\test","D:")));
 Check("local drive subfolder stays absolute",()=>Equal(@"D:\Data",Resolve(true,@"C:\Users\test",@"D:\Data")));
 Check("Windows SFTP accepts bare D drive root",()=>Equal("/D:/",Resolve(false,"/C:/Users/test","D:")));
 Check("Windows SFTP translates absolute drive folder",()=>Equal("/D:/Data",Resolve(false,"/C:/Users/test",@"D:\Data")));
 Check("Linux SFTP retains remote absolute paths",()=>Equal("/var/data",Resolve(false,"/home/test","/var/data")));
 Check("Linux SFTP rejects Windows drive paths",()=>Reject(false,"/home/test",@"D:\Data"));
 Check("drive-relative path rejected",()=>Reject(true,@"C:\Users\test",@"D:Data"));
 Check("remote traversal rejected",()=>Reject(false,"/C:/Users/test",@"D:\..\Data"));
 Check("device namespace rejected",()=>Reject(true,@"C:\Users\test",@"\\?\D:\Data"));
 Check("local scan snapshot saves and reloads independently",()=>{
 string fixture=Path.Combine(Path.GetTempPath(),"asterhub-storage-regression-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
 try{Directory.CreateDirectory(Path.Combine(fixture,"models"));File.WriteAllBytes(Path.Combine(fixture,"models","model.bin"),new byte[4096]);File.WriteAllText(Path.Combine(fixture,"readme.txt"),"test");var job=new StorageScanJob(new Server{IsLocal=true},fixture);job.Run().GetAwaiter().GetResult();Equal("ready",job.Status);if(job.Result.Node("0").Count!=2)throw new Exception("wrong file count");string index=Path.Combine(fixture,"state","snapshot.json.gz");job.Result.Save(index);var loaded=StorageSnapshot.Load(index);if(loaded==null||loaded.Id!=job.Result.Id||loaded.Node("0").Bytes!=job.Result.Node("0").Bytes||loaded.Node("0").Count!=2)throw new Exception("saved scan was lost");}
 finally{if(Path.GetDirectoryName(fixture)!=Path.GetTempPath().TrimEnd('\\'))throw new Exception("fixture escaped temp");Directory.Delete(fixture,true);}
 });
 Check("unreadable directories are counted separately and don't hide readable siblings",()=>{
  string root=Path.Combine(Path.GetTempPath(),"asterhub-scan-acl-"+Guid.NewGuid().ToString("N"));string locked=Path.Combine(root,"locked");Directory.CreateDirectory(locked);
  var restricted=Directory.GetAccessControl(locked);
  var user=System.Security.Principal.WindowsIdentity.GetCurrent().User;
  var deny=new System.Security.AccessControl.FileSystemAccessRule(user,System.Security.AccessControl.FileSystemRights.ListDirectory,System.Security.AccessControl.AccessControlType.Deny);restricted.AddAccessRule(deny);
  try{File.WriteAllText(Path.Combine(locked,"inaccessible.txt"),"private");File.WriteAllText(Path.Combine(root,"readable.txt"),"test");Directory.SetAccessControl(locked,restricted);
   var job=new StorageScanJob(new Server{IsLocal=true},root);job.Run().GetAwaiter().GetResult();Equal("ready",job.Status);
   if(!job.Result.Partial||job.Result.AccessDenied!=1||job.Result.EntryLimitReached||job.Result.Node("0").Count!=1)throw new Exception("permission limit was lost or a readable sibling was skipped");
  }finally{restricted.RemoveAccessRuleSpecific(deny);Directory.SetAccessControl(locked,restricted);if(Path.GetDirectoryName(root)!=Path.GetTempPath().TrimEnd('\\'))throw new Exception("fixture escaped temp");Directory.Delete(root,true);}
 });
 Check("saving remains active and cancel keeps the previous index",()=>{
  string root=Path.Combine(Path.GetTempPath(),"asterhub-scan-save-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string index=Path.Combine(root,"index.json.gz");
  try{File.WriteAllText(Path.Combine(root,"readable.txt"),"test");var previous=new StorageScanJob(new Server{IsLocal=true},root);previous.Run().GetAwaiter().GetResult();previous.Result.Save(index);
   var pending=new StorageScanJob(new Server{IsLocal=true},root);bool staged=false;
   pending.Run((snapshot,token)=>snapshot.SaveAsync(index,token,commit=>{Equal("aggregating",pending.Status);staged=true;pending.Cancel.Cancel();commit();return Task.FromResult(0);})).GetAwaiter().GetResult();
   if(!staged)throw new Exception("index wasn't staged");Equal("cancelled",pending.Status);Equal(previous.Result.Id,StorageSnapshot.Load(index).Id);if(Directory.GetFiles(root,"*.tmp").Length!=0)throw new Exception("staged file was not cleaned up");
  }finally{if(Path.GetDirectoryName(root)!=Path.GetTempPath().TrimEnd('\\'))throw new Exception("fixture escaped temp");Directory.Delete(root,true);}
 });return failed==0?0:1;
 }
}
