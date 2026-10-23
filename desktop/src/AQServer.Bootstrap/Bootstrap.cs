using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
internal static class AQBootstrap {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
 static extern bool SetDllDirectory(string path);
 [STAThread] public static int Main(string[] args) {
  try {
   string launcher=Assembly.GetExecutingAssembly().Location;
   string root=Path.GetDirectoryName(launcher);
   string app=Path.Combine(root,"app","AQServer.Desktop.dll");
   if(!File.Exists(app))throw new FileNotFoundException("程序组件不完整，请重新运行 AQ Server 安装程序。",app);
   if(!SetDllDirectory(Path.Combine(root,"runtime","win-x64")))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   Environment.SetEnvironmentVariable("AQSERVER_LAUNCHER_PATH",launcher);
   var entry=Assembly.LoadFrom(app).GetType("Program",true).GetMethod("Main",BindingFlags.Static|BindingFlags.Public);
   return (int)entry.Invoke(null,new object[]{args});
  } catch(Exception error) {
   var invocation=error as TargetInvocationException;
   if(invocation!=null&&invocation.InnerException!=null)error=invocation.InnerException;
   string log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AQServer","Logs","startup.log");
   try{Directory.CreateDirectory(Path.GetDirectoryName(log));File.WriteAllText(log,error.ToString());}catch{}
   if(args.Length==0)MessageBox.Show("无法启动 AQ Server。\n"+error.Message+"\n日志："+log,"AQ Server");
   else Console.Error.WriteLine(error);
   return 1;
  }
 }
}
