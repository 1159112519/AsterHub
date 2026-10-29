using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public sealed class NetworkCounter
{
    public string Id, Name, Description,Kind,IPv4,IPv6,Mac;
    public long? LinkSpeed,ReceivedPackets,SentPackets,ReceiveErrors,SendErrors,ReceiveDiscards,SendDiscards;
    public long ReceivedBytes, SentBytes;
    public double? DownloadBytesPerSecond, UploadBytesPerSecond;
    public NetworkCounter Copy() { return (NetworkCounter)MemberwiseClone(); }
}
public sealed class NetworkMetrics
{
    public double SampleSeconds;
    public int? TcpConnections;
    public List<NetworkCounter> Interfaces = new List<NetworkCounter>();
    public double? DownloadBytesPerSecond, UploadBytesPerSecond;
    public string Error;
    public NetworkMetrics Copy() { var result = (NetworkMetrics)MemberwiseClone(); result.Interfaces = Interfaces.Select(x => x.Copy()).ToList(); return result; }
}
public sealed class StorageVolume
{
    public string Path, Label, FileSystem;
    public long TotalBytes, UsedBytes, AvailableBytes;
    public double UsedPercent { get { return TotalBytes > 0 ? Math.Max(0, Math.Min(100, UsedBytes * 100.0 / TotalBytes)) : double.NaN; } }
    public StorageVolume Copy() { return (StorageVolume)MemberwiseClone(); }
}
public static class PeripheralMetrics
{
    public static void CalculateRates(NetworkMetrics current, NetworkMetrics previous)
    {
        if (current == null) return;
        current.DownloadBytesPerSecond = current.UploadBytesPerSecond = null;
        double elapsed = previous == null ? 0 : current.SampleSeconds - previous.SampleSeconds;
        foreach (var item in current.Interfaces) {
            item.DownloadBytesPerSecond = item.UploadBytesPerSecond = null;
            var before = previous == null ? null : previous.Interfaces.Find(x => x.Id == item.Id);
            if (elapsed <= 0 || before == null || item.ReceivedBytes < before.ReceivedBytes || item.SentBytes < before.SentBytes) continue;
            item.DownloadBytesPerSecond = (item.ReceivedBytes - before.ReceivedBytes) / elapsed;
            item.UploadBytesPerSecond = (item.SentBytes - before.SentBytes) / elapsed;
        }
        var valid = current.Interfaces.Where(x => x.DownloadBytesPerSecond.HasValue && x.UploadBytesPerSecond.HasValue).ToList();
        if (valid.Count > 0) {
            current.DownloadBytesPerSecond = valid.Sum(x => x.DownloadBytesPerSecond.Value);
            current.UploadBytesPerSecond = valid.Sum(x => x.UploadBytesPerSecond.Value);
        }
    }
    public static StorageVolume PrimaryVolume(Metrics metrics)
    {
        if (metrics == null || metrics.Volumes == null) return null;
        return metrics.Volumes.Find(x => x.Path == "/" || x.Path == metrics.PrimaryStoragePath) ?? metrics.Volumes.FirstOrDefault();
    }
    public static string Bytes(double? value, bool rate = false)
    {
        if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value < 0) return "—";
        double n = value.Value; string[] units = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" }; int unit = 0;
        while (n >= 1024 && unit < units.Length - 1) { n /= 1024; unit++; }
        return n.ToString(n >= 100 || unit == 0 ? "0" : "0.0", CultureInfo.InvariantCulture) + " " + units[unit] + (rate ? "/s" : "");
    }
    public static string Capacity(long used, long total)
    {
        double n = total, u = used; string[] units = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" }; int unit = 0;
        while (n >= 1024 && unit < units.Length - 1) { n /= 1024; u /= 1024; unit++; }
        string format = n >= 100 || unit == 0 ? "0" : "0.0";
        return u.ToString(format, CultureInfo.InvariantCulture) + " / " + n.ToString(format, CultureInfo.InvariantCulture) + " " + units[unit];
    }
    public static string CompactRate(double? value) { return Bytes(value, true).Replace(" ", ""); }
}
public sealed class DiskActivity {
 public string Id,Name;
 public double? ReadBytesPerSecond,WriteBytesPerSecond,QueueDepth,ActivePercent;
 public DiskActivity Copy(){return (DiskActivity)MemberwiseClone();}
}
