using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
public static class Storage {
 public static string PathName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AQServer","servers.xml");
 public static Settings Load(){Settings s=null;try{using(var f=File.OpenRead(PathName))s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);}catch{}if(s==null)s=new Settings();if(s.Servers==null)s.Servers=new List<Server>();if(s.Servers.Count==0)s.Servers.Add(DefaultServer());s.Interval=Math.Max(2,Math.Min(120,s.Interval));s.RetentionDays=Math.Max(1,Math.Min(365,s.RetentionDays));s.ArchiveLimitMB=Math.Max(64,Math.Min(16384,s.ArchiveLimitMB));if(!s.Servers.Any(x=>x.IsLocal))s.Servers.Add(LocalServer());return s;}
 public static Server LocalServer(){return new Server{IsLocal=true,Name="本机",Host=Environment.MachineName,User=Environment.UserName,Port=22};}
 public static Server DefaultServer(){string home=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);if(string.IsNullOrWhiteSpace(home))home=Environment.GetEnvironmentVariable("USERPROFILE")??"";return new Server{Name="aq-server",Host="117.50.162.34",User="baiyang",Port=22,KeyPath=Path.Combine(home,".ssh","id_rsa")};}
 public static void Save(Settings s){Directory.CreateDirectory(Path.GetDirectoryName(PathName));var temp=PathName+".tmp";using(var f=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(f,s);if(File.Exists(PathName))File.Copy(PathName,PathName+".backup",true);File.Copy(temp,PathName,true);File.Delete(temp);}
}
