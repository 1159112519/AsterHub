using System;using System.IO;using System.Linq;using System.Text;using System.Reflection;using System.Threading;using System.Threading.Tasks;
internal static class BootstrapCore {
 internal const string TargetVersion="0.3.0-preview.22";
 internal static string JoinArguments(string[] args){return string.Join(" ",args.Select(Quote));}
 static string Quote(string value){if(value==null)throw new ArgumentNullException("value");var s=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"')s.Append('\\',slashes*2+1);else s.Append('\\',slashes);s.Append(c);slashes=0;}s.Append('\\',slashes*2);return s.Append('"').ToString();}
 internal static ReleaseInfo ReadRelease(){using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("release.json")){if(input==null)throw new InvalidDataException("升级程序缺少版本信息。");using(var output=new MemoryStream()){input.CopyTo(output);var release=AppUpdates.VerifyManifest(output.ToArray(),UpdatePublicKey.Xml);Validate(release);return release;}}}
 internal static void Validate(ReleaseInfo r){if(r==null||r.version!=TargetVersion||!new Uri(r.installerUrl).AbsolutePath.EndsWith("/AsterHub-Setup-"+TargetVersion+".exe",StringComparison.Ordinal))throw new InvalidDataException("升级程序的版本信息不匹配。");}
 internal static async Task<string> Download(string folder,IProgress<double> progress,CancellationToken token){var r=ReadRelease();var path=await AppUpdates.DownloadTo(r,progress,token,folder).ConfigureAwait(false);AppUpdates.VerifyInstaller(path,r);return path;}
}
