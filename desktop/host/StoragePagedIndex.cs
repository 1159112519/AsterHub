using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

// A disposable, bounded page cache over a derived index. The gzip snapshot remains
// authoritative. No third-party SQLite binary is shipped: Windows supplies winsqlite3.
internal sealed class StoragePagedIndex:IDisposable {
 readonly PageSql db;internal readonly StorageSnapshot Metadata;
 static readonly object verifyGate=new object();
 static readonly Dictionary<string,string> verified=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 string FilePathSql{get{return "rtrim(p.path,'"+(Metadata.IsLocal?"/\\":"/")+"') || '"+(Metadata.IsLocal?"\\":"/")+"' || n.name";}}
 string Columns{get{return "n.id,n.parent,n.name,CASE WHEN n.directory=1 THEN n.path ELSE "+FilePathSql+" END,n.directory,n.bytes,n.stamp,n.incomplete,n.count";}}
 const string FromNodes=" FROM nodes n LEFT JOIN nodes p ON n.parent=p.id ";
 StoragePagedIndex(string file,CancellationToken token){db=new PageSql(file,token);try{using(var q=db.Prepare("SELECT header FROM meta WHERE id=1")){if(!q.Row())throw new IOException("分页索引缺少元数据");Metadata=StorageSnapshot.ReadHeader(new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(q.Text(0)));}}catch{db.Dispose();throw;}}
 internal static bool Supported(string file){if(!File.Exists(file))return false;using(var input=File.OpenRead(file))using(var zip=new GZipStream(input,CompressionMode.Decompress))return zip.ReadByte()=='{'&&zip.ReadByte()=='\n';}
 internal static bool IsCurrent(string source,CancellationToken token){
  string index=source+".pages.sqlite";if(!File.Exists(source)||!File.Exists(index))return false;
  var info=new FileInfo(source);try{using(var sql=new PageSql(index,token))using(var q=sql.Prepare("SELECT version,source_length,source_stamp FROM meta WHERE id=1"))return q.Row()&&q.Int64(0)==3&&q.Int64(1)==info.Length&&q.Int64(2)==info.LastWriteTimeUtc.Ticks&&ValidateDerived(sql,index,token);}catch(PageSqlException e){if(e.Code==1||e.Code==11||e.Code==17||e.Code==26)return false;throw;}
 }
 internal static StoragePagedIndex Open(string source,CancellationToken token,Action<long,string> progress=null){
  string index=source+".pages.sqlite";var info=new FileInfo(source);long length=info.Length,stamp=info.LastWriteTimeUtc.Ticks;bool valid=false;
  if(File.Exists(index))try{using(var old=new PageSql(index,token)){
   using(var q=old.Prepare("SELECT version,source_length,source_stamp FROM meta WHERE id=1"))valid=q.Row()&&q.Int64(0)==3&&q.Int64(1)==length&&q.Int64(2)==stamp;
   if(valid)valid=ValidateDerived(old,index,token);
  }}catch(PageSqlException error){if(error.Code!=1&&error.Code!=11&&error.Code!=17&&error.Code!=26)throw;valid=false;}
  if(!valid){string temp=index+"."+Guid.NewGuid().ToString("N")+".tmp";try{Build(source,temp,length,stamp,token,progress);var latest=new FileInfo(source);if(latest.Length!=length||latest.LastWriteTimeUtc.Ticks!=stamp)throw new IOException("扫描结果已更新，请重试");token.ThrowIfCancellationRequested();if(File.Exists(index))File.Replace(temp,index,null);else File.Move(temp,index);}finally{if(File.Exists(temp))File.Delete(temp);}}
  return new StoragePagedIndex(index,token);
 }
 static bool ValidateDerived(PageSql sql,string path,CancellationToken token){
  // Validate each on-disk revision once, including partial corruption with readable meta.
  // Cancellation, access errors and disk exhaustion propagate instead of triggering rebuilds.
  var f=new FileInfo(path);string revision=f.Length+":"+f.LastWriteTimeUtc.Ticks;string previous;
  lock(verifyGate)if(verified.TryGetValue(path,out previous)&&previous==revision)return true;
  using(var schema=sql.Prepare("SELECT id,parent,name,path,path_key,directory,bytes,base_bytes,stamp,ticks,incomplete,count,base_count,removed,kind FROM nodes LIMIT 0"))schema.Row();
  using(var indexes=sql.Prepare("SELECT count(*) FROM sqlite_master WHERE type='index' AND name IN ('children','paths','largest')")){indexes.Row();if(indexes.Int64(0)!=3)return false;}
  using(var check=sql.Prepare("PRAGMA quick_check(1)")){if(!check.Row()||check.Text(0)!="ok")return false;}
  token.ThrowIfCancellationRequested();lock(verifyGate){if(verified.Count>=16)verified.Clear();verified[path]=revision;}return true;
 }
 sealed class DirectoryCount {internal long Parent;internal int Count;internal bool Defined;}
 static void Build(string source,string target,long length,long stamp,CancellationToken token,Action<long,string> progress){
  var json=new JavaScriptSerializer{RecursionLimit=20};var directories=new Dictionary<long,DirectoryCount>();
  using(var db=new PageSql(target,token))using(var input=File.OpenRead(source))using(var zip=new GZipStream(input,CompressionMode.Decompress))using(var reader=new StreamReader(zip)){
   if(reader.ReadLine()!="{")throw new IOException("扫描索引格式不受支持");string line=reader.ReadLine();if(line==null||!line.EndsWith(",",StringComparison.Ordinal)||reader.ReadLine()!="\"nodes\":[")throw new IOException("扫描索引头无效");string header="{"+line.Substring(0,line.Length-1)+"}";var metadata=StorageSnapshot.ReadHeader(json.Deserialize<Dictionary<string,object>>(header));
   db.Exec("PRAGMA journal_mode=OFF;PRAGMA synchronous=OFF;BEGIN;CREATE TABLE meta(id INTEGER PRIMARY KEY,version INTEGER,source_length INTEGER,source_stamp INTEGER,header TEXT,overlay TEXT);CREATE TABLE nodes(id TEXT PRIMARY KEY,parent TEXT,name TEXT,path TEXT,path_key TEXT,directory INTEGER,bytes INTEGER,base_bytes INTEGER,stamp TEXT,ticks INTEGER,incomplete INTEGER,count INTEGER DEFAULT 0,base_count INTEGER DEFAULT 0,removed INTEGER DEFAULT 0,kind TEXT);");
   using(var meta=db.Prepare("INSERT INTO meta VALUES(1,3,?,?,?,'')")){meta.Bind(1,length);meta.Bind(2,stamp);meta.Bind(3,header);meta.Done();}
   bool ended=false;int rows=0;
   using(var insert=db.Prepare("INSERT INTO nodes(id,parent,name,path,path_key,directory,bytes,base_bytes,stamp,ticks,incomplete,kind) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)")){
    while((line=reader.ReadLine())!=null){token.ThrowIfCancellationRequested();if(line=="]}"){ended=true;break;}if(line.EndsWith(",",StringComparison.Ordinal))line=line.Substring(0,line.Length-1);if(line.Length==0)continue;var v=json.Deserialize<Dictionary<string,object>>(line);string id=Convert.ToString(v["id"]),parent=v["parent"]==null?null:Convert.ToString(v["parent"]),name=Convert.ToString(v["name"]),path=Convert.ToString(v["path"]);bool directory=Convert.ToBoolean(v["directory"]);long bytes=Convert.ToInt64(v["bytes"]);var date=DateTime.Parse(Convert.ToString(v["stamp"]),null,System.Globalization.DateTimeStyles.RoundtripKind);long numeric=long.Parse(id);DirectoryCount count;
     if(directory){if(!directories.TryGetValue(numeric,out count))directories.Add(numeric,count=new DirectoryCount());count.Parent=parent==null?-1:long.Parse(parent);count.Defined=true;}
     else if(parent!=null){long key=long.Parse(parent);if(!directories.TryGetValue(key,out count))directories.Add(key,count=new DirectoryCount());count.Count++;}

     insert.Bind(1,id);insert.Bind(2,parent);insert.Bind(3,name);insert.Bind(4,directory?path:null);insert.Bind(5,directory?PathKey(path,metadata.IsLocal):null);insert.Bind(6,directory?1:0);insert.Bind(7,bytes);insert.Bind(8,bytes);insert.Bind(9,date.ToString("o"));insert.Bind(10,date.ToUniversalTime().Ticks);insert.Bind(11,Convert.ToBoolean(v["incomplete"])?1:0);insert.Bind(12,Kind(name,directory));insert.Done();insert.Reset();rows++;if(progress!=null&&rows%4096==0)progress(rows,"正在构建索引");
    }
   }
   if(!ended||rows==0||!directories.ContainsKey(0))throw new IOException("扫描索引未完整写入");
   foreach(var item in directories.OrderByDescending(x=>x.Key)){token.ThrowIfCancellationRequested();if(!item.Value.Defined)throw new IOException("扫描索引目录不完整");DirectoryCount parent;if(item.Value.Parent>=0&&directories.TryGetValue(item.Value.Parent,out parent))parent.Count+=item.Value.Count;}
   using(var update=db.Prepare("UPDATE nodes SET count=?,base_count=? WHERE id=?"))foreach(var item in directories){token.ThrowIfCancellationRequested();update.Bind(1,item.Value.Count);update.Bind(2,item.Value.Count);update.Bind(3,item.Key.ToString());update.Done();update.Reset();}
   if(progress!=null)progress(rows,"正在整理查询目录");token.ThrowIfCancellationRequested();db.Exec("CREATE INDEX children ON nodes(parent,removed,bytes DESC,id);CREATE INDEX paths ON nodes(path_key) WHERE directory=1;CREATE INDEX largest ON nodes(directory,removed,bytes DESC);COMMIT;");
  }
 }
 static string PathKey(string path,bool local){string p=(local?(path??"").Replace('\\','/'):(path??"")).TrimEnd('/');return local?p.ToUpperInvariant():p;}
 internal static string Kind(string name,bool directory){if(directory)return "folder";string ext=System.IO.Path.GetExtension(name).ToLowerInvariant();if(new[]{".zip",".7z",".tar",".gz",".rar",".xz"}.Contains(ext))return "archive";if(new[]{".pt",".pth",".onnx",".safetensors",".ckpt",".bin"}.Contains(ext))return "model";if(new[]{".mp4",".mkv",".mov",".png",".jpg",".jpeg",".webp"}.Contains(ext))return "media";if(new[]{".db",".sqlite",".sqlite3",".mdb",".mdf",".log"}.Contains(ext))return "data";return "file";}
 StorageNode Read(PageSql.Statement q){return new StorageNode{Id=q.Text(0),Parent=q.Text(1),Name=q.Text(2),Path=q.Text(3),Directory=q.Int64(4)!=0,Bytes=q.Int64(5),Stamp=DateTime.Parse(q.Text(6),null,System.Globalization.DateTimeStyles.RoundtripKind),Incomplete=q.Int64(7)!=0,Count=(int)q.Int64(8)};}
 internal StorageNode Node(string id){using(var q=db.Prepare("SELECT "+Columns+FromNodes+"WHERE n.id=? AND n.removed=0")){q.Bind(1,id??"0");if(!q.Row())throw new IOException("此项目已不在当前扫描结果中，请重新扫描");return Read(q);}}
 internal void ApplyRemoved(IEnumerable<StorageSummaryRemoval> entries,CancellationToken token){
  var paths=entries.Select(x=>PathKey(x.Path,Metadata.IsLocal)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();string version=new JavaScriptSerializer().Serialize(paths);using(var q=db.Prepare("SELECT overlay FROM meta WHERE id=1"))if(q.Row()&&q.Text(0)==version)return;
  db.Exec("BEGIN;");try{
   db.Exec("UPDATE nodes SET removed=0,bytes=base_bytes,count=base_count WHERE removed<>0 OR bytes<>base_bytes OR count<>base_count;");
   foreach(string path in paths){token.ThrowIfCancellationRequested();StorageNode node=null;using(var q=db.Prepare("SELECT "+Columns+FromNodes+"WHERE n.directory=1 AND n.path_key=? AND n.removed=0")){q.Bind(1,path);if(q.Row())node=Read(q);}
    if(node==null){int cut=path.LastIndexOf('/');if(cut>=0)using(var q=db.Prepare("SELECT "+Columns+FromNodes+"WHERE p.directory=1 AND p.path_key=? AND n.name COLLATE "+(Metadata.IsLocal?"AH_IGNORECASE":"AH_ORDINAL")+"=? AND n.removed=0")){q.Bind(1,path.Substring(0,cut));q.Bind(2,path.Substring(cut+1));if(q.Row())node=Read(q);}}
    if(node==null||node.Parent==null)continue;
    // A file stores only its parent and name. Only directories store full paths.
    if(node.Directory){string prefix=path+"/";using(var q=db.Prepare("UPDATE nodes SET removed=1 WHERE directory=1 AND (path_key=? OR (path_key>=? AND path_key<?))")){q.Bind(1,path);q.Bind(2,prefix);q.Bind(3,path+"0");q.Done();}db.Exec("UPDATE nodes SET removed=1 WHERE removed=0 AND parent IN(SELECT id FROM nodes WHERE directory=1 AND removed=1);");}
    else using(var q=db.Prepare("UPDATE nodes SET removed=1 WHERE id=?")){q.Bind(1,node.Id);q.Done();}
    string parent=node.Parent;while(parent!=null){var ancestor=Node(parent);using(var q=db.Prepare("UPDATE nodes SET bytes=MAX(0,bytes-?),count=MAX(0,count-?) WHERE id=?")){q.Bind(1,node.Bytes);q.Bind(2,node.Directory?node.Count:1);q.Bind(3,parent);q.Done();}parent=ancestor.Parent;}
   }
   using(var q=db.Prepare("UPDATE meta SET overlay=? WHERE id=1")){q.Bind(1,version);q.Done();}db.Exec("COMMIT;");
  }catch{db.Exec("ROLLBACK;");throw;}
 }
 internal object Query(Dictionary<string,object> p,CancellationToken token){
  Func<string,string,string> text=(key,fallback)=>p.ContainsKey(key)?Convert.ToString(p[key]):fallback;
  bool large=text("mode","")=="large",folders=text("foldersOnly","").Equals("true",StringComparison.OrdinalIgnoreCase);string query=text("query","").Trim(),kind=text("kind","");int offset,days;long min;int.TryParse(text("offset","0"),out offset);offset=Math.Max(0,offset);int.TryParse(text("olderThanDays","0"),out days);days=Math.Max(0,Math.Min(36500,days));long.TryParse(text("minBytes","0"),out min);var node=Node(text("nodeId","0"));
  var values=new List<object>();string where="n.removed=0 AND "+(large?"n.directory=0":"n.parent=?");if(!large)values.Add(node.Id);where+=" AND n.bytes>=?";values.Add(min);if(folders)where+=" AND n.directory=1";if(days>0){where+=" AND n.ticks<?";values.Add(DateTime.UtcNow.AddDays(-days).Ticks);}if(kind.Length>0){where+=" AND n.kind=?";values.Add(kind);}if(query.Length>0){where+=" AND (AH_CONTAINS(n.name,?)";values.Add(query);if(large){where+=" OR AH_CONTAINS("+FilePathSql+",?)";values.Add(query);}where+=")";}
  object raw;var excluded=p.TryGetValue("excludeIds",out raw)?raw as IEnumerable:null;if(excluded!=null&&!(excluded is string)){var ids=excluded.Cast<object>().OfType<string>().Distinct(StringComparer.Ordinal).Take(64).ToArray();if(ids.Length>0){where+=" AND n.id NOT IN("+string.Join(",",ids.Select(x=>"?"))+")";values.AddRange(ids);}}
  int total;long bytes;using(var q=db.Prepare("SELECT COUNT(*),COALESCE(SUM(n.bytes),0)"+(large&&query.Length>0?FromNodes:" FROM nodes n ")+"WHERE "+where)){q.BindAll(values);token.ThrowIfCancellationRequested();q.Row();total=(int)q.Int64(0);bytes=q.Int64(1);}
  string order=large?(Metadata.IsLocal&&Metadata.EntryLimit==StorageSnapshot.VolumeEntryLimit?"n.bytes DESC,p.path COLLATE AH_ORDINAL,n.name COLLATE AH_ORDINAL":"n.bytes DESC,("+FilePathSql+") COLLATE AH_ORDINAL"):"n.bytes DESC,n.id";var page=new List<StorageNode>(200);using(var q=db.Prepare("SELECT "+Columns+FromNodes+"WHERE "+where+" ORDER BY "+order+" LIMIT 200 OFFSET ?")){q.BindAll(values);q.Bind(values.Count+1,offset);while(q.Row()){token.ThrowIfCancellationRequested();page.Add(Read(q));}}
  var crumbs=new List<object>();StorageNode cursor=node;var seen=new HashSet<string>();while(cursor!=null){if(!seen.Add(cursor.Id))throw new IOException("扫描目录层级无效");crumbs.Insert(0,cursor.View());cursor=cursor.Parent==null?null:Node(cursor.Parent);}
  return new{snapshotId=Metadata.Id,node=node.View(),crumbs=crumbs.ToArray(),total=total,bytes=bytes,offset=offset,limit=200,items=page.Select(n=>new{id=n.Id,parent=n.Parent,name=n.Name,path=n.Path,directory=n.Directory,bytes=n.Bytes,modified=n.Stamp.ToString("o"),count=n.Count,kind=Kind(n.Name,n.Directory),incomplete=n.Incomplete}).ToArray(),partial=Metadata.Partial,skipped=Metadata.Skipped};
 }
 public void Dispose(){db.Dispose();}
}

internal sealed class PageSqlException:IOException {internal readonly int Code;internal PageSqlException(int code,string message):base(message){Code=code;}}

// Minimal UTF-8 binding to Windows' SQLite. SQL values are always bound parameters.
internal sealed class PageSql:IDisposable {
 IntPtr handle;readonly Compare compare,ignoreCase;readonly Contains contains;readonly Progress progress;readonly CancellationToken cancellation;
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Progress(IntPtr state);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Compare(IntPtr state,int left,IntPtr a,int right,IntPtr b);
 [UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void Contains(IntPtr context,int count,IntPtr args);
 static byte[] Utf8(string s){return Encoding.UTF8.GetBytes(s+'\0');}
 static string Text(IntPtr p,int size){if(p==IntPtr.Zero)return null;var bytes=new byte[size];Marshal.Copy(p,bytes,0,size);return Encoding.UTF8.GetString(bytes);}
 static string Text(IntPtr p){if(p==IntPtr.Zero)return null;int length=0;while(Marshal.ReadByte(p,length)!=0)length++;return Text(p,length);}
 internal PageSql(string path,CancellationToken token=default(CancellationToken)){cancellation=token;int rc=sqlite3_open_v2(Utf8(path),out handle,2|4|0x10000,IntPtr.Zero);try{Check(rc);progress=s=>cancellation.IsCancellationRequested?1:0;sqlite3_progress_handler(handle,10000,progress,IntPtr.Zero);compare=(s,l,a,r,b)=>StringComparer.Ordinal.Compare(Text(a,l),Text(b,r));ignoreCase=(s,l,a,r,b)=>StringComparer.OrdinalIgnoreCase.Compare(Text(a,l),Text(b,r));contains=(c,n,a)=>{try{var x=Marshal.ReadIntPtr(a,0);var y=Marshal.ReadIntPtr(a,IntPtr.Size);string first=Text(sqlite3_value_text(x),sqlite3_value_bytes(x))??"",second=Text(sqlite3_value_text(y),sqlite3_value_bytes(y))??"";sqlite3_result_int(c,first.IndexOf(second,StringComparison.OrdinalIgnoreCase)>=0?1:0);}catch(Exception error){sqlite3_result_error(c,Utf8(error.Message),-1);}};Check(sqlite3_create_collation_v2(handle,Utf8("AH_ORDINAL"),1,IntPtr.Zero,compare,IntPtr.Zero));Check(sqlite3_create_collation_v2(handle,Utf8("AH_IGNORECASE"),1,IntPtr.Zero,ignoreCase,IntPtr.Zero));Check(sqlite3_create_function_v2(handle,Utf8("AH_CONTAINS"),2,1|2048,IntPtr.Zero,contains,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero));Exec("PRAGMA cache_size=-4096;PRAGMA temp_store=FILE;PRAGMA mmap_size=0;PRAGMA busy_timeout=5000;");}catch{Dispose();throw;}}
 internal void Check(int rc){if(rc==9)cancellation.ThrowIfCancellationRequested();if(rc!=0&&rc!=100&&rc!=101)throw new PageSqlException(rc&255,"分页索引: "+Text(sqlite3_errmsg(handle)));}
 internal void Exec(string sql){IntPtr error;int rc=sqlite3_exec(handle,Utf8(sql),IntPtr.Zero,IntPtr.Zero,out error);try{Check(rc);}finally{if(error!=IntPtr.Zero)sqlite3_free(error);}}
 internal Statement Prepare(string sql){IntPtr p;Check(sqlite3_prepare_v2(handle,Utf8(sql),-1,out p,IntPtr.Zero));return new Statement(this,p);}
 public void Dispose(){if(handle!=IntPtr.Zero){sqlite3_close_v2(handle);handle=IntPtr.Zero;}GC.KeepAlive(compare);GC.KeepAlive(ignoreCase);GC.KeepAlive(contains);GC.KeepAlive(progress);}
 internal sealed class Statement:IDisposable {
  readonly PageSql owner;IntPtr statement;internal Statement(PageSql owner,IntPtr value){this.owner=owner;statement=value;}
  internal void Bind(int i,object value){if(value==null)owner.Check(sqlite3_bind_null(statement,i));else if(value is string){var bytes=Encoding.UTF8.GetBytes((string)value+'\0');owner.Check(sqlite3_bind_text(statement,i,bytes,bytes.Length-1,(IntPtr)(-1)));}else owner.Check(sqlite3_bind_int64(statement,i,Convert.ToInt64(value)));}
  internal void BindAll(IEnumerable<object> values){int i=1;foreach(var v in values)Bind(i++,v);}
  internal bool Row(){int rc=sqlite3_step(statement);owner.Check(rc);return rc==100;}
  internal void Done(){int rc=sqlite3_step(statement);owner.Check(rc);if(rc!=101)throw new IOException("分页索引写入状态无效");}
  internal void Reset(){owner.Check(sqlite3_reset(statement));owner.Check(sqlite3_clear_bindings(statement));}
  internal string Text(int i){return PageSql.Text(sqlite3_column_text(statement,i),sqlite3_column_bytes(statement,i));}
  internal long Int64(int i){return sqlite3_column_int64(statement,i);}
  public void Dispose(){if(statement!=IntPtr.Zero){sqlite3_finalize(statement);statement=IntPtr.Zero;}GC.KeepAlive(owner);}
 }
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_progress_handler(IntPtr db,int steps,Progress callback,IntPtr context);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] path,out IntPtr db,int flags,IntPtr vfs);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close_v2(IntPtr db);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_errmsg(IntPtr db);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr context,out IntPtr error);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_free(IntPtr value);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr db,byte[] sql,int size,out IntPtr statement,IntPtr tail);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr statement);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr statement);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_reset(IntPtr statement);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_clear_bindings(IntPtr statement);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr statement,int index,byte[] value,int size,IntPtr destructor);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_int64(IntPtr statement,int index,long value);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_null(IntPtr statement,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr statement,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr statement,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern long sqlite3_column_int64(IntPtr statement,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_create_collation_v2(IntPtr db,byte[] name,int encoding,IntPtr state,Compare callback,IntPtr destroy);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_create_function_v2(IntPtr db,byte[] name,int argc,int flags,IntPtr state,Contains callback,IntPtr step,IntPtr final,IntPtr destroy);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_value_text(IntPtr value);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_value_bytes(IntPtr value);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_result_int(IntPtr context,int value);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern void sqlite3_result_error(IntPtr context,byte[] value,int size);
}
