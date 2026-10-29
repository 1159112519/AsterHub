using System;
using System.Diagnostics;
using System.IO;

public static class UpdateInstallation
{
    static string Quote(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
            throw new ArgumentException("更新路径无效。");
        return "\"" + value.TrimEnd('\\') + "\"";
    }
    internal static ProcessStartInfo CreateStartInfo(string installer, string installRoot, string log, int parentPid, string restartProof = null)
    {
        if (parentPid <= 0) throw new ArgumentOutOfRangeException("parentPid");
        var arguments = "/SILENT /SP- /NORESTART /UPDATE=1 /AQPID=" + parentPid +
            " /DIR=" + Quote(Path.GetFullPath(installRoot)) + " /LOG=" + Quote(Path.GetFullPath(log));
        if (restartProof != null) arguments += " /AQRESTARTPROOF=" + Quote(Path.GetFullPath(restartProof));
        return new ProcessStartInfo(Path.GetFullPath(installer), arguments) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(installer)) };
    }
    public static Process Start(string installer, ReleaseInfo release)
    {
        return StartAt(installer, release, AppDomain.CurrentDomain.BaseDirectory, null);
    }
    internal static Process StartAt(string installer, ReleaseInfo release, string installRoot, string restartProof)
    {
        AppUpdates.VerifyInstaller(installer, release);
        var logRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(installer)), "Logs");
        Directory.CreateDirectory(logRoot);
        var log = Path.Combine(logRoot, "install-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".log");
        var start = CreateStartInfo(installer, installRoot, log, Process.GetCurrentProcess().Id, restartProof);
        var process = Process.Start(start);
        if (process == null) throw new IOException("无法启动更新安装程序，当前程序仍在运行。");
        return process;
    }
}
