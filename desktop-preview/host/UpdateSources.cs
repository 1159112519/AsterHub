using System;
using System.IO;

// Publisher-maintained channel. Legacy per-user overrides are deliberately ignored.
public static class UpdateSources {
    public static string PreferencePath { get { string root=Environment.GetEnvironmentVariable("AQSERVER_RUNTIME_ROOT");return Path.Combine(string.IsNullOrWhiteSpace(root)?Path.GetDirectoryName(Storage.PathName):root,"update-source.json"); } }
    // Fixed publisher-owned repository paths only. No user overrides, mirrors, or temporary tunnels.
    public static string[] ManifestEndpoints { get { return new[] {
        AppUpdates.Endpoint,
        "https://api.github.com/repos/1159112519/AsterHub/contents/website/public/releases/windows-preview.json?ref=main",
        AppUpdates.Endpoint // one fresh attempt recovers transient failures / API rate limits
    }; } }
    public static string CurrentEndpoint { get { return AppUpdates.Endpoint; } }
}
