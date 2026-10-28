using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public sealed partial class MonitorWindow
{
    string cpuTab = "overview", memoryTab = "overview";
    Border trendSurface;

    void BuildHeader()
    {
        var bar = new Grid { Height = 92, Margin = new Thickness(24, 0, 24, 0) };
        workspaceHeader = bar;
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 4; i++) bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var identity = new Grid { VerticalAlignment = VerticalAlignment.Center };
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        identity.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pageIcon = new IconView("Cpu", 21, Theme.Accent);
        pageIconTile = new Border { Height = 40, Width = 40, CornerRadius = new CornerRadius(11), Background = Theme.Raised, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Child = pageIcon };
        identity.Children.Add(pageIconTile);
        var title = new StackPanel { Margin = new Thickness(14, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        TitleText = Theme.Text("处理器", 23, Theme.Ink, false, true); title.Children.Add(TitleText);
        ServerPicker = new ComboBox { MinWidth = 110, MaxWidth = 220, Height = 26, MinHeight = 26, FontSize = 13, FontWeight = FontWeights.Normal, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), HorizontalAlignment = HorizontalAlignment.Left, VerticalContentAlignment = VerticalAlignment.Center };
        RefreshPickerTemplate(); title.Children.Add(ServerPicker);
        shellHeading = Theme.Text("", 14, Theme.Ink); shellHeading.Visibility = Visibility.Collapsed;
        shellAddress = Theme.Text("", 12, Theme.Muted); shellAddress.Visibility = Visibility.Collapsed;
        Subtitle = Theme.Text("", 14, Theme.Muted); Subtitle.Visibility = Visibility.Collapsed;
        Grid.SetColumn(title, 1); identity.Children.Add(title); bar.Children.Add(identity);
        ServerPicker.SelectionChanged += (a, b) => {
            active = ServerPicker.SelectedItem as Server; requestVersion++;
            if (active == null) return;
            config.SelectedName = active.Name; ShowSelectedDevice(); RefreshDeviceRail();
            if (Workspace != null) Workspace.SetDevices(config.Servers, active);
            if (TerminalView != null) TerminalView.SetDefaultServer(active);
            if (View == "文件" && FileView != null) FileView.SetServer(active);
            if (View == "历史" || View == "终端" || View == "文件") RenderView();
            RequestConnectionHealth();
        };
        Connection = Theme.Text("连接中", 13, Theme.Muted); Connection.Width = 82; Connection.TextAlignment = TextAlignment.Right; Connection.Margin = new Thickness(8, 0, 16, 0);
        Grid.SetColumn(Connection, 1); bar.Children.Add(Connection);
        Updated = Theme.Text("", 12, Theme.Muted); Updated.Visibility = Visibility.Collapsed;
        TerminalButton = Theme.IconAction("ArrowDownUp", "", ToggleTransfers); TerminalButton.ToolTip = "传输队列";
        CompactButton = Theme.IconAction("PictureInPicture2", "", ShowCompact); CompactButton.ToolTip = "置顶速览 · Ctrl+M";
        SettingsButton = Theme.IconAction("SlidersHorizontal", "", OpenSettings); SettingsButton.ToolTip = "偏好设置";
        var buttons = new[] { TerminalButton, CompactButton, SettingsButton };
        for (int i = 0; i < buttons.Length; i++) {
            var button = buttons[i]; button.Width = button.Height = button.MinHeight = 38;
            button.Background = Theme.Raised; button.BorderBrush = Theme.Line; button.BorderThickness = new Thickness(1);
            button.Padding = new Thickness(9); button.Margin = new Thickness(i == 0 ? 0 : 8, 0, 0, 0);
            Grid.SetColumn(button, i + 2); bar.Children.Add(button);
        }
        Grid.SetColumn(bar, 1); Root.Children.Add(bar);
    }

    void BuildSection()
    {
        workspaceSection = new Grid { Margin = new Thickness(24, 0, 24, 0) };
        Grid.SetColumn(workspaceSection, 1); Grid.SetRow(workspaceSection, 1); Root.Children.Add(workspaceSection);
    }

    static string PageSymbol(string name)
    {
        switch (name) { case "CPU": return "Cpu"; case "GPU": return "Gpu"; case "内存": return "MemoryStick"; case "网络": return "Network"; case "存储": return "HardDrive"; case "终端": return "Terminal"; case "文件": return "FolderOpen"; default: return "History"; }
    }

    void BuildNavigation()
    {
        viewButtons.Clear(); railNavigation.Children.Clear(); railGroupLabels.Clear();
        string[] pages = { "CPU", "GPU", "内存", "网络", "存储", "终端", "文件", "历史" };
        for (int i = 0; i < pages.Length; i++) {
            if (i == 0 || i == 5) {
                var label = Theme.Text(i == 0 ? "资源监控" : "工作台", 12, Theme.Muted);
                label.Margin = new Thickness(12, i == 0 ? 8 : 20, 0, 8); railNavigation.Children.Add(label); railGroupLabels.Add(label);
            }
            string name = pages[i];
            var button = Theme.Action("", () => SwitchView(name));
            var content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) }); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.Children.Add(new IconView(PageSymbol(name), 19));
            var labelText = Theme.Text(name == "CPU" ? "处理器" : name == "GPU" ? "显卡" : name, 15, Theme.Ink);
            labelText.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(labelText, 1); content.Children.Add(labelText);
            button.Content = content; button.ToolTip = labelText.Text; button.Tag = name;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Padding = new Thickness(12, 0, 12, 0);
            button.Height = button.MinHeight = 44; button.Margin = new Thickness(0, 2, 0, 2);
            railNavigation.Children.Add(button); viewButtons.Add(button);
        }
        UpdatePageHeader(); BuildResourceNavigation();
    }

    void UpdatePageHeader()
    {
        if (pageIcon != null) { pageIcon.Symbol = PageSymbol(View); pageIcon.InvalidateVisual(); }
        if (TitleText != null) TitleText.Text = View == "CPU" ? "CPU 运行状态" : View == "GPU" ? "GPU 资源" : View == "内存" ? "内存使用情况" : View == "终端" ? "终端工作区" : View == "文件" ? "文件与传输" : View == "网络" ? "网络流量" : View == "存储" ? "存储空间" : "历史记录与存档";
        for (int i = 0; i < viewButtons.Count; i++) {
            bool selected = (string)viewButtons[i].Tag == View;
            viewButtons[i].Background = selected ? Theme.AccentSoft : Brushes.Transparent;
            var content = (Grid)viewButtons[i].Content;
            ((IconView)content.Children[0]).Stroke = selected ? Theme.Accent : Theme.Muted;
            ((IconView)content.Children[0]).InvalidateVisual();
            ((TextBlock)content.Children[1]).FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal;
        }
    }

    void BuildResourceNavigation()
    {
        if (workspaceSection == null) return;
        workspaceSection.Children.Clear();
        bool resource = View == "CPU" || View == "内存";
        workspaceSection.Height = resource ? 48 : 0;
        if (!resource) return;
        var wrap = new StackPanel { Orientation = Orientation.Horizontal };
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        string[] ids = View == "CPU" ? new[] { "overview", "cores", "hardware" } : new[] { "overview", "hardware" };
        string current = View == "CPU" ? cpuTab : memoryTab;
        foreach (string id in ids) {
            string selected = id;
            var button = Theme.Nav(id == "overview" ? "总览" : id == "cores" ? "逐核" : "硬件", () => SelectResourceTab(selected));
            button.MinWidth = 80; button.Height = button.MinHeight = 36;
            button.Background = id == current ? Theme.AccentSoft : Brushes.Transparent;
            button.Foreground = id == current ? Theme.Ink : Theme.Muted;
            button.FontWeight = id == current ? FontWeights.SemiBold : FontWeights.Normal;
            button.Margin = new Thickness(0, 0, 2, 0); tabs.Children.Add(button);
        }
        wrap.Children.Add(new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Child = tabs });
        workspaceSection.Children.Add(wrap);
    }

    internal void SelectResourceTab(string name)
    {
        string previous = View == "CPU" ? cpuTab : memoryTab;
        if (name == previous) return;
        if (View == "CPU") { cpuTab = name; coresExpanded = name == "cores"; }
        else memoryTab = name;
        BuildResourceNavigation(); ApplyCoreVisibility(); Scroll.ScrollToTop();
        if (!testMode) Motion.Enter(Body);
    }

    public void ShowCores()
    {
        if (View != "CPU") SwitchView("CPU");
        SelectResourceTab(cpuTab == "cores" ? "overview" : "cores");
    }

    void ApplyCoreVisibility()
    {
        bool cpu = View == "CPU"; string selected = cpu ? cpuTab : memoryTab;
        if (trendSurface != null) trendSurface.Visibility = selected == "overview" ? Visibility.Visible : Visibility.Collapsed;
        if (coresPanel != null) coresPanel.Visibility = cpu && selected == "cores" ? Visibility.Visible : Visibility.Collapsed;
        if (hardwareHost != null) hardwareHost.Visibility = selected == "hardware" ? Visibility.Visible : Visibility.Collapsed;
    }

    void BuildTrend()
    {
        hardwareHost = null; shownHardware = null; coreGrid = null; coreHeading = null; coresPanel = null;
        trendPanel = new StackPanel();
        var head = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var label = new StackPanel(); label.Children.Add(Theme.Text(View == "CPU" ? "处理器利用率" : "物理内存利用率", 14, Theme.Muted));
        trendValue = Theme.Text("—", 44, Theme.Green, true, true); trendValue.Margin = new Thickness(0, 6, 0, 0); label.Children.Add(trendValue); head.Children.Add(label);
        trendInfo = Theme.Text("", 14, Theme.Muted); trendInfo.TextWrapping = TextWrapping.Wrap; trendInfo.TextAlignment = TextAlignment.Right; trendInfo.VerticalAlignment = VerticalAlignment.Center; trendInfo.Margin = new Thickness(24, 0, 0, 0);
        Grid.SetColumn(trendInfo, 1); head.Children.Add(trendInfo); trendPanel.Children.Add(head);
        chart = new TrendChart { Height = Math.Max(200, Math.Min(340, Scroll.ActualHeight - 220)), HorizontalAlignment = HorizontalAlignment.Stretch, Data = History, Memory = View == "内存", Interval = config.Interval };
        trendPanel.Children.Add(chart);
        var caption = Theme.Text("最近 5 分钟", 13, Theme.Muted); caption.Margin = new Thickness(0, 16, 0, 0); trendPanel.Children.Add(caption);
        trendSurface = new Border { CornerRadius = new CornerRadius(12), Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(24), Child = trendPanel };
        Body.Children.Add(trendSurface);
        hardwareHost = new StackPanel(); Body.Children.Add(hardwareHost); UpdateHardwarePanel();
        if (View == "CPU") {
            var cores = new StackPanel(); coreHeading = Theme.Text("逐核占用率", 16, Theme.Ink, false, true); coreHeading.TextWrapping = TextWrapping.Wrap; coreHeading.Margin = new Thickness(0, 0, 0, 20); cores.Children.Add(coreHeading);
            coreGrid = new WrapPanel { Margin = new Thickness(0, 0, -8, -8) }; coreGrid.SizeChanged += (s, e) => LayoutCoreTiles(); cores.Children.Add(coreGrid);
            coresPanel = new Border { CornerRadius = new CornerRadius(12), Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(24), Child = cores };
            Body.Children.Add(coresPanel);
        }
        ApplyCoreVisibility(); UpdateTrend();
    }

    void LayoutCoreTiles()
    {
        if (coreGrid == null || coreGrid.ActualWidth < 100) return;
        int count = Math.Max(1, (int)(coreGrid.ActualWidth / 104));
        double width = Math.Floor(coreGrid.ActualWidth / count) - 8;
        foreach (FrameworkElement tile in coreGrid.Children) tile.Width = width;
    }

    void UpdateCores()
    {
        if (coreGrid == null) return;
        if (last == null || !string.IsNullOrEmpty(last.Error)) { coreHeading.Text = "逐核占用率 · 等待连接"; return; }
        var cores = last.CpuCores.OrderBy(x => x.Index).ToArray();
        coreHeading.Text = "逻辑处理器  ·  " + cores.Length;
        coreHeading.ToolTip = "系统逻辑处理器 " + cores.Length + " 个 · 当前可用 " + last.Cores + " 个";
        if (cores.Length == 0) { coreGrid.Children.Clear(); coreGrid.Children.Add(Theme.Text("当前设备未返回逐核数据", 14, Theme.Muted)); return; }
        bool same = coreGrid.Children.Count == cores.Length && coreGrid.Children.Cast<FrameworkElement>().All(x => x.Tag is int) && coreGrid.Children.Cast<FrameworkElement>().Select(x => (int)x.Tag).SequenceEqual(cores.Select(x => x.Index));
        if (!same) coreGrid.Children.Clear();
        for (int i = 0; i < cores.Length; i++) {
            var core = cores[i]; var color = core.Util >= 85 ? Theme.Amber : Theme.Green;
            Border border; StackPanel cell;
            if (!same) {
                cell = new StackPanel();
                var label = Theme.Text("CPU " + core.Index, 12, Theme.Muted, true); label.TextAlignment = TextAlignment.Center; cell.Children.Add(label);
                var value = Theme.Text("", 18, color, true, true); value.TextAlignment = TextAlignment.Center; value.Margin = new Thickness(0, 3, 0, 8); cell.Children.Add(value);
                cell.Children.Add(new PixelMeter(core.Util, color, 4));
                border = new Border { Tag = core.Index, Width = 96, Padding = new Thickness(10), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(8), Background = Theme.Raised, Child = cell };
                coreGrid.Children.Add(border);
            } else { border = (Border)coreGrid.Children[i]; cell = (StackPanel)border.Child; }
            var number = (TextBlock)cell.Children[1]; NumericMotion.Text(number, core.Util.ToString("0") + "%"); number.Foreground = color;
            var meter = (PixelMeter)cell.Children[2]; meter.Fill = color; meter.Value = core.Util;
            border.ToolTip = "逻辑处理器 " + core.Index + " · " + core.Util.ToString("0.0") + "%";
        }
        LayoutCoreTiles();
    }

    void UpdateWorkspaceDensity()
    {
        if (deviceRail == null) return;
        double width = Root.ActualWidth;
        bool compact = width > 0 && width < 980, changed = compact != railCompact; railCompact = compact;
        Root.ColumnDefinitions[0].Width = new GridLength(compact ? 68 : PaneLayouts.Clamp(double.IsNaN(sidebarWidth) ? PaneLayouts.Get("workspace.sidebar", 240) : sidebarWidth, 200, Math.Min(340, Math.Max(200, width - 620))));
        deviceRail.Visibility = Visibility.Visible; if (railResizer != null) railResizer.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        var dock = (DockPanel)deviceRail.Child; dock.Margin = new Thickness(compact ? 8 : 12, 24, compact ? 8 : 12, 12);
        railBrandText.Visibility = railLabel.Visibility = railCaption.Visibility = railMetadata.Visibility = railArchive.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        foreach (var label in railGroupLabels) label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        foreach (var button in viewButtons) {
            var content = (Grid)button.Content; content.Children[1].Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            button.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Stretch;
            button.Padding = new Thickness(12, 0, 12, 0);
        }
        foreach (var button in new[] { railSettings, railAbout }) {
            var content = button.Content as StackPanel; if (content != null && content.Children.Count > 1) content.Children[1].Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            button.HorizontalContentAlignment = compact ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        }
        if (changed) RefreshDeviceRail();
        if (footerCpu != null) { footerLive.Visibility = Visibility.Collapsed; for (int i = 0; i < 3; i++) LayoutFooterCell(i); }
    }
}
