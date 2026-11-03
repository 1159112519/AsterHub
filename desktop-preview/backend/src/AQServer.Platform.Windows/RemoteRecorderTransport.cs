using System;
using System.Collections.Generic;
using System.Globalization;
public static class RemoteRecorderTransport {
 public static string Run(Server server,string action,long start,long end,long ticks){
  if(action!="status"&&action!="query"&&action!="read")throw new ArgumentException("未知采集读取操作");
  // Fixed executable and numeric-only arguments. No incoming shell/path fragments.
  var args=new List<string>{"python3","/opt/asterhub-agent/asterhub_agent.py",action};
  if(action=="query"){args.Add("--after");args.Add(start.ToString(CultureInfo.InvariantCulture));args.Add("--before");args.Add(end.ToString(CultureInfo.InvariantCulture));}
  if(action=="read"){args.Add("--ticks");args.Add(ticks.ToString(CultureInfo.InvariantCulture));}
  string program="import json,pathlib,subprocess\np=pathlib.Path('/opt/asterhub-agent/asterhub_agent.py')\nif not p.is_file(): print(json.dumps({'installed':False,'healthy':False}))\nelse:\n r=subprocess.run("+new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(args)+",capture_output=True,text=True,timeout=8)\n print(r.stdout if r.returncode==0 and len(r.stdout)<2000000 else json.dumps({'installed':True,'healthy':False,'error':'Recorder read unavailable'}))";
  return AqModuleSsh.Run(server,program);
 }
}
