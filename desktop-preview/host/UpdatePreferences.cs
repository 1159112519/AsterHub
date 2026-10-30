using System;
using System.IO;
using System.Web.Script.Serialization;

// Independent of desktop/window options; absent files preserve the default for existing users.
public sealed class UpdatePreferences {
 public bool checkOnStartup=true;
 internal static string PathAt(string root){return Path.Combine(root,"update-preferences.json");}
 public static UpdatePreferences Load(string root){try{return new JavaScriptSerializer().Deserialize<UpdatePreferences>(File.ReadAllText(PathAt(root)))??new UpdatePreferences();}catch{return new UpdatePreferences();}}
 public void Save(string root){
  Directory.CreateDirectory(root);
  string path=PathAt(root),pending=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllText(pending,new JavaScriptSerializer().Serialize(this));if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);}finally{if(File.Exists(pending))File.Delete(pending);}
 }
}
