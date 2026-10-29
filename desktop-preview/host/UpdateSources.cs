using System;
using System.IO;

// Publisher-maintained channel. Legacy per-user overrides are deliberately ignored.
public static class UpdateSources {
    public static string PreferencePath { get { string root=Environment.GetEnvironmentVariable("AQSERVER_RUNTIME_ROOT");return Path.Combine(string.IsNullOrWhiteSpace(root)?Path.GetDirectoryName(Storage.PathName):root,"update-source.json"); } }
    public static string CurrentEndpoint { get { return AppUpdates.Endpoint; } }
}
