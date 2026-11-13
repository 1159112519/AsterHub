using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
// Locale belongs to preferences. UI text is localized before rendering; user data is unchanged.
internal static class UiLanguage {
 static string language;
 static readonly Lazy<Dictionary<string,string>> english=new Lazy<Dictionary<string,string>>(()=>{
  var file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"resources","locales","en.json");
  return new JavaScriptSerializer{MaxJsonLength=4194304}.Deserialize<Dictionary<string,string>>(File.ReadAllText(file));
 });
 internal static string Current {get{if(language==null)Set(DesktopPreferences.Load().language);return language;}}
 internal static void Set(string value){System.Threading.Interlocked.Exchange(ref language,value=="en"?"en":"zh-CN");}
 internal static string Text(string value){if(value==null||Current!="en")return value;string result;if(english.Value.TryGetValue(value,out result))return result;foreach(var entry in english.Value.Where(e=>e.Key.Length>5&&!e.Key.Contains("{")).OrderByDescending(e=>e.Key.Length)){if(value.StartsWith(entry.Key,StringComparison.Ordinal))return entry.Value+value.Substring(entry.Key.Length);}return value;}
}
