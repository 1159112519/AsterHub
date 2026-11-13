using System;
using System.IO;
public static class AppPaths {
 public static string InstallRoot { get { return AppDomain.CurrentDomain.BaseDirectory; } }
 public static string TerminalAssets { get { return Path.Combine(InstallRoot,"resources","terminal"); } }
 public static string LogDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AQServer","Logs"); } }
}
