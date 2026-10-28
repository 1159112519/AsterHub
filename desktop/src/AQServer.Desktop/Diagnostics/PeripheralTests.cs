using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

public static class PeripheralTests
{
    static List<string> log = new List<string>();
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); log.Add("PASS " + text); }
    static NetworkMetrics Net(double time, long down, long up, string id = "eth0") { var n = new NetworkMetrics { SampleSeconds = time }; n.Interfaces.Add(new NetworkCounter { Id = id, Name = id, ReceivedBytes = down, SentBytes = up }); return n; }
    public static int Run(string folder)
    {
        Directory.CreateDirectory(folder);
        try {
            var first = Net(10, 1000, 2000); PeripheralMetrics.CalculateRates(first, null);
            Check(!first.DownloadBytesPerSecond.HasValue && !first.UploadBytesPerSecond.HasValue, "first sample remains unknown");
            var next = Net(12, 7144, 4048); PeripheralMetrics.CalculateRates(next, first);
            Check(next.DownloadBytesPerSecond == 3072 && next.UploadBytesPerSecond == 1024, "counter delta direction and elapsed seconds");
            var other = Net(100, 99000, 100000); var otherNext = Net(104, 103000, 108000); PeripheralMetrics.CalculateRates(otherNext, other);
            Check(otherNext.DownloadBytesPerSecond == 1000 && next.DownloadBytesPerSecond == 3072, "independent device baselines");
            var reset = Net(14, 10, 10); PeripheralMetrics.CalculateRates(reset, next); Check(!reset.DownloadBytesPerSecond.HasValue, "counter reset cannot spike");
            var same = Net(12, 8000, 5000); PeripheralMetrics.CalculateRates(same, next); Check(!same.DownloadBytesPerSecond.HasValue, "duplicate sample time cannot fabricate zero");
            var adapter = Net(14, 8000, 5000, "eth1"); PeripheralMetrics.CalculateRates(adapter, next); Check(!adapter.DownloadBytesPerSecond.HasValue, "new adapter establishes its own baseline");
            var idle = Net(14, 7144, 4048); PeripheralMetrics.CalculateRates(idle, next); Check(idle.DownloadBytesPerSecond == 0 && idle.UploadBytesPerSecond == 0, "actual idle delta is zero");
            var remote = new Metrics(); PeripheralCollector.ParseRemote(remote, "AQNETTIME", "100.25 10.11"); PeripheralCollector.ParseRemote(remote, "AQNET", "eth0,10240,20480"); PeripheralCollector.ParseRemote(remote, "AQVOLUMES", "/\text4\t1024\t512\t400\t/dev/vda1");
            Check(remote.Network.SampleSeconds == 100.25 && remote.Network.Interfaces[0].SentBytes == 20480, "SSH counter parser preserves totals");
            Check(remote.Volumes.Single().TotalBytes == 1048576 && remote.Volumes[0].UsedPercent == 50 && remote.PrimaryStoragePath == "/", "SSH df KiB converted to bytes and real capacity");
            PeripheralCollector.ParseRemote(remote, "AQVOLUMES", "/mnt/Training Data\txfs\t2048\t1024\t900\t/dev/mapper/volume data");
            Check(remote.Volumes[1].Path == "/mnt/Training Data" && remote.Volumes[1].Label == "/dev/mapper/volume data", "SSH volume path and source retain spaces");
            var sample = Program.Fixture("eight"); var at = DateTime.UtcNow; var record = ArchiveRecord.From(sample, at, 5); sample.Network.Interfaces[0].Name = "changed"; sample.Volumes[0].UsedBytes = 1;
            Check(record.Network.Interfaces[0].Name == "eth0" && record.Volumes[0].UsedBytes > 1, "archive owns network and volume snapshot");
            var store = new ArchiveStore(Path.Combine(folder, "archive")); var server = Storage.LocalServer(); store.Append(server, record);
            var reopened = new ArchiveStore(store.Root); var query = reopened.Query(server, at.AddSeconds(-1), at.AddSeconds(1), CancellationToken.None); var restored = reopened.Read(query.Points.Single()).Metrics();
            Check(restored.Network.UploadBytesPerSecond == record.Network.UploadBytesPerSecond && restored.Volumes.Count == 2, "network and partitions survive disk reopen");
            var legacy = ArchiveStore.Json().Deserialize<ArchiveRecord>("{\"V\":1,\"Cpu\":\"21%\",\"Mem\":\"100 / 1000 MiB\"}").Metrics();
            Check(legacy.Network == null && legacy.Volumes.Count == 0 && legacy.Cpu == "21%", "legacy archives remain readable");
            Check(PeripheralMetrics.Capacity(1L << 49, 1L << 50).Length < 23 && PeripheralMetrics.Bytes(null, true) == "—", "large capacity units and unavailable rate formatting");
            var local = new Metrics(); PeripheralCollector.CollectLocal(local); Thread.Sleep(150); var localNext = new Metrics(); PeripheralCollector.CollectLocal(localNext); PeripheralMetrics.CalculateRates(localNext.Network, local.Network);
            Check(localNext.Volumes.Count > 0 && localNext.Volumes.All(x => x.TotalBytes > 0), "actual Windows volumes collected read-only");
            Check(localNext.Network.SampleSeconds > local.Network.SampleSeconds && localNext.Network.Interfaces.Count > 0 && localNext.Network.DownloadBytesPerSecond.HasValue, "actual Windows interfaces yield a measured rate");
            File.WriteAllText(Path.Combine(folder, "local-measurement.json"), ArchiveStore.Json().Serialize(ArchiveRecord.From(localNext, DateTime.UtcNow, 5)));
            File.WriteAllLines(Path.Combine(folder, "peripheral-results.txt"), new[] { "FAILURES=0", "CHECKS=" + log.Count }.Concat(log)); return 0;
        } catch (Exception error) { File.WriteAllText(Path.Combine(folder, "peripheral-error.txt"), error.ToString()); File.WriteAllLines(Path.Combine(folder, "peripheral-results.txt"), log); return 1; }
    }
}