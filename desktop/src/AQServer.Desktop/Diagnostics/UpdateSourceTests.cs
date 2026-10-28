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
            string expected = "https://updates.example.org/aqserver/releases/stable.json";
            Check(UpdateSources.Normalize("https://updates.example.org/aqserver/") == expected, "website normalizes to manifest", log);
            Check(UpdateSources.Normalize("https://updates.example.org/aqserver/releases") == expected, "release directory normalizes without duplicate segment", log);
            foreach (string address in new[] { "file:///C:/fixture.json", "relative", "https://example.org/?q=1", "https://example.org/#fragment" }) {
                bool rejected = false;
                try { UpdateSources.Normalize(address); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "invalid update-source syntax rejected", log);
            }
            UpdateSources.Save("https://updates.example.org/aqserver/");
            Check(UpdateSources.CurrentEndpoint == expected, "source preference persists and deserializes", log);
            UpdateSources.Save(AppUpdates.Endpoint);
            Check(UpdateSources.CurrentEndpoint == AppUpdates.Endpoint, "source preference can be replaced and reset", log);
            File.WriteAllText(UpdateSources.PreferencePath, "{corrupt");
            Check(UpdateSources.CurrentEndpoint == AppUpdates.Endpoint, "corrupt preference falls back to default", log);
            var release = new ReleaseInfo { version = "99.0.0", installerUrl = AppUpdates.Website + "releases/setup.exe", ManifestEndpoint = "https://mirror.example.org/aqserver/releases/stable.json" };
            Check(AppUpdates.DownloadAddress(release) == "https://mirror.example.org/aqserver/releases/setup.exe", "verified-source origin transports signed installer filename", log);
            Check(!new JavaScriptSerializer().Serialize(release).Contains("ManifestEndpoint"), "transport origin is excluded from signed payload", log);
            release.ManifestEndpoint = "https://mirror.example.org/aqserver/releases/latest.json";
            Check(AppUpdates.DownloadAddress(release) == "https://mirror.example.org/aqserver/releases/setup.exe", "custom JSON filename uses the same verified transport directory", log);
            release.installerUrl = "https://publisher.example.org/custom.exe";
            Check(AppUpdates.DownloadAddress(release) == release.installerUrl, "custom signed download remains unchanged", log);
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
