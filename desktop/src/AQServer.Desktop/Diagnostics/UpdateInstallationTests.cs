using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

public static class UpdateInstallationTests
{
    static Mutex sessionMutex;
    public static IEnumerable<string> CheckArguments(string folder)
    {
        var start = UpdateInstallation.CreateStartInfo(Path.Combine(folder, "download with spaces.exe"), Path.Combine(folder, "program with spaces"), Path.Combine(folder, "update log.txt"), 1234);
        if (!start.UseShellExecute || !start.Arguments.Contains("/SILENT ") || !start.Arguments.Contains("/NORESTART") || !start.Arguments.Contains("/AQPID=1234") || !start.Arguments.Contains("/DIR=\"") || start.Arguments.Contains("/AQRESTARTPROOF"))
            throw new Exception("Silent update must wait for its caller, retain its install path and use normal restart.");
        yield return "PASS silent installation arguments preserve custom paths and parent identity";
        bool rejected = false;
        try { UpdateInstallation.CreateStartInfo("setup.exe", "bad\"path", "log.txt", 1); } catch (ArgumentException) { rejected = true; }
        if (!rejected) throw new Exception("Unexpected quote in installer path arguments.");
        yield return "PASS malformed installer path rejected before launching";
    }
    static void Isolate(string folder)
    {
        Directory.CreateDirectory(folder);
        Storage.PathName = Path.Combine(folder, "settings.xml");
        ArchiveStore.DefaultRoot = Path.Combine(folder, "History");
        var settings = new Settings { SelectedName = "本机", SelectedView = "CPU" };
        settings.Servers.Add(Storage.LocalServer()); Storage.Save(settings);
    }
    public static void StartSession(Application app, string folder, string installer, bool legacy = false)
    {
        Isolate(folder);
        sessionMutex = new Mutex(true, @"Local\AQServerMonitor-Installer-QA");
        app.DispatcherUnhandledException += (s,e) => { File.WriteAllText(Path.Combine(folder,"handoff-error.txt"),e.Exception.ToString());e.Handled=true;app.Shutdown(1); };
        var window = new MonitorWindow(true); app.MainWindow = window; window.Show();
        var timer = new DispatcherTimer { Interval=TimeSpan.FromSeconds(1.5) };
        bool started=false;
        timer.Tick += (s,e) => {
            if (started) { timer.Stop(); window.Exit(); app.Shutdown(); return; }
            var info = new FileInfo(installer); string hash;
            using(var input=File.OpenRead(installer))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");
            var release = new ReleaseInfo { installerBytes=info.Length,installerSha256=hash,version=AppUpdates.DisplayVersion };
            Process installing;
            if (legacy) {
                AppUpdates.VerifyInstaller(installer,release);
                var start=UpdateInstallation.CreateStartInfo(installer,AppPaths.InstallRoot,Path.Combine(folder,"legacy-install.log"),Process.GetCurrentProcess().Id,Path.Combine(folder,"restart-result.json"));
                start.Arguments=start.Arguments.Replace(" /AQPID="+Process.GetCurrentProcess().Id,"");
                installing=Process.Start(start);
            } else installing=UpdateInstallation.StartAt(installer,release,AppPaths.InstallRoot,Path.Combine(folder,"restart-result.json"));
            using(var process=installing)
                File.WriteAllText(Path.Combine(folder,"handoff-started.txt"),"PARENT_PID="+Process.GetCurrentProcess().Id+"\nINSTALLER_PID="+process.Id+"\nWINDOW_VISIBLE="+window.IsVisible);
            started=true;
        }; timer.Start();
    }
    public static void RestartSmoke(Application app,string proof)
    {
        string folder=Path.GetDirectoryName(proof); Isolate(Path.Combine(folder,"restarted-user-data"));
        var window=new MonitorWindow(true);app.MainWindow=window;window.Show();
        var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(.8) };
        timer.Tick+=(s,e)=>{timer.Stop();window.RenderImage(Path.Combine(folder,"update-restarted.png"),1200,900,96);
            File.WriteAllText(proof,ArchiveStore.Json().Serialize(new { version=AppUpdates.DisplayVersion,visible=window.IsVisible,loaded=window.IsLoaded,launcher=Startup.ExecutablePath,utc=DateTime.UtcNow }));
            window.Exit();app.Shutdown();};timer.Start();
    }
}
