using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Net.NetworkInformation;

public static class PeripheralCollector
{
    public const string VolumeAwk = "NR>1 { if(match($0,/[[:space:]]+[^[:space:]]+[[:space:]]+[0-9]+[[:space:]]+[0-9]+[[:space:]]+[0-9]+[[:space:]]+[0-9]+%[[:space:]]+/)) { source=substr($0,1,RSTART-1); target=substr($0,RSTART+RLENGTH); split(substr($0,RSTART,RLENGTH),f); printf \"%s\\t%s\\t%s\\t%s\\t%s\\t%s\\n\",target,f[1],f[2],f[3],f[4],source } }";
    public const string RemoteCommand = "printf 'AQNETTIME\\n'; cat /proc/uptime 2>/dev/null; printf 'AQNET\\n'; awk 'FNR==NR{if(NR>1 && $2==\"00000000\") wanted[$1]=1;next} /:/{split($0,a,\":\");n=a[1];gsub(/ /,\"\",n);split(a[2],b);if(n!=\"lo\" && (length(wanted)==0 || n in wanted))printf \"%s,%s,%s\\n\",n,b[1],b[9]}' /proc/net/route /proc/net/dev 2>/dev/null; printf 'AQVOLUMES\\n'; LC_ALL=C df -PTk -x tmpfs -x devtmpfs -x squashfs 2>/dev/null | awk " + "'" + VolumeAwk + "'; ";

    public static void CollectLocal(Metrics metrics)
    {
        metrics.Network = new NetworkMetrics { SampleSeconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency };
        try {
            foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces()) {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                // Virtual switches are counted at the active host adapter, avoiding duplicate bridge traffic.
                if (adapter.Description.IndexOf("Hyper-V Virtual Ethernet", StringComparison.OrdinalIgnoreCase) >= 0 && !adapter.GetIPProperties().GatewayAddresses.Any()) continue;
                try {
                    var counters = adapter.GetIPv4Statistics();
                    metrics.Network.Interfaces.Add(new NetworkCounter { Id = adapter.Id, Name = adapter.Name, ReceivedBytes = counters.BytesReceived, SentBytes = counters.BytesSent,
                        Description=adapter.Description,Kind=adapter.NetworkInterfaceType.ToString(),LinkSpeed=adapter.Speed,
                        IPv4=string.Join(", ",adapter.GetIPProperties().UnicastAddresses.Where(x=>x.Address.AddressFamily==AddressFamily.InterNetwork).Select(x=>x.Address.ToString())),
                        IPv6=string.Join(", ",adapter.GetIPProperties().UnicastAddresses.Where(x=>x.Address.AddressFamily==AddressFamily.InterNetworkV6).Select(x=>x.Address.ToString())),
                        Mac=adapter.GetPhysicalAddress().ToString(),ReceivedPackets=counters.UnicastPacketsReceived,SentPackets=counters.UnicastPacketsSent,
                        ReceiveErrors=counters.IncomingPacketsWithErrors,SendErrors=counters.OutgoingPacketsWithErrors,ReceiveDiscards=counters.IncomingPacketsDiscarded,SendDiscards=counters.OutgoingPacketsDiscarded });
                } catch (NetworkInformationException) { }
            }
        } catch (Exception error) { metrics.Network.Error = error.GetType().Name; }
        try{metrics.Network.TcpConnections=IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Count(x=>x.State==TcpState.Established);}catch(NetworkInformationException){}
        try{using(var search=new ManagementObjectSearcher("root\\CIMV2","SELECT Name,DiskReadBytesPersec,DiskWriteBytesPersec,CurrentDiskQueueLength,PercentIdleTime FROM Win32_PerfFormattedData_PerfDisk_PhysicalDisk")){search.Options.Timeout=TimeSpan.FromSeconds(2);using(var result=search.Get())foreach(ManagementObject row in result)using(row){string name=Convert.ToString(row["Name"]);if(name=="_Total")continue;metrics.Storage.Add(new DiskActivity{Id=name,Name="磁盘 "+name,ReadBytesPerSecond=Convert.ToDouble(row["DiskReadBytesPersec"]),WriteBytesPerSecond=Convert.ToDouble(row["DiskWriteBytesPersec"]),QueueDepth=Convert.ToDouble(row["CurrentDiskQueueLength"]),ActivePercent=Math.Max(0,Math.Min(100,100-Convert.ToDouble(row["PercentIdleTime"])))});}}}catch(ManagementException){}catch(UnauthorizedAccessException){}catch(System.Runtime.InteropServices.COMException){}
        metrics.PrimaryStoragePath = Path.GetPathRoot(Environment.SystemDirectory);
        foreach (var drive in DriveInfo.GetDrives()) {
            try {
                if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)) continue;
                metrics.Volumes.Add(new StorageVolume { Path = drive.Name, Label = drive.VolumeLabel, FileSystem = drive.DriveFormat,
                    TotalBytes = drive.TotalSize, UsedBytes = drive.TotalSize - drive.TotalFreeSpace, AvailableBytes = drive.AvailableFreeSpace });
            } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    public static void ParseRemote(Metrics metrics, string section, string line)
    {
        if (section == "AQNETTIME") {
            double seconds; if (double.TryParse(line.Split(' ')[0], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds)) {
                if (metrics.Network == null) metrics.Network = new NetworkMetrics(); metrics.Network.SampleSeconds = seconds;
            }
        } else if (section == "AQNET") {
            var parts = line.Split(','); long down, up;
            if (parts.Length == 3 && long.TryParse(parts[1], out down) && long.TryParse(parts[2], out up)) {
                if (metrics.Network == null) metrics.Network = new NetworkMetrics();
                metrics.Network.Interfaces.Add(new NetworkCounter { Id = parts[0], Name = parts[0], ReceivedBytes = down, SentBytes = up });
            }
        } else if (section == "AQVOLUMES") {
            var parts = line.Split('\t'); long total, used, free;
            if (parts.Length == 6 && long.TryParse(parts[2], out total) && long.TryParse(parts[3], out used) && long.TryParse(parts[4], out free))
                metrics.Volumes.Add(new StorageVolume { Path = parts[0].Replace("\\040", " "), FileSystem = parts[1], Label = parts[5], TotalBytes = total * 1024, UsedBytes = used * 1024, AvailableBytes = free * 1024 });
            metrics.PrimaryStoragePath = "/";
        }
    }
}