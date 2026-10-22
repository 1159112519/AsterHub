using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
internal static class AQLauncher {
 static string Hash(byte[] data){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant();}
 static string Quote(string value){var result=new StringBuilder("\"");int slashes=0;foreach(char c in value??""){if(c=='\\'){slashes++;continue;}if(c=='\"'){result.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}result.Append('\\',slashes).Append(c);slashes=0;}return result.Append('\\',slashes*2).Append('"').ToString();}
 [STAThread]static int Main(string[] args){try{string launcher=Assembly.GetExecutingAssembly().Location;byte[] package;using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("AQPayload.zip")){using(var data=new MemoryStream()){stream.CopyTo(data);package=data.ToArray();}}string key=Hash(package).Substring(0,16);string customRoot=Environment.GetEnvironmentVariable("AQSERVER_RUNTIME_ROOT");string runtimeRoot=string.IsNullOrEmpty(customRoot)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AQServer","Runtime"):Path.GetFullPath(customRoot);string root=Path.Combine(runtimeRoot,"v0.1.0-"+key);Directory.CreateDirectory(root);using(var mutex=new Mutex(false,@"Local\AQServerExtract_"+key)){bool held=false;try{try{held=mutex.WaitOne(TimeSpan.FromSeconds(45));}catch(AbandonedMutexException){held=true;}if(!held)throw new IOException("运行文件正在准备，请稍后重试。");string fullRoot=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;using(var zip=new ZipArchive(new MemoryStream(package),ZipArchiveMode.Read)){foreach(var entry in zip.Entries){string dest=Path.GetFullPath(Path.Combine(root,entry.FullName.Replace('/',Path.DirectorySeparatorChar)));if(!dest.StartsWith(fullRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("安装文件路径无效。");if(entry.FullName.EndsWith("/")){Directory.CreateDirectory(dest);continue;}byte[] bytes;using(var input=entry.Open())using(var data=new MemoryStream()){input.CopyTo(data);bytes=data.ToArray();}if(File.Exists(dest)&&new FileInfo(dest).Length==bytes.Length&&Hash(File.ReadAllBytes(dest))==Hash(bytes))continue;Directory.CreateDirectory(Path.GetDirectoryName(dest));string pending=dest+".tmp-"+Process.GetCurrentProcess().Id;File.WriteAllBytes(pending,bytes);if(File.Exists(dest))File.Delete(dest);File.Move(pending,dest);}}}finally{if(held)mutex.ReleaseMutex();}}
 var start=new ProcessStartInfo(Path.Combine(root,"AQServer.App.exe"),string.Join(" ",args.Select(Quote))){UseShellExecute=false,WorkingDirectory=root};start.EnvironmentVariables["AQSERVER_LAUNCHER_PATH"]=launcher;using(var child=Process.Start(start)){child.WaitForExit();return child.ExitCode;}
 }catch(Exception e){string error="AQ Server 无法启动：\n"+e.Message;try{File.WriteAllText(Path.Combine(Path.GetTempPath(),"AQServer-launcher-error.txt"),e.ToString());}catch{}if(args.Length==0)MessageBox.Show(error,"AQ Server",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
}
