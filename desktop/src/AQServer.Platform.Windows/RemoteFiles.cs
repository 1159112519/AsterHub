using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Renci.SshNet;

public sealed class FileEntry : System.ComponentModel.INotifyPropertyChanged {
 public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged; public int Depth{get;set;} public bool Expanded{get;set;} public bool Expanding{get;set;} public void Notify(){if(PropertyChanged!=null)PropertyChanged(this,new System.ComponentModel.PropertyChangedEventArgs("DisplayName"));}
 public string Name{get;set;} public string FullPath{get;set;} public bool IsDirectory{get;set;} public bool IsLink{get;set;} public long Size{get;set;} public DateTime Modified{get;set;} public string Permissions{get;set;}
 public string DisplayName{get{return new string(' ',Depth*4)+(IsDirectory?(Expanding?"◌  ":Expanded?"▾  ":"▸  "):IsLink?"↗  ":"    ")+Name;}} public string SizeText{get{return IsDirectory?"—":FileTools.Size(Size);}} public string TypeText{get{return IsLink?"符号链接":IsDirectory?"文件夹":(Path.GetExtension(Name).Length>0?Path.GetExtension(Name).Substring(1).ToUpperInvariant()+" 文件":"文件");}} public string ModifiedText{get{return Modified.ToString("yyyy-MM-dd HH:mm");}}
}
public static class FileTools {
 public static string Size(long n){if(n<1024)return n+" B";if(n<1048576)return (n/1024.0).ToString("0.0")+" KB";if(n<1073741824)return(n/1048576.0).ToString("0.0")+" MB";return(n/1073741824.0).ToString("0.00")+" GB";}
 public static string Join(bool local,string a,string b){return local?Path.Combine(a,b):a.TrimEnd('/')+"/"+b;}
 public static void ValidateWindowsName(string name){
 if(string.IsNullOrWhiteSpace(name)||name=="."||name==".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.IndexOf('/')>=0||name.IndexOf('\\')>=0||name.EndsWith(".")||name.EndsWith(" "))throw new IOException("此名称无法安全保存到 Windows："+name);
 string stem=name.Split('.')[0].TrimEnd(' ');if(Regex.IsMatch(stem,@"^(CON|PRN|AUX|NUL|CLOCK\$|CONIN\$|CONOUT\$|COM[1-9¹²³]|LPT[1-9¹²³])$",RegexOptions.IgnoreCase))throw new IOException("Windows 保留设备名称："+name);
 }
 public static string LocalChild(string root,string parent,string rawName){ValidateWindowsName(rawName);string result=Path.GetFullPath(Path.Combine(parent,rawName));EnsureUnderRoot(root,result);return result;}
 public static void EnsureUnderRoot(string root,string path){string canonicalRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);string canonicalPath=Path.GetFullPath(path);if(!canonicalPath.StartsWith(canonicalRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("目标路径超出选定下载目录。");string cursor=Path.GetDirectoryName(canonicalPath);while(cursor!=null&&cursor.Length>=canonicalRoot.Length){if(Directory.Exists(cursor)&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new IOException("下载目标包含链接目录，无法保证保存范围："+cursor);if(string.Equals(cursor,canonicalRoot,StringComparison.OrdinalIgnoreCase))break;cursor=Path.GetDirectoryName(cursor);}}
 public static void RejectRecursiveLocalCopy(string source,string destination){string src=Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);string dst=Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);if(string.Equals(src,dst,StringComparison.OrdinalIgnoreCase)||dst.StartsWith(src+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("不能将文件夹复制到自身或其子目录。");}
 public static List<Tuple<string,string>> PathParts(bool local,string path){string root=local?Path.GetPathRoot(path):"/";var parts=new List<Tuple<string,string>>{Tuple.Create(local?root:"根目录",root)};string accumulated=root;foreach(var segment in path.Substring(root.Length).Split(local?new[]{'/','\\'}:new[]{'/'},StringSplitOptions.RemoveEmptyEntries)){accumulated=Join(local,accumulated,segment);parts.Add(Tuple.Create(segment,accumulated));}return parts;}
 public static string Parent(bool local,string p){if(local){var d=Directory.GetParent(p);return d==null?p:d.FullName;}var q=p.TrimEnd('/');int n=q.LastIndexOf('/');return n<=0?"/":q.Substring(0,n);}
 public static string Key(Server s){return s.IsLocal?"local":s.Host+":"+s.Port+":"+s.User+":"+s.KeyPath+":"+s.AuthMode+":"+s.ProtectedPassword+":"+(s.Password==null?"saved":SshAuthentication.SessionIdentity(s));}
 public static int Natural(string a,string b){var aa=Regex.Matches(a,@"\d+|\D+");var bb=Regex.Matches(b,@"\d+|\D+");for(int i=0;i<Math.Min(aa.Count,bb.Count);i++){string x=aa[i].Value,y=bb[i].Value;int c;if(char.IsDigit(x[0])&&char.IsDigit(y[0])){x=x.TrimStart('0');y=y.TrimStart('0');c=x.Length.CompareTo(y.Length);if(c==0)c=string.CompareOrdinal(x,y);}else c=string.Compare(x,y,StringComparison.CurrentCultureIgnoreCase);if(c!=0)return c;}return aa.Count.CompareTo(bb.Count);}
 public static List<FileEntry> Sort(IEnumerable<FileEntry> x){var l=x.ToList();l.Sort((a,b)=>a.IsDirectory!=b.IsDirectory?(a.IsDirectory?-1:1):Natural(a.Name,b.Name));return l;}
}
public sealed class FileConnection : IDisposable {
 public readonly Server Device; SftpClient client; readonly List<PrivateKeyFile> keys=new List<PrivateKeyFile>(); public string Home{get;private set;}
 public FileConnection(Server s){Device=s.Copy();}
 static string Arg(string s){return "\""+(s??"").Replace("\\","\\").Replace("\"","\\\"")+"\"";}
 static string Run(string exe,string args){using(var p=new Process{StartInfo=new ProcessStartInfo(exe,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8}}){p.Start();var output=p.StandardOutput.ReadToEndAsync();var err=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(10000)){try{p.Kill();}catch{}throw new IOException("读取 OpenSSH 配置超时");}if(p.ExitCode!=0&&!(exe=="ssh-keygen.exe"&&p.ExitCode==1))throw new IOException("无法读取 OpenSSH 配置；请检查 SSH 安装及配置。");return output.Result;}}
 public async Task Connect(CancellationToken token){if(SshAuthentication.Managed(Device)){client=SshAuthentication.Files(Device);client.BufferSize=131072;client.OperationTimeout=TimeSpan.FromSeconds(30);await client.ConnectAsync(token).ConfigureAwait(false);Home=client.WorkingDirectory;return;}if(Device.IsLocal){Home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);return;} await Task.Run(async()=>{
 token.ThrowIfCancellationRequested();string host=Device.Host,user=Device.User,proxy="",hostAlias="";int port=Device.Port;var ids=new List<string>();var known=new List<string>();string cfg=Run("ssh.exe","-G -p "+port+" "+(string.IsNullOrWhiteSpace(user)?"":"-l "+Arg(user)+" ")+"-- "+Arg(host));
 foreach(var line in cfg.Split('\n')){int n=line.IndexOf(' ');if(n<0)continue;string k=line.Substring(0,n).Trim(),v=line.Substring(n+1).Trim();if(k=="hostname")host=v;else if(k=="user"&&string.IsNullOrWhiteSpace(user))user=v;else if(k=="hostkeyalias")hostAlias=v;else if(k=="port")int.TryParse(v,out port);else if(k=="identityfile")ids.Add(v);else if((k=="proxyjump"||k=="proxycommand")&&v!="none")proxy=v;else if(k=="userknownhostsfile"||k=="globalknownhostsfile")known.AddRange(Regex.Matches(v,"\"[^\"]+\"|\\S+").Cast<Match>().Select(m=>m.Value.Trim('"')));}
 if(proxy.Length>0)throw new NotSupportedException("此配置使用 ProxyJump / ProxyCommand。文件面板暂不支持代理；请用集成终端的系统 sftp 命令。");
 string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(!string.IsNullOrWhiteSpace(Device.KeyPath))ids.Insert(0,Device.KeyPath);var auth=new List<AuthenticationMethod>();foreach(var id in ids.Distinct()){string path=Environment.ExpandEnvironmentVariables(id.Replace("~",home).Replace("%d",home).Replace("%h",host).Replace("%r",user));if(!File.Exists(path))continue;try{var key=new PrivateKeyFile(path);keys.Add(key);auth.Add(new PrivateKeyAuthenticationMethod(user,key));}catch(Renci.SshNet.Common.SshPassPhraseNullOrEmptyException){throw new NotSupportedException("私钥已加密。文件面板不保存口令；请使用集成终端的系统 sftp（支持 ssh-agent）。");}}
 if(auth.Count==0)throw new NotSupportedException("没有可直接读取的私钥。文件面板暂不支持 ssh-agent；请在设备设置中指定私钥或使用终端 sftp。");
 var accepted=new HashSet<string>();var revoked=new HashSet<string>();if(known.Count==0)known.Add(Path.Combine(home,".ssh","known_hosts"));string target=string.IsNullOrWhiteSpace(hostAlias)?host:hostAlias;string lookup=port==22?target:"["+target+"]:"+port;foreach(var f in known){string path=Environment.ExpandEnvironmentVariables(f.Replace("~",home));if(!File.Exists(path))continue;string found=Run("ssh-keygen.exe","-F "+Arg(lookup)+" -f "+Arg(path));foreach(var l in found.Split('\n')){var parts=l.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);if(parts.Length>=4&&parts[0]=="@revoked")revoked.Add(parts[3]);if(parts.Length>=3&&!parts[0].StartsWith("#")&&!parts[0].StartsWith("@"))accepted.Add(parts[2]);}}
 if(accepted.Count==0)throw new IOException("此主机尚未验证。请先在集成终端用 ssh 连接并核对指纹；文件面板不会自动信任新主机。");
 var ci=new ConnectionInfo(host,port,user,auth.ToArray()){Timeout=TimeSpan.FromSeconds(12)};client=new SftpClient(ci);client.BufferSize=131072;client.OperationTimeout=TimeSpan.FromSeconds(30);client.HostKeyReceived+=(o,e)=>{string presented=Convert.ToBase64String(e.HostKey);e.CanTrust=accepted.Contains(presented)&&!revoked.Contains(presented);};await client.ConnectAsync(token).ConfigureAwait(false);Home=client.WorkingDirectory;
 },token).ConfigureAwait(false);}
 public List<FileEntry> List(string path,CancellationToken token){token.ThrowIfCancellationRequested();var result=new List<FileEntry>();if(Device.IsLocal){foreach(var item in new DirectoryInfo(path).EnumerateFileSystemInfos()){token.ThrowIfCancellationRequested();bool dir=(item.Attributes&FileAttributes.Directory)!=0;result.Add(new FileEntry{Name=item.Name,FullPath=item.FullName,IsDirectory=dir,IsLink=(item.Attributes&FileAttributes.ReparsePoint)!=0,Size=dir?0:((FileInfo)item).Length,Modified=item.LastWriteTime,Permissions=(item.Attributes&FileAttributes.ReadOnly)!=0?"只读":"读写"});}}else{foreach(var item in client.ListDirectory(path)){token.ThrowIfCancellationRequested();if(item.Name=="."||item.Name=="..")continue;result.Add(new FileEntry{Name=item.Name,FullPath=item.FullName,IsDirectory=item.IsDirectory,IsLink=item.IsSymbolicLink,Size=item.Length,Modified=item.LastWriteTime,Permissions=PermissionText(item.Attributes)});}}return FileTools.Sort(result);}
 static string PermissionText(Renci.SshNet.Sftp.SftpFileAttributes a){return (a.OwnerCanRead?"r":"-")+(a.OwnerCanWrite?"w":"-")+(a.OwnerCanExecute?"x":"-")+(a.GroupCanRead?"r":"-")+(a.GroupCanWrite?"w":"-")+(a.GroupCanExecute?"x":"-")+(a.OthersCanRead?"r":"-")+(a.OthersCanWrite?"w":"-")+(a.OthersCanExecute?"x":"-");}
 public bool IsDirectory(string path){return Device.IsLocal?Directory.Exists(path):client.GetAttributes(path).IsDirectory;}
 public bool IsLink(string path){return Device.IsLocal?(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0:client.GetAttributes(path).IsSymbolicLink;}
 public bool Exists(string path){return Device.IsLocal?File.Exists(path)||Directory.Exists(path):client.Exists(path);}
 public long Length(string path){return Device.IsLocal?new FileInfo(path).Length:client.GetAttributes(path).Size;}
 public DateTime Stamp(string path){return Device.IsLocal?File.GetLastWriteTimeUtc(path):client.GetAttributes(path).LastWriteTimeUtc;}
 public void MakeDirectory(string path){if(Device.IsLocal){Directory.CreateDirectory(path);return;}if(client.Exists(path))return;string parent=FileTools.Parent(false,path);if(parent!=path&&!client.Exists(parent))MakeDirectory(parent);client.CreateDirectory(path);}
 public Stream OpenRead(string path){return Device.IsLocal?(Stream)new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,131072,true):client.OpenRead(path);}
 public Stream OpenNew(string path){return Device.IsLocal?(Stream)new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,131072,true):client.Open(path,FileMode.CreateNew,FileAccess.Write);}
 public void RemoveOwnPart(string path){if(Device.IsLocal){if(File.Exists(path))File.Delete(path);}else if(client!=null&&client.IsConnected&&client.Exists(path))client.DeleteFile(path);}
 public void Commit(string part,string dest,bool replace){if(Device.IsLocal){if(replace&&File.Exists(dest))File.Replace(part,dest,null);else File.Move(part,dest);}else{if(replace)client.RenameFile(part,dest,true);else client.RenameFile(part,dest);}}
 public void UndoCopy(string path,string expectedHash,DateTime stamp,long length){
 // Only the exact successful copy may be removed. Never traverse links or recurse.
 string ancestor=path;while(!string.IsNullOrEmpty(ancestor)){if(IsLink(ancestor))throw new IOException("目标路径包含链接，已保留文件。");string parent=FileTools.Parent(Device.IsLocal,ancestor);if(parent==ancestor)break;ancestor=parent;}
 if(!Exists(path))throw new IOException("目标已不存在。");if(Length(path)!=length||Stamp(path)!=stamp)throw new IOException("目标已被修改，已保留文件。");
 string owned=path+".aq-undo-"+Guid.NewGuid().ToString("N");Commit(path,owned,false);
 try{string actual;using(var input=OpenRead(owned))using(var sha=System.Security.Cryptography.SHA256.Create())actual=Convert.ToBase64String(sha.ComputeHash(input));if(actual!=expectedHash||Length(owned)!=length||Stamp(owned)!=stamp)throw new IOException("目标内容已改变，不能撤销。");RemoveOwnPart(owned);}
 catch{try{Commit(owned,path,false);}catch{throw new IOException("文件已保留在 "+owned+"；原路径发生变化，请手动检查。");}throw;}
 }
 public void RemoveOwnTestDirectory(string path){if(!path.StartsWith(Home.TrimEnd('/' )+"/.aqserver-test-"))throw new IOException("仅允许清理本测试目录");client.DeleteDirectory(path);}
 public void Dispose(){if(client!=null){client.Dispose();client=null;}foreach(var k in keys)k.Dispose();keys.Clear();}
}






