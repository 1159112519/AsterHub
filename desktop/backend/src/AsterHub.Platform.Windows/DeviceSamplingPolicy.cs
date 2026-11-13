using System;
// One requested cadence for the complete local snapshot; do not silently floor GPU/IO at two seconds.
public static class DeviceSamplingPolicy {
 public static int LocalFullIntervalMilliseconds(int localMilliseconds){return Math.Max(500,Math.Min(120000,localMilliseconds));}
 public static int FullIntervalMilliseconds(bool local,int remoteSeconds,int localMilliseconds){return local?LocalFullIntervalMilliseconds(localMilliseconds):Math.Max(2,Math.Min(120,remoteSeconds))*1000;}
 public static int FullIntervalSeconds(bool local,int remoteSeconds,int localMilliseconds){return (int)Math.Ceiling(FullIntervalMilliseconds(local,remoteSeconds,localMilliseconds)/1000.0);}
}
