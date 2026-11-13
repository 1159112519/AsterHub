using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
public sealed partial class WorkbenchWindow {
 object RestartStatus(){int transfers,terminals;lock(fileGate){transfers=fileJobs.Values.Count(j=>j.Status=="queued"||j.Status=="running"||j.Status=="undoing");}lock(terminalGate){terminals=terminalSessions.Count+terminalCreations.Count;}return new{transfers=transfers,terminals=terminals};}
 void EnsureRestartSafe(Dictionary<string,object> p){lock(fileGate){if(fileJobs.Values.Any(j=>j.Status=="queued"||j.Status=="running"||j.Status=="undoing"))throw new IOException("文件任务仍在进行，请完成后再重启。图标选择已保存。");}lock(terminalGate){object confirmed;if((terminalSessions.Count+terminalCreations.Count)>0&&(!p.TryGetValue("confirm",out confirmed)||!Convert.ToBoolean(confirmed)))throw new IOException("请先确认断开终端会话。");}}
 static string QuoteArgument(string value){return "\""+value.Replace("\"", "\\\"")+"\"";}
 void StartReplacement(){string args="--data-root "+QuoteArgument(WorkbenchHost.DataRoot)+" --restart-parent "+Process.GetCurrentProcess().Id;if(WorkbenchHost.SmokeRoot!=null)args+=" --smoke "+QuoteArgument(WorkbenchHost.SmokeRoot);if(WorkbenchHost.DebugPort>0)args+=" --debug-port "+WorkbenchHost.DebugPort;Process.Start(new ProcessStartInfo{FileName=System.Reflection.Assembly.GetExecutingAssembly().Location,Arguments=args,UseShellExecute=false,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});}
}
