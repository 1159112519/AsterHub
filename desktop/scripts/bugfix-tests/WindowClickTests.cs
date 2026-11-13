using System;using System.Collections.Generic;using System.IO;using System.Reflection;using System.Reflection.Emit;using System.Windows;
// Test executable never constructs WorkbenchWindow/Application or sends any native input.
public sealed partial class WorkbenchWindow:Window { public bool IsExiting{get{return false;}} }
internal static class DiagnosticsLog { internal static void Write(string area,Exception error){} }
class WindowClickTests {
 static int checks;static void Check(bool ok,string name){checks++;if(!ok)throw new Exception(name);}
 static Dictionary<short,OpCode> codes=new Dictionary<short,OpCode>();
 static IEnumerable<MethodBase> Calls(MethodBase method){
  var asyncAttributes=method.GetCustomAttributes(typeof(System.Runtime.CompilerServices.AsyncStateMachineAttribute),false);if(asyncAttributes.Length>0)method=((System.Runtime.CompilerServices.AsyncStateMachineAttribute)asyncAttributes[0]).StateMachineType.GetMethod("MoveNext",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);var body=method.GetMethodBody();if(body==null)yield break;var il=body.GetILAsByteArray();
  for(int i=0;i<il.Length;){short key=il[i++];if(key==0xFE)key=(short)(0xFE00|il[i++]);var code=codes[key];int n=0;
   switch(code.OperandType){
    case OperandType.InlineMethod:int token=BitConverter.ToInt32(il,i);yield return method.Module.ResolveMethod(token);n=4;break;
    case OperandType.InlineSwitch:n=4+4*BitConverter.ToInt32(il,i);break;
    case OperandType.InlineI8:case OperandType.InlineR:n=8;break;
    case OperandType.InlineBrTarget:case OperandType.InlineField:case OperandType.InlineI:case OperandType.InlineSig:case OperandType.InlineString:case OperandType.InlineTok:case OperandType.InlineType:case OperandType.ShortInlineR:n=4;break;
    case OperandType.InlineVar:n=2;break;
    case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:n=1;break;
   }i+=n;
  }
 }
 static bool CallsNamed(MethodBase method,string type,string name){foreach(var called in Calls(method))if(called.DeclaringType.Name==type&&called.Name==name)return true;return false;}
 static MethodInfo Method(Type t,string name){return t.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static);}
 static int Main(string[] args){try{
  foreach(var f in typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static))if(f.FieldType==typeof(OpCode)){var op=(OpCode)f.GetValue(null);codes[op.Value]=op;}
  foreach(bool exiting in new[]{false,true})foreach(bool visible in new[]{false,true})foreach(bool minimized in new[]{false,true}){
   var expected=exiting?WindowClickAction.Ignore:visible&&!minimized?WindowClickAction.Minimize:WindowClickAction.Restore;
   Check(WindowClickCommands.ForTaskbarClick(exiting,visible,minimized)==expected,"toggle decision");
  }
  for(long command=0xF020;command<=0xF02F;command++)Check(WindowClickCommands.IsMinimizeSystemCommand(0x0112,command),"SC_MINIMIZE low nibble");
  foreach(int msg in new[]{0,0x6,0xA1,0x1C,0x46,0x112})foreach(long cmd in new long[]{0,0xF030,0xF060,0xF120,-1})Check(!WindowClickCommands.IsMinimizeSystemCommand(msg,cmd),"don't swallow restore/maximize/close");
  Check(!WindowClickCommands.IsMinimizeSystemCommand(0x6,0xF020),"message type matters");
  var intent=new MinimizeIntent();Check(!intent.Consume(),"initial clear");intent.Explicit();Check(intent.Consume(),"explicit consumed");Check(!intent.Consume(),"explicit consumed once");intent.Caption();Check(intent.Consume(),"caption consumed");intent.Caption();intent.Reset();Check(!intent.Consume(),"reset caption");intent.Explicit();intent.Reset();Check(!intent.Consume(),"reset explicit");
  string install=args[1];AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){string name=new AssemblyName(e.Name).Name+".dll";foreach(string folder in new[]{"runtime/managed","app/modules"}){string f=Path.Combine(install,folder,name);if(File.Exists(f))return Assembly.LoadFrom(f);}return null;};
  var app=Assembly.LoadFrom(Path.GetFullPath(args[0]));var window=app.GetType("WorkbenchWindow",true);var desktop=app.GetType("DesktopSettings",true);
  var shared=Method(window,"MinimizeLikeCaption");Check(shared!=null,"actual EXE has common minimize method");
  Check(CallsNamed(Method(window,"Message"),"WorkbenchWindow","MinimizeLikeCaption"),"actual in-app entry calls common action");
  Check(CallsNamed(Method(window,"MinimizeIntentMessage"),"WorkbenchWindow","MinimizeLikeCaption"),"actual native entry calls common action");
  Check(CallsNamed(Method(desktop,"Toggle"),"WorkbenchWindow","MinimizeLikeCaption"),"actual icon toggle calls common action");
  Check(CallsNamed(Method(window,"ToggleFromTaskbar"),"DesktopSettings","Toggle"),"actual launch toggle is wired");
  Check(CallsNamed(shared,"MinimizeIntent","Explicit"),"actual common action records intent");
  Check(CallsNamed(shared,"Window","set_WindowState"),"actual common action minimizes with WPF state");
  Check(!CallsNamed(shared,"WorkbenchWindow","RestoreFromTray"),"minimize never restores");
  Console.WriteLine("WINDOW_CLICK_CHECKS="+checks+" / NO_APPLICATION_STARTED");return 0;
 }catch(Exception e){Console.Error.WriteLine(e.ToString());return 1;}}
}
