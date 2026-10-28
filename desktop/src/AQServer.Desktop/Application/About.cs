using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public sealed class AboutWindow : Window
{
    readonly Action installExit;
    readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    readonly TextBlock status, versionLine, notes, sourceLine;
    readonly Button check, download, feedbackButton;
    readonly ProgressBar progress;
    CancellationTokenSource operation;
    ReleaseInfo release;
    string installer;
    bool busy, closed;

    public AboutWindow(Window owner, Action exit)
    {
        Owner = owner; installExit = exit;
        Title = "关于 AQ Server";
        Width = 580; Height = 600; MinWidth = 520; MinHeight = 560;
        MaxHeight = SystemParameters.WorkArea.Height - 32;
        WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        FontFamily = Theme.Chinese; FontSize = 15;
        Background = Theme.Bg; Foreground = Theme.Ink; Resources = Theme.Resources();
        Icon = owner == null ? null : owner.Icon;
        if (Icon == null)
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AQServer.ico"))
                Icon = System.Windows.Media.Imaging.BitmapFrame.Create(stream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.PreservePixelFormat,
                    System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
        var root = new Grid { Margin = new Thickness(28) };
        for (int i = 0; i < 6; i++)
            root.RowDefinitions.Add(new RowDefinition { Height = i == 2 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        Content = root;
        var brand = new Grid { Margin = new Thickness(0, 2, 0, 24) };
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });
        brand.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        brand.Children.Add(new Image { Source = Icon, Width = 52, Height = 52, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center });
        var heading = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        heading.Children.Add(Theme.Text("AQ Server", 26, Theme.Ink, false, true));
        versionLine = Theme.Text("版本 " + AppUpdates.DisplayVersion + " · Windows x64", 14, Theme.Muted);
        versionLine.Margin = new Thickness(0, 6, 0, 0);
        heading.Children.Add(versionLine);
        Grid.SetColumn(heading, 1); brand.Children.Add(heading); root.Children.Add(brand);
        var intro = new StackPanel { Margin = new Thickness(0, 0, 0, 20) };
        intro.Children.Add(Theme.Text("设备监控，安静常驻。", 16, Theme.Ink));
        status = Theme.Text("检查新版本，查看更新内容。", 14, Theme.Muted);
        status.Margin = new Thickness(0, 10, 0, 0); status.TextWrapping = TextWrapping.Wrap;
        intro.Children.Add(status); Grid.SetRow(intro, 1); root.Children.Add(intro);
        var card = new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 0, 18) };
        var content = new DockPanel();
        var title = Theme.Text("版本亮点", 15, Theme.Ink, false, true);
        title.Margin = new Thickness(0, 0, 0, 10); DockPanel.SetDock(title, Dock.Top); content.Children.Add(title);
        notes = new TextBlock {
            Text = "更清晰的底部指标与统一的图形比例\n合并历史时间筛选、导出与存档操作\n统一按钮、表单与深浅主题\n支持密码连接与问题反馈，固定官方更新渠道",
            TextWrapping = TextWrapping.Wrap, Foreground = Theme.Muted, FontSize = 15,
            LineHeight = 27, LineStackingStrategy = LineStackingStrategy.BlockLineHeight
        };
        content.Children.Add(new ScrollViewer { Content = notes, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        card.Child = content; Grid.SetRow(card, 2); root.Children.Add(card);
        var sourceRow = new DockPanel { Margin = new Thickness(0, 0, 0, 18) };
        feedbackButton = Theme.Nav("反馈问题", () => new FeedbackWindow(this).ShowDialog()); feedbackButton.MinWidth = 82;
        feedbackButton.Padding = new Thickness(8, 0, 8, 0);
        DockPanel.SetDock(feedbackButton, Dock.Right); sourceRow.Children.Add(feedbackButton);
        sourceLine = Theme.Text("", 13, Theme.Muted); sourceRow.Children.Add(sourceLine); UpdateSourceLine();
        Grid.SetRow(sourceRow, 3); root.Children.Add(sourceRow);
        progress = new ProgressBar { Height = 4, Minimum = 0, Maximum = 100, Foreground = Theme.Accent,
            Background = Theme.Line, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 14) };
        Grid.SetRow(progress, 4); root.Children.Add(progress);
        var footer = new Grid();
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var site = Theme.Nav("项目主页", () => Process.Start(new ProcessStartInfo("https://github.com/1159112519/aq-server") { UseShellExecute = true }));
        site.HorizontalAlignment = HorizontalAlignment.Left; footer.Children.Add(site);
        check = Theme.Button("检查更新", Check); check.MinWidth = 110; Grid.SetColumn(check, 1); footer.Children.Add(check);
        download = Theme.Button("更新并重启", Download); download.MinWidth = 110; download.Margin = new Thickness(10, 0, 0, 0);
        download.Visibility = Visibility.Collapsed; download.BorderBrush = Theme.Accent;
        Grid.SetColumn(download, 2); footer.Children.Add(download); Grid.SetRow(footer, 5); root.Children.Add(footer);
        Closed += (s, e) => { closed = true; lifetime.Cancel(); };
    }

    void UpdateSourceLine()
    {
        string address = UpdateSources.CurrentEndpoint;
        sourceLine.Text = "官方更新 · 发布签名校验";
        sourceLine.ToolTip = address;
    }

    void Busy(bool value)
    {
        busy = value; check.Content = value ? "取消" : "检查更新";
        download.IsEnabled = feedbackButton.IsEnabled = !value;
    }

    async void Check()
    {
        if (busy) { if (operation != null) operation.Cancel(); return; }
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var currentOperation = operation;
        Busy(true); status.Text = "正在连接更新服务…";
        progress.IsIndeterminate = true; progress.Visibility = Visibility.Visible;
        try {
            var info = await AppUpdates.Check(currentOperation.Token);
            if (closed) return;
            release = info; installer = null; notes.Text = info.notes ?? "";
            if (info.ParsedVersion > AppUpdates.CurrentVersion) {
                status.Text = "发现新版本 " + info.version;
                download.Content = "更新并重启"; download.Visibility = Visibility.Visible;
            } else {
                status.Text = "当前已是最新版本（" + AppUpdates.DisplayVersion + "）";
                download.Visibility = Visibility.Collapsed;
            }
        } catch (Exception error) {
            if (!closed) { status.Text = AppUpdates.FriendlyError(error); download.Visibility = Visibility.Collapsed; }
        } finally {
            currentOperation.Dispose(); operation = null;
            if (!closed) { Busy(false); progress.Visibility = Visibility.Collapsed; }
        }
    }

    async void Download()
    {
        if (busy || release == null) return;
        if (MessageBox.Show(this, "更新至 AQ Server " + release.version + " 并重启？\n\n下载和校验后将自动关闭程序、安装并重新打开。终端会话会结束，连接配置和本地历史会保留。",
            "更新并重启", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
        if (installer != null) { Install(); return; }
        operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var currentOperation = operation;
        Busy(true); progress.Value = 0; progress.IsIndeterminate = false;
        progress.Visibility = Visibility.Visible; status.Text = "正在下载更新…";
        try {
            installer = await AppUpdates.Download(release, new Progress<double>(value => {
                if (!closed && busy && object.ReferenceEquals(operation, currentOperation)) {
                    progress.Value = value; status.Text = "正在下载更新 · " + value.ToString("0") + "%";
                }
            }), currentOperation.Token);
            if (closed) return;
            status.Text = "校验通过，正在准备安装并重启…"; Install();
        } catch (Exception error) { if (!closed) status.Text = AppUpdates.FriendlyError(error); }
        finally {
            currentOperation.Dispose(); operation = null;
            if (!closed) { Busy(false); progress.Visibility = Visibility.Collapsed; }
        }
    }

    void Install()
    {
        try {
            using (var process = UpdateInstallation.Start(installer, release)) {
                status.Text = "正在保存并安装更新…";
                Close(); if (installExit != null) installExit();
            }
        } catch (Exception error) {
            status.Text = "安装未启动，当前程序仍在运行。" + AppUpdates.FriendlyError(error);
            download.Content = "重试更新";
        }
    }


    public void Render(string file) { Snapshot.Save((Grid)Content, file, 580, 600, 144, null); }
}

public sealed partial class MonitorWindow
{
    void OpenAbout() { new AboutWindow(this, Exit).ShowDialog(); }
}
