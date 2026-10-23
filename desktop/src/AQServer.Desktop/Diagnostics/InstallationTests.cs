using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
public static class InstallationTests {
 public static int Run(string output) {
  Directory.CreateDirectory(output);
  try {
   var assemblies=new[]{typeof(Server).Assembly,typeof(ArchiveStore).Assembly,typeof(Local).Assembly,typeof(Program).Assembly};
   if(assemblies.Distinct().Count()!=4)throw new Exception("Core, storage, Windows and desktop must be separate assemblies.");
   var expected=new[]{"AQServer.Core","AQServer.Storage","AQServer.Platform.Windows","AQServer.Desktop"};
   for(int i=0;i<assemblies.Length;i++)if(assemblies[i].GetName().Name!=expected[i]||assemblies[i].GetName().Version!=AppUpdates.CurrentVersion)throw new Exception("Component identity/version mismatch.");
   foreach(var assembly in assemblies.Take(2))foreach(var reference in assembly.GetReferencedAssemblies())if(new[]{"PresentationFramework","PresentationCore","System.Management","System.Windows.Forms"}.Contains(reference.Name))throw new Exception("Portable module references a Windows UI dependency.");
   if(!File.Exists(Path.Combine(AppPaths.TerminalAssets,"bootstrap.sh"))||!File.Exists(Path.Combine(AppPaths.TerminalAssets,"index.html")))throw new Exception("Terminal resources missing.");
   if(!File.Exists(Path.Combine(AppPaths.InstallRoot,"runtime","win-x64","WebView2Loader.dll")))throw new Exception("Native loader missing.");
   if(Path.GetFullPath(Startup.ExecutablePath)!=Path.Combine(AppPaths.InstallRoot,"AQServer.exe"))throw new Exception("Startup points to a component instead of the root launcher.");
   var original=Storage.PathName;
   try {
    Storage.PathName=Path.Combine(output,"legacy-settings","servers.xml");
    var settings=new Settings{ThemeName="graphite",Interval=7};settings.Servers.Add(new Server{Name="compatibility-fixture",Host="127.0.0.1",User="test",Port=2222});
    Storage.Save(settings);
    var restored=Storage.Load();
    if(restored.Interval!=7||restored.Servers[0].Port!=2222||restored.Terminal.FontSize!=settings.Terminal.FontSize)throw new Exception("Settings round trip changed.");
    string xml=File.ReadAllText(Storage.PathName);if(xml.Contains("AQServer.Core")||xml.Contains("AQServer.Storage"))throw new Exception("Settings unexpectedly persist assembly identity.");
   } finally {Storage.PathName=original;}
   var report=new{version=AppUpdates.DisplayVersion,modules=assemblies.Select(a=>new{name=a.GetName().Name,version=a.GetName().Version.ToString(),file=a.Location}).ToArray(),portableDependencies=true,settingsCompatible=true,resourcesPresent=true,launcherCorrect=true};
   File.WriteAllText(Path.Combine(output,"modules.json"),ArchiveStore.Json().Serialize(report));
   return 0;
  }catch(Exception error){File.WriteAllText(Path.Combine(output,"module-error.txt"),error.ToString());return 1;}
 }
}
