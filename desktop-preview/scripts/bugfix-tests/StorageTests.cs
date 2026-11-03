using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
public static class PreviewDevices {public static string Id(Server s){return s.IsLocal?"local":s.Host;}}
public static class StorageRegressionTests {
 static int failed;static void Check(string name,Action test){try{test();Console.WriteLine("PASS "+name);}catch(Exception ex){failed++;Console.WriteLine("FAIL "+name+": "+ex.Message);}}
 static void Equal(string expected,string actual){if(expected!=actual)throw new Exception("expected "+expected+", got "+actual);}
 static string Resolve(bool local,string home,string input){
#if BASELINE
 return string.IsNullOrWhiteSpace(input)?home:PreviewFileCopy.Normalize(local,input);
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
 });return failed==0?0:1;
 }
}
