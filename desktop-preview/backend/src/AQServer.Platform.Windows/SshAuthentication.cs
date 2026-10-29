using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Renci.SshNet;
using Renci.SshNet.Common;

// Passwords never enter command arguments, logs, or plaintext XML.
public static class SshAuthentication
{
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("AQServer/SSH/password/v1");
    static readonly object HostsGate = new object();
    public static bool Managed(Server server) { return server.AuthMode == "password" || server.AuthMode == "none"; }
    public static string SessionIdentity(Server server) { using(var h=SHA256.Create()) return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(server.Password??""))); }
    public static string Protect(string password) {
        var bytes = Encoding.UTF8.GetBytes(password ?? "");
        try { return Convert.ToBase64String(ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser)); }
        finally { Array.Clear(bytes, 0, bytes.Length); }
    }
    public static string Password(Server server) {
        if (server.Password != null) return server.Password;
        if (string.IsNullOrEmpty(server.ProtectedPassword)) return "";
        try {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(server.ProtectedPassword), Entropy, DataProtectionScope.CurrentUser);
            try { return Encoding.UTF8.GetString(bytes); } finally { Array.Clear(bytes, 0, bytes.Length); }
        } catch { throw new InvalidOperationException("无法读取此 Windows 账户保存的密码，请在连接设置中重新输入。"); }
    }
    sealed class Target { public string Host, User, Lookup; public int Port; public List<string> Known = new List<string>(); }
    static string Run(string exe, string args) {
        using (var p = new Process { StartInfo = new ProcessStartInfo(exe, args) { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8 } }) {
            p.Start(); var output=p.StandardOutput.ReadToEndAsync(); var error=p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(10000)) { try { p.Kill(); } catch {} throw new TimeoutException("读取 SSH 配置超时。"); }
            if (p.ExitCode!=0 && !(exe=="ssh-keygen.exe" && p.ExitCode==1)) throw new IOException("无法读取 SSH 配置或已知主机文件。");
            return output.Result;
        }
    }
    static Target Resolve(Server s) {
        if (!Regex.IsMatch(s.Host??"", @"^[a-zA-Z0-9._:-]+$") || !Regex.IsMatch(s.User??"", @"^[a-zA-Z0-9._-]*$") || s.Port<1 || s.Port>65535) throw new ArgumentException("服务器地址、用户名或端口无效。");
        var t = new Target { Host=s.Host, User=s.User, Port=s.Port }; string alias=null;
        string cfg=Run("ssh.exe", "-G -p "+s.Port+(string.IsNullOrWhiteSpace(s.User)?"":" -l "+ConPtySession.Quote(s.User))+" -- "+ConPtySession.Quote(s.Host));
        foreach (string line in cfg.Split('\n')) {
            int pos=line.IndexOf(' '); if (pos<0) continue; string key=line.Substring(0,pos), value=line.Substring(pos+1).Trim();
            if (key=="hostname") t.Host=value;
            else if (key=="user" && string.IsNullOrWhiteSpace(t.User)) t.User=value;
            else if (key=="hostkeyalias") alias=value;
            else if ((key=="proxyjump" || key=="proxycommand") && value!="none") throw new NotSupportedException("密码及无密码模式暂不支持 SSH 跳板配置，请使用密钥连接。");
            else if (key=="userknownhostsfile" || key=="globalknownhostsfile") foreach (Match m in Regex.Matches(value,"\"[^\"]+\"|\\S+")) t.Known.Add(m.Value.Trim('"'));
        }
        string target=string.IsNullOrWhiteSpace(alias)?t.Host:alias; t.Lookup=t.Port==22?target:"["+target+"]:"+t.Port;
        return t;
    }
    public static string KnownHostsPath;
    public static string HostOptions { get { return string.IsNullOrEmpty(KnownHostsPath)?"":" -o GlobalKnownHostsFile=NUL -o UserKnownHostsFile="+ConPtySession.Quote(KnownHostsPath)+" "; } }
    static string UserHosts { get { return string.IsNullOrEmpty(KnownHostsPath)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".ssh","known_hosts"):KnownHostsPath; } }
    static void VerifyHost(Target t, HostKeyEventArgs e) {
        e.CanTrust=false;
        lock (HostsGate) {
            string key=Convert.ToBase64String(e.HostKey); bool any=false, match=false, revoked=false;
            var paths=string.IsNullOrEmpty(KnownHostsPath)?t.Known.Concat(new[]{UserHosts}).Distinct():new[]{UserHosts};
            foreach (string raw in paths) {
                string path=Environment.ExpandEnvironmentVariables(raw.Replace("~",Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
                if (!File.Exists(path)) continue;
                foreach(string line in Run("ssh-keygen.exe","-F "+ConPtySession.Quote(t.Lookup)+" -f "+ConPtySession.Quote(path)).Split('\n')) {
                    string[] a=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
                    if (a.Length==0 || a[0].StartsWith("#")) continue;
                    if (a[0]=="@revoked") { if(a.Length>=4 && a[3]==key) revoked=true; continue; }
                    if (a[0].StartsWith("@")) { any=true; continue; }
                    if(a.Length>=3) { any=true; if(a[2]==key) match=true; }
                }
            }
            if (revoked || (any && !match)) return;
            if (!any) {
                // Same accept-new policy as existing OpenSSH: pin first use, reject replacements.
                Directory.CreateDirectory(Path.GetDirectoryName(UserHosts));
                File.AppendAllText(UserHosts,"\n"+t.Lookup+" "+e.HostKeyName+" "+key+"\n",new UTF8Encoding(false));
            }
            e.CanTrust=true;
        }
    }
    static ConnectionInfo Info(Server s, Target t) {
        var auth = new List<AuthenticationMethod>();
        if (s.AuthMode=="none") auth.Add(new NoneAuthenticationMethod(t.User));
        else {
            string password=Password(s); auth.Add(new PasswordAuthenticationMethod(t.User,password));
            var interactive=new KeyboardInteractiveAuthenticationMethod(t.User);
            interactive.AuthenticationPrompt+=(sender,e)=>{ foreach(var prompt in e.Prompts) {
                if (Regex.IsMatch(prompt.Request??"", @"password|密码", RegexOptions.IgnoreCase)) prompt.Response=password;
                else throw new SshAuthenticationException("此服务器需要额外认证，请使用支持该认证的 SSH 配置。");
            }};
            auth.Add(interactive);
        }
        return new ConnectionInfo(t.Host,t.Port,t.User,auth.ToArray()) { Timeout=TimeSpan.FromSeconds(12), Encoding=Encoding.UTF8 };
    }
    public static SshClient Client(Server s) { var t=Resolve(s); var c=new SshClient(Info(s,t)); c.KeepAliveInterval=TimeSpan.FromSeconds(25); c.HostKeyReceived+=(o,e)=>VerifyHost(t,e); return c; }
    public static SftpClient Files(Server s) { var t=Resolve(s); var c=new SftpClient(Info(s,t)); c.KeepAliveInterval=TimeSpan.FromSeconds(25); c.HostKeyReceived+=(o,e)=>VerifyHost(t,e); return c; }
    public static string Execute(Server s,string command,int seconds) {
        using(var c=Client(s)) { c.Connect(); using(var cmd=c.CreateCommand(command)) { cmd.CommandTimeout=TimeSpan.FromSeconds(seconds); string output=cmd.Execute(); if(cmd.ExitStatus!=0) throw new IOException(string.IsNullOrWhiteSpace(cmd.Error)?"SSH 命令未完成。":cmd.Error.Trim()); return output; } }
    }
}
