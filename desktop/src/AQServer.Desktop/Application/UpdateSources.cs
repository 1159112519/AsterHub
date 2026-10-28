using System;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

// Transport configuration is per-user. Release trust still comes only from the embedded signing key.
public static class UpdateSources
{
    public sealed class Preferences { public string endpoint { get; set; } }
    public static string PreferencePath {
        get {
            string isolated = Environment.GetEnvironmentVariable("AQSERVER_RUNTIME_ROOT");
            return Path.Combine(string.IsNullOrWhiteSpace(isolated) ? Path.GetDirectoryName(Storage.PathName) : isolated,
                "update-source.json");
        }
    }

    public static string CurrentEndpoint {
        get {
            try {
                if (!File.Exists(PreferencePath)) return AppUpdates.Endpoint;
                var saved = new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(PreferencePath, Encoding.UTF8));
                return Normalize(saved == null ? null : saved.endpoint);
            } catch { return AppUpdates.Endpoint; }
        }
    }

    public static string Normalize(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return AppUpdates.Endpoint;
        Uri uri;
        if (!Uri.TryCreate(address.Trim(), UriKind.Absolute, out uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("请输入完整的网站或更新清单地址，不要包含账号、查询参数或片段。");
        if (!uri.AbsolutePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) {
            string path = uri.AbsolutePath.TrimEnd('/');
            path += path.EndsWith("/releases", StringComparison.OrdinalIgnoreCase) ? "/stable.json" : "/releases/stable.json";
            uri = new UriBuilder(uri) { Path = path }.Uri;
        }
        return uri.AbsoluteUri;
    }

    public static void Save(string address)
    {
        var endpoint = Normalize(address);
        string path = PreferencePath, temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        try {
            File.WriteAllText(temporary, new JavaScriptSerializer().Serialize(new Preferences { endpoint = endpoint }), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static string WebsiteFor(string endpoint)
    {
        var uri = new Uri(Normalize(endpoint));
        return uri.AbsolutePath.EndsWith("/releases/stable.json", StringComparison.OrdinalIgnoreCase)
            ? new Uri(uri, "../").AbsoluteUri : new Uri(uri, "./").AbsoluteUri;
    }
}
