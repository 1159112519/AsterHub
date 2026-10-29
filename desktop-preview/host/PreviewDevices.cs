using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

public sealed class PreviewDeviceConfig { public int Interval=3;public string Selected="local";public List<Server> Servers=new List<Server>(); }
// Owns configuration and credentials. Public views are deliberately constructed, never serialized Servers.
public sealed class PreviewDevices {
 readonly string path;readonly JavaScriptSerializer json=ArchiveStore.Json();readonly object gate=new object();PreviewDeviceConfig config;
 public PreviewDevices(string root){
  path=Path.Combine(root,"devices.json");
  config=File.Exists(path)?json.Deserialize<PreviewDeviceConfig>(File.ReadAllText(path)):new PreviewDeviceConfig();
  if(config==null||config.Servers==null)throw new IOException("设备配置无法读取，请保留配置文件后重试。");
  config.Interval=Math.Max(2,Math.Min(60,config.Interval));
  config.Servers.RemoveAll(s=>s==null||s.IsLocal);config.Servers.Insert(0,Storage.LocalServer());
  if(!config.Servers.Any(s=>Id(s)==config.Selected))config.Selected="local";
 }
 public static string Id(Server s){return s.IsLocal?"local":ArchiveStore.Key(s);}
 public int Interval{get{lock(gate)return config.Interval;}}
 public string Selected{get{lock(gate)return config.Selected;}}
 public Server[] All(){lock(gate)return config.Servers.Select(s=>s.Copy()).ToArray();}
 public Server Find(string id){lock(gate){var server=config.Servers.FirstOrDefault(s=>Id(s)==id);if(server==null)throw new ArgumentException("设备不存在或已移除");return server.Copy();}}
 static string Text(Dictionary<string,object> p,string key,string fallback=""){object value;return p.TryGetValue(key,out value)?Convert.ToString(value,CultureInfo.InvariantCulture):fallback;}
 public Server Draft(Dictionary<string,object> p){
  var id=Text(p,"id");var prior=string.IsNullOrEmpty(id)?new Server():Find(id);if(prior.IsLocal)throw new ArgumentException("本机连接无需编辑");
  var server=prior.Copy();server.Name=Text(p,"name",prior.Name).Trim();server.Host=Text(p,"host",prior.Host).Trim();server.User=Text(p,"user",prior.User).Trim();server.KeyPath=Text(p,"keyPath",prior.KeyPath).Trim();server.AuthMode=Text(p,"authMode",prior.AuthMode);int port;
  if(!int.TryParse(Text(p,"port",prior.Port.ToString()),out port)||port<1||port>65535)throw new ArgumentException("端口应为 1–65535");server.Port=port;
  if(server.Name.Length<1||server.Name.Length>80||server.Name.Any(char.IsControl))throw new ArgumentException("请输入 1–80 字的设备名称");
  if(server.Host.Length>253||!Regex.IsMatch(server.Host,@"^[a-zA-Z0-9._:-]+$")||server.Host.StartsWith("-"))throw new ArgumentException("请输入主机名或 IP 地址");
  if(server.User.Length>64||!Regex.IsMatch(server.User,@"^[a-zA-Z0-9._-]*$"))throw new ArgumentException("SSH 用户名无效");
  if(!new[]{"password","key","auto","none"}.Contains(server.AuthMode))throw new ArgumentException("认证方式无效");
  if(server.AuthMode=="key"&&(server.KeyPath.Length>1024||!Path.IsPathRooted(server.KeyPath)||!File.Exists(server.KeyPath)))throw new ArgumentException("请选择存在的私钥文件");
  if(server.AuthMode!="key")server.KeyPath="";
  if(p.ContainsKey("password")){string password=Text(p,"password");if(password.Length>2048)throw new ArgumentException("密码过长");server.ProtectedPassword=SshAuthentication.Protect(password);}
  if(server.AuthMode!="password")server.ProtectedPassword="";server.Password=null;return server;
 }
 void Write(PreviewDeviceConfig next){
  string temp=path+".tmp";var text=json.Serialize(next);
  using(var fs=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){var bytes=new UTF8Encoding(false).GetBytes(text);fs.Write(bytes,0,bytes.Length);fs.Flush(true);}
  if(File.Exists(path))File.Replace(temp,path,path+".backup");else File.Move(temp,path);config=next;
 }
 PreviewDeviceConfig Next(){return new PreviewDeviceConfig{Interval=config.Interval,Selected=config.Selected,Servers=config.Servers.Select(s=>s.Copy()).ToList()};}
 public Server Save(Dictionary<string,object> p){var server=Draft(p);lock(gate){string old=Text(p,"id"),id=Id(server);if(config.Servers.Any(s=>Id(s)==id&&Id(s)!=old))throw new ArgumentException("这台设备已经存在");var next=Next();if(string.IsNullOrEmpty(old)&&next.Servers.Count>=24)throw new ArgumentException("最多可添加 24 台设备");next.Servers.RemoveAll(s=>Id(s)==old);next.Servers.Add(server);if(next.Selected==old)next.Selected=id;Write(next);return server.Copy();}}
 public void Remove(string id){if(id=="local")throw new ArgumentException("本机不能移除");lock(gate){Find(id);var next=Next();next.Servers.RemoveAll(s=>Id(s)==id);if(next.Selected==id)next.Selected="local";Write(next);}}
 public void Select(string id){lock(gate){Find(id);var next=Next();next.Selected=id;Write(next);}}
 public void SetInterval(int seconds){if(seconds<2||seconds>60)throw new ArgumentOutOfRangeException("seconds");lock(gate){var next=Next();next.Interval=seconds;Write(next);}}
 public object Public(Server s){return new{id=Id(s),name=s.Name,host=s.Host,user=s.User,port=s.Port,isLocal=s.IsLocal,authMode=s.AuthMode,keyPath=s.KeyPath,hasPassword=!string.IsNullOrEmpty(s.ProtectedPassword)};}
 public string SafeError(string text,Server server){if(string.IsNullOrEmpty(text))return "";string password;try{password=SshAuthentication.Password(server);}catch{return "无法读取保存的凭据，请重新输入。";}if(!string.IsNullOrEmpty(password))text=(text??"").Replace(password,"[已隐藏]");text=Regex.Replace(text??"",@"[\r\n\t]+"," ");return text.Length>240?text.Substring(0,240):text;}
}
