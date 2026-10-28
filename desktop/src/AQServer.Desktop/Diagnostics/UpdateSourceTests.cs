using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Web.Script.Serialization;

public static class UpdateSourceTests
{
    static void Check(bool ok, string name, List<string> log) { if (!ok) throw new Exception(name); log.Add("PASS " + name); }
    public static IEnumerable<string> Run(string folder)
    {
        var log = new List<string>();
        string prior = Environment.GetEnvironmentVariable("AQSERVER_RUNTIME_ROOT");
        try {
            Environment.SetEnvironmentVariable("AQSERVER_RUNTIME_ROOT", Path.Combine(Path.GetFullPath(folder), "source-preference"));
            Directory.CreateDirectory(Path.GetDirectoryName(UpdateSources.PreferencePath));
            File.WriteAllText(UpdateSources.PreferencePath,"{\"endpoint\":\"https://untrusted.example.org/stable.json\"}");
            Check(UpdateSources.CurrentEndpoint==AppUpdates.Endpoint,"legacy user update-source override ignored",log);
            Check(AppUpdates.Endpoint=="https://raw.githubusercontent.com/1159112519/aq-server/main/website/public/releases/stable.json","official channel fixed to publisher",log);
            File.WriteAllText(UpdateSources.PreferencePath,"{corrupt");
            Check(UpdateSources.CurrentEndpoint==AppUpdates.Endpoint,"corrupt legacy settings cannot change official channel",log);
            var release=new ReleaseInfo{version="99.0.0",installerUrl="https://github.com/1159112519/aq-server/releases/download/v99.0.0/setup.exe",ManifestEndpoint="https://untrusted.example.org/stable.json"};
            Check(AppUpdates.DownloadAddress(release)==release.installerUrl,"signed installer URL cannot be redirected by legacy source",log);
            Check(!new JavaScriptSerializer().Serialize(release).Contains("ManifestEndpoint"),"transport origin excluded from signed payload",log);
            var bytes = Enumerable.Range(0, 2048).Select(i => (byte)i).ToArray();
            release.ManifestEndpoint = null; release.installerBytes = bytes.Length;
            using (var sha = SHA256.Create()) release.installerSha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            bytes[80] ^= 1;
            using (var endpoint = new UpdateTests.Endpoint(bytes)) {
                release.installerUrl = endpoint.Url; bool rejected = false;
                try { AppUpdates.DownloadTo(release, null, CancellationToken.None, folder).GetAwaiter().GetResult(); } catch (InvalidDataException) { rejected = true; }
                Check(rejected && !Directory.GetFiles(folder, "*.part").Any(), "wrong-hash download rejected and partial removed", log);
            }
            using (var endpoint = new UpdateTests.Endpoint(new byte[16], 600)) using (var cancel = new CancellationTokenSource()) {
                cancel.CancelAfter(100); var clock = System.Diagnostics.Stopwatch.StartNew(); bool cancelled = false;
                try { AppUpdates.CheckAt(endpoint.Url, UpdatePublicKey.Xml, cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && clock.ElapsedMilliseconds < 1500, "pending network check cancels promptly", log);
            }
            using (var endpoint = new UpdateTests.Endpoint(bytes, 600)) using (var cancel = new CancellationTokenSource()) {
                release.installerUrl = endpoint.Url; cancel.CancelAfter(100); bool cancelled = false;
                try { AppUpdates.DownloadTo(release, null, cancel.Token, folder).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; }
                Check(cancelled && !Directory.GetFiles(folder, "*.part").Any(), "pending network download cancels and leaves no partial package", log);
            }
            File.WriteAllLines(Path.Combine(folder, "update-source-results.txt"), log); return log;
        } finally { Environment.SetEnvironmentVariable("AQSERVER_RUNTIME_ROOT", prior); }
    }
}
