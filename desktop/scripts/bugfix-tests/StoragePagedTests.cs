using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;using System.Threading;using System.Web.Script.Serialization;
public static class StoragePagedTests {
 static JavaScriptSerializer json=new JavaScriptSerializer();static int passed;
 static void Check(bool value,string text){if(!value)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
 static StorageSnapshot Make(){var s=new StorageSnapshot{Id="paged-fixture",DeviceId="local",Root=@"C:\audit",Endpoint="fixture",IsLocal=true,Created=DateTime.UtcNow.AddMinutes(-5),EntryLimit=StorageSnapshot.VolumeEntryLimit};var date=DateTime.UtcNow.AddDays(-30);s.Nodes.Add("0",new StorageNode{Id="0",Name="audit",Path=s.Root,Directory=true,Stamp=date});for(int i=1;i<4;i++){var id=i.ToString();s.Nodes.Add(id,new StorageNode{Id=id,Parent="0",Directory=true,Name="folder"+i,Path=s.Root+"\\folder"+i,Stamp=date});s.Nodes["0"].Children.Add(id);}for(int i=4;i<550;i++){string id=i.ToString(),parent=i<500?"0":(i%3+1).ToString(),name=(i%11==0?"O'Reilly_100%内存":i%7==0?"Δata":"sample")+i+(i%5==0?".onnx":i%3==0?".zip":".dat"),path=s.Nodes[parent].Path+"\\"+name;s.Nodes.Add(id,new StorageNode{Id=id,Parent=parent,Name=name,Path=path,Bytes=i%13*100,Stamp=date});s.Nodes[parent].Children.Add(id);}s.Aggregate();return s;}
 static Dictionary<string,object> Args(params object[] values){var p=new Dictionary<string,object>();for(int i=0;i<values.Length;i+=2)p.Add((string)values[i],values[i+1]);return p;}
 static object Expected(StorageSnapshot s,Dictionary<string,object> p){return typeof(WorkbenchWindow).GetMethod("QueryStorage",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,p});}
 static void Same(StorageSnapshot s,StoragePagedIndex pages,Dictionary<string,object> p,string name){string a=json.Serialize(Expected(s,p)),b=json.Serialize(pages.Query(p,CancellationToken.None));if(a!=b){Console.WriteLine(a.Substring(0,Math.Min(400,a.Length)));Console.WriteLine(b.Substring(0,Math.Min(400,b.Length)));}Check(a==b,name);}
 static Dictionary<string,object> Dict(object value){return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(new JavaScriptSerializer().Serialize(value));}
 public static int Main(){string dir=Path.Combine(Path.GetTempPath(),"asterhub-paged-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);try{string file=Path.Combine(dir,"snapshot.gz");var s=Make();s.Save(file);s=StorageSnapshot.Load(file);
  using(var pages=StoragePagedIndex.Open(file,CancellationToken.None)){
   Same(s,pages,Args(),"root rows, bytes, counts, breadcrumbs and ordinal tie order match graph reader");Same(s,pages,Args("offset",200),"second page matches graph reader");Same(s,pages,Args("nodeId","2"),"folder navigation and breadcrumbs match");Same(s,pages,Args("mode","large"),"largest file order preserves volume parent/name ties");Same(s,pages,Args("mode","large","offset",200,"minBytes",200),"largest page and minimum size match");Same(s,pages,Args("mode","large","kind","model"),"file kind filter matches");Same(s,pages,Args("query","O'Reilly_100%"),"quotes, percent and underscore in searches remain literal");Same(s,pages,Args("mode","large","query","δATA"),"Unicode ordinal case-insensitive search matches");Same(s,pages,Args("foldersOnly",true),"folder-only map requests match");Same(s,pages,Args("excludeIds",new[]{"1","2","12"}),"aggregate tile exclusions match");Same(s,pages,Args("olderThanDays",15),"UTC age filter matches");
   var e=new StorageTrashEntry{DeviceId="local",Endpoint="fixture",Original=s.Nodes["2"].Path,Bytes=s.Nodes["2"].Bytes,Directory=true,State="moved",Created=DateTime.UtcNow};var entries=new List<StorageTrashEntry>{e};typeof(WorkbenchWindow).GetMethod("ApplyTrashToSnapshot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,entries});pages.ApplyRemoved(new[]{new StorageSummaryRemoval{Path=e.Original,Bytes=e.Bytes,Directory=true}},CancellationToken.None);Same(s,pages,Args(),"recycle hides subtree and adjusts root totals without loading full graph");Same(s,pages,Args("mode","large"),"recycle excludes descendants from largest files");
   e.Restored=true;typeof(WorkbenchWindow).GetMethod("ApplyTrashToSnapshot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{s,entries});pages.ApplyRemoved(new StorageSummaryRemoval[0],CancellationToken.None);Same(s,pages,Args(),"restore recovers exact aggregates and row order");
  }
  using(var again=StoragePagedIndex.Open(file,CancellationToken.None))Same(s,again,Args(),"derived index reopens without rebuilding graph");
  var changed=Make();changed.Id="new-snapshot";changed.Save(file);using(var again=StoragePagedIndex.Open(file,CancellationToken.None))Check(again.Metadata.Id==changed.Id,"source revision invalidates derived index");
  string oldId=changed.Id;File.WriteAllText(file+".pages.sqlite","broken derived file");using(var again=StoragePagedIndex.Open(file,CancellationToken.None))Check(again.Metadata.Id==oldId,"corrupt derived index rebuilds from intact authoritative scan");
  using(var corrupt=new PageSql(file+".pages.sqlite"))corrupt.Exec("DROP TABLE nodes;");
  using(var again=StoragePagedIndex.Open(file,CancellationToken.None))Check(again.Metadata.Id==oldId&&again.Query(Args(),CancellationToken.None)!=null,"readable metadata with missing nodes rebuilds derived cache");
  var cancelBuild=new CancellationTokenSource();cancelBuild.Cancel();var newer=Make();newer.Id="cancelled-rebuild";newer.Save(file);bool wasCancelled=false;
  try{using(var ignored=StoragePagedIndex.Open(file,cancelBuild.Token)){}}catch(OperationCanceledException){wasCancelled=true;}
  Check(wasCancelled&&StorageSnapshot.Load(file).Id==newer.Id&&!Directory.GetFiles(dir,"*.tmp").Any(),"cancelled derived import preserves source and removes temporary file");
  using(var again=StoragePagedIndex.Open(file,CancellationToken.None))Check(again.Metadata.Id==newer.Id,"cancelled migration can be retried successfully");
  var special=Make();special.Root=@"C:\audit";string original=special.Nodes["2"].Path;
  foreach(var n in special.Nodes.Values){if(n.Id=="2"||n.Parent=="2")n.Path=n.Path.Replace(original,special.Root+@"\100%_folder");}special.Nodes["2"].Name="100%_folder";
  special.Save(file);special=StorageSnapshot.Load(file);
  using(var pages=StoragePagedIndex.Open(file,CancellationToken.None)){
   string fid=special.Nodes.Values.First(n=>!n.Directory&&n.Parent=="2").Id;var fn=special.Nodes[fid];fn.Removed=true;special.Aggregate();pages.ApplyRemoved(new[]{new StorageSummaryRemoval{Path=fn.Path,Bytes=fn.Bytes}},CancellationToken.None);
   Same(special,pages,Args("nodeId","2"),"single file recycle works beneath literal percent and underscore directory");
   fn.Removed=false;special.Aggregate();pages.ApplyRemoved(new StorageSummaryRemoval[0],CancellationToken.None);Same(special,pages,Args(),"single file restore recovers aggregate totals");
  }
  foreach(bool local in new[]{true,false}){
   var remote=Make();remote.IsLocal=local;remote.EntryLimit=120000;remote.Root=local?@"C:\audit":"/audit";
   foreach(var n in remote.Nodes.Values)n.Path=local?n.Path:n.Path.Replace(@"C:\audit","/audit").Replace('\\','/');
   remote.Save(file);remote=StorageSnapshot.Load(file);using(var pages=StoragePagedIndex.Open(file,CancellationToken.None)){Same(remote,pages,Args("mode","large"),local?"non-volume Windows full-path ordinal tie order matches":"SFTP path reconstruction and largest tie order match");}
  }
  var posix=Make();posix.IsLocal=false;posix.EntryLimit=120000;posix.Root="/audit";
  foreach(var n in posix.Nodes.Values)n.Path=n.Path.Replace(@"C:\audit","/audit").Replace('\\','/');
  string oldFolder=posix.Nodes["2"].Path;foreach(var n in posix.Nodes.Values)if(n.Id=="2"||n.Parent=="2")n.Path=n.Path.Replace(oldFolder,oldFolder+"\\");posix.Nodes["2"].Name+="\\";
  string posixFile=posix.Nodes.Values.First(n=>!n.Directory&&n.Parent=="2").Id;posix.Nodes[posixFile].Name+="\\name";posix.Nodes[posixFile].Path+="\\name";posix.Save(file);posix=StorageSnapshot.Load(file);
  using(var pages=StoragePagedIndex.Open(file,CancellationToken.None)){
   Check(pages.Node(posixFile).Path==posix.Nodes[posixFile].Path,"POSIX backslashes in directory and filename retain exact path identity");
   Same(posix,pages,Args("mode","large","query","folder2\\"),"POSIX backslash path search matches original reader");
   var originalNode=posix.Nodes[posixFile];pages.ApplyRemoved(new[]{new StorageSummaryRemoval{Path=originalNode.Path,Bytes=originalNode.Bytes}},CancellationToken.None);bool hidden=false;try{pages.Node(posixFile);}catch(IOException){hidden=true;}Check(hidden,"POSIX file recycle locates parent without normalizing legal backslashes");
   pages.ApplyRemoved(new StorageSummaryRemoval[0],CancellationToken.None);Check(pages.Node(posixFile).Path==originalNode.Path,"POSIX restore retains original filename");
  }
  var zeros=Make();for(int i=550;i<20550;i++){string id=i.ToString();zeros.Nodes.Add(id,new StorageNode{Id=id,Parent="0",Name="zero"+i+".dat",Path=zeros.Root+"\\zero"+i+".dat",Stamp=DateTime.UtcNow.AddDays(-10)});zeros.Nodes["0"].Children.Add(id);}foreach(var n in zeros.Nodes.Values)if(!n.Directory)n.Bytes=0;zeros.Aggregate();zeros.Save(file);zeros=StorageSnapshot.Load(file);
  using(var pages=StoragePagedIndex.Open(file,CancellationToken.None))Same(zeros,pages,Args("mode","large","offset",200),"large all-zero tie group retains bounded result pages and exact ordering");
  {
   string previousRoot=WorkbenchHost.DataRoot;WorkbenchHost.DataRoot=dir;
   try{
    var owner=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(WorkbenchWindow));var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
    var saveTrash=typeof(WorkbenchWindow).GetMethod("SaveStorageTrash",flags);var readTrash=typeof(WorkbenchWindow).GetMethod("ReadStorageTrash",flags);var entries=new List<StorageTrashEntry>{new StorageTrashEntry{Id="fixture"}};
    saveTrash.Invoke(owner,new object[]{entries});string trashFile=Path.Combine(dir,"Storage","recycle.json");System.Threading.Tasks.Task publishTrash;
    lock(WorkbenchWindow.StorageTrashFileGate){using(var held=File.OpenRead(trashFile)){publishTrash=System.Threading.Tasks.Task.Run(()=>saveTrash.Invoke(owner,new object[]{entries}));Check(!publishTrash.Wait(75),"recycle manifest publication waits for concurrent status read");}}
    publishTrash.GetAwaiter().GetResult();Check(((List<StorageTrashEntry>)readTrash.Invoke(owner,null)).Count==1,"recycle manifest remains readable after atomic replacement");
   }finally{WorkbenchHost.DataRoot=previousRoot;}
  }
  {
   string prepareFile=Path.Combine(dir,"preparation.gz");Make().Save(prepareFile);
   Check(!StoragePagedIndex.IsCurrent(prepareFile,CancellationToken.None),"missing derived cache requires background preparation");
   long progressCount=0;string phase="";using(var index=StoragePagedIndex.Open(prepareFile,CancellationToken.None,(count,text)=>{progressCount=count;phase=text;})){}
   Check(progressCount>0&&phase=="正在整理查询目录","index build publishes real item count and final sorting phase");
   Check(StoragePagedIndex.IsCurrent(prepareFile,CancellationToken.None),"ready derived cache is reused without rebuilding");
   var changedPrepare=Make();changedPrepare.Id="prepare-new";changedPrepare.Save(prepareFile);
   Check(!StoragePagedIndex.IsCurrent(prepareFile,CancellationToken.None),"new snapshot invalidates background preparation revision");
   var slots=new SemaphoreSlim(0,1);var preparation=new StorageIndexPreparation("local",prepareFile,"fixture",@"C:\",CancellationToken.None);preparation.Start(slots);
   Check(Convert.ToString(Dict(preparation.View())["status"])=="queued","preparation waits in queue before consuming query slot");slots.Release();preparation.Work.GetAwaiter().GetResult();preparation.ThrowIfFailed();preparation.Cancel();
   Check(Convert.ToString(Dict(preparation.View())["status"])=="ready"&&Dict(preparation.View())["finished"]!=null,"background preparation retains completed notification and finish time");
   using(var overlay=StoragePagedIndex.Open(prepareFile,CancellationToken.None))overlay.ApplyRemoved(new StorageSummaryRemoval[0],CancellationToken.None);preparation.RememberValidatedRevision();
   Check(preparation.Ready,"legitimate recycle overlay updates preserve ready preparation without duplicate tasks");
   using(var corrupt=new PageSql(prepareFile+".pages.sqlite",CancellationToken.None))corrupt.Exec("DROP TABLE nodes;");
   Check(!preparation.Ready&&!StoragePagedIndex.IsCurrent(prepareFile,CancellationToken.None),"readable metadata cannot mark a damaged derived cache ready");
   var repairing=new StorageIndexPreparation("local",prepareFile,"fixture",@"C:\",CancellationToken.None);repairing.Start(slots);repairing.Work.GetAwaiter().GetResult();repairing.ThrowIfFailed();
   Check(repairing.Ready,"background preparation validates and repairs partial cache corruption");
   var stopped=new StorageIndexPreparation("local",prepareFile,"fixture",@"C:\",CancellationToken.None);slots.Wait();stopped.Start(slots);stopped.Cancel();stopped.Work.GetAwaiter().GetResult();
   Check(Convert.ToString(Dict(stopped.View())["status"])=="cancelled","shutdown cancellation stops queued preparation without acquiring slot");slots.Release();slots.Dispose();
  }
  var c=new CancellationTokenSource();c.Cancel();bool cancelled=false;try{StoragePagedIndex.Open(file+".missing",c.Token);}catch(FileNotFoundException){cancelled=true;}Check(cancelled,"missing source never returns an empty successful result");
 }finally{Directory.Delete(dir,true);}Console.WriteLine("PAGED_INDEX_TESTS="+passed+" PASS");return 0;}
}
