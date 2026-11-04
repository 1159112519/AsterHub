using System;
// Lightweight CPU/memory samples use the requested millisecond interval. Full
// GPU/network/storage collection has a two-second floor because it invokes providers.
public static class DeviceSamplingPolicy {
 public static int LocalFullIntervalMilliseconds(int localMilliseconds){return Math.Max(2000,Math.Min(120000,localMilliseconds));}
 public static int FullIntervalSeconds(bool local,int remoteSeconds,int localMilliseconds){return local?(int)Math.Ceiling(LocalFullIntervalMilliseconds(localMilliseconds)/1000.0):Math.Max(2,Math.Min(120,remoteSeconds));}
}