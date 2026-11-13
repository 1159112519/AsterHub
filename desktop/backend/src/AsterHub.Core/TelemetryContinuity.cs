using System;
using System.Text.RegularExpressions;

// Shared policy for explicit transport interruptions in the live and archived feeds.
public static class TelemetryContinuity {
 static readonly Regex disconnect=new Regex(@"\bconnection\b[^\r\n]{0,120}\b(?:closed|reset|aborted|refused|lost)\b|\bdisconnected\b|\bnot connected\b|\bbroken pipe\b|\bnetwork is unreachable\b|\bno route to host\b|\bsoftware caused connection abort\b|连接(?:已|被)?(?:中断|断开|重置|关闭|拒绝)|强(?:迫|行)关闭|网络不可达|没有到主机的路由",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
 public static bool IsDisconnect(string error){return !string.IsNullOrEmpty(error)&&disconnect.IsMatch(error);}
 public static double GapSeconds(double cadence,bool missed){double limit=Math.Max(1,2.5*cadence);return missed?Math.Min(12,limit):limit;}
}
