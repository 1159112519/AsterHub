using System;
using System.Globalization;
using System.Text.RegularExpressions;
public class ResourceSample {
 public DateTime Time; public double Cpu,Memory;public double? Download,Upload,Storage;
 public static ResourceSample From(Metrics m,DateTime time){var volume=PeripheralMetrics.PrimaryVolume(m);return new ResourceSample{Time=time,Cpu=ParsePercent(m.Cpu),Memory=MemoryPercent(m.Mem),Download=m.Network==null?null:m.Network.DownloadBytesPerSecond,Upload=m.Network==null?null:m.Network.UploadBytesPerSecond,Storage=volume==null?(double?)null:volume.UsedPercent};}
 public static double ParsePercent(string s){double n;return double.TryParse((s??"").TrimEnd('%'),NumberStyles.Float,CultureInfo.InvariantCulture,out n)?Math.Max(0,Math.Min(100,n)):double.NaN;}
 public static double MemoryPercent(string s){var a=Regex.Matches(s??"",@"[0-9.]+");if(a.Count<2)return double.NaN;double used=MetricMath.Number(a[0].Value),total=MetricMath.Number(a[1].Value);return total>0?Math.Min(100,100*used/total):double.NaN;}
 public static string MemoryLabel(string s){var a=Regex.Matches(s??"",@"[0-9.]+");return a.Count<2?"—":(MetricMath.Number(a[0].Value)/1024).ToString("0.0")+" / "+(MetricMath.Number(a[1].Value)/1024).ToString("0.0")+" GiB";}
}
