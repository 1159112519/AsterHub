using System.Threading;

internal sealed class StartupUpdatePolicy {
 int attempted;
 internal bool TryBegin(bool enabled){return Interlocked.Exchange(ref attempted,1)==0&&enabled;}
 internal static bool ShouldOffer(ReleaseInfo verified,string current){return verified!=null&&AppUpdates.CompareVersions(verified.version,current)>0;}
}
