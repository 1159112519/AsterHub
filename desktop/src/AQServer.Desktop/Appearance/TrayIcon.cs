using System;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

public sealed partial class MonitorWindow
{
    void RefreshTrayIcon()
    {
        if (tray == null) return;
        bool lightTaskbar = false;
        try {
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                lightTaskbar = key != null && Convert.ToInt32(key.GetValue("SystemUsesLightTheme", 0)) != 0;
        } catch { }
        string resource = lightTaskbar ? "AQTrayDark.ico" : "AQTrayLight.ico";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
        using (var icon = new System.Drawing.Icon(stream)) {
            var replacement = (System.Drawing.Icon)icon.Clone();
            var previous = trayIcon; trayIcon = replacement; tray.Icon = replacement;
            if (previous != null) previous.Dispose();
        }
    }

    void OnTrayThemeChanged(object sender, UserPreferenceChangedEventArgs args)
    {
        if (tray != null && !Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke((Action)RefreshTrayIcon);
    }
}