using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public sealed partial class MonitorWindow
{
    readonly Grid[] footerCells = new Grid[5];
    readonly Border[] footerGroups = new Border[5];
    readonly TextBlock[] footerNames = new TextBlock[5], footerReadings = new TextBlock[5];
    readonly FrameworkElement[] footerValueHosts = new FrameworkElement[5], footerPlots = new FrameworkElement[5];
    Grid footerGrid;
    bool? footerTwoRows;
    TextBlock NetworkValue, StorageValue, networkDownload, storageCapacity;
    MiniSpark footerNetwork;
    PixelMeter footerStorage;

    void BuildFooter()
    {
        workspaceFooter = new Border { Height = 76, Background = Theme.Surface, BorderBrush = Theme.Line,
            BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(8, 10, 8, 10), Margin = new Thickness(0, 16, 0, 0) };
        footerGrid = new Grid { MaxWidth = 1480 };
        string[] names = { "CPU", "GPU", "内存", "网络", "存储" };
        for (int i = 0; i < names.Length; i++) FooterCell(i, names[i]);
        CpuValue = footerReadings[0]; DiskValue = footerReadings[1]; MemoryValue = footerReadings[2]; NetworkValue = footerReadings[3]; StorageValue = footerReadings[4];
        footerLive = Theme.Text("", 12, Theme.Green); captureStatus = Theme.Text("", 12, Theme.Muted);
        cpuDetail = Theme.Text("", 12, Theme.Muted); gpuDetail = Theme.Text("", 12, Theme.Muted); memoryDetail = Theme.Text("", 12, Theme.Muted);
        foreach (var hidden in new[] { footerLive, captureStatus, cpuDetail, gpuDetail, memoryDetail }) { hidden.Visibility = Visibility.Collapsed; footerGrid.Children.Add(hidden); }
        footerGrid.SizeChanged += (s, e) => LayoutFooter();
        workspaceFooter.Child = footerGrid; Grid.SetRow(workspaceFooter, 3); Grid.SetColumn(workspaceFooter, 1); Root.Children.Add(workspaceFooter);
        LayoutFooter();
    }

    void FooterCell(int index, string title)
    {
        var cell = new Grid { Height = 36, VerticalAlignment = VerticalAlignment.Center, Background = Brushes.Transparent };
        footerCells[index] = cell;
        foreach (double width in new[] { -1.0, 36, 6, 92, 8, 48, -1 })
            cell.ColumnDefinitions.Add(new ColumnDefinition { Width = width < 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
        var label = Theme.Text(title, 16, Theme.Muted); label.TextAlignment = TextAlignment.Center; footerNames[index] = label;
        Grid.SetColumn(label, 1); cell.Children.Add(label);
        var value = Theme.Text("—", 16, Theme.Ink, false, true); value.TextAlignment = TextAlignment.Center;
        System.Windows.Documents.Typography.SetNumeralAlignment(value, FontNumeralAlignment.Tabular); footerReadings[index] = value;
        FrameworkElement host = value;
        if (index >= 3) {
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center }; stack.Children.Add(value);
            var secondary = Theme.Text("—", 12, Theme.Muted); secondary.TextAlignment = TextAlignment.Center; stack.Children.Add(secondary); host = stack;
            if (index == 3) networkDownload = secondary; else storageCapacity = secondary;
        }
        footerValueHosts[index] = host; Grid.SetColumn(host, 3); cell.Children.Add(host);
        FrameworkElement plot;
        if (index == 0) { footerCpu = new MiniSpark(); plot = footerCpu; }
        else if (index == 1) { footerGpu = new GpuMini(); plot = footerGpu; }
        else if (index == 2) { footerMemory = new PixelMeter(0, Theme.Green, 8) { Blocks = 18 }; plot = footerMemory; }
        else if (index == 3) { footerNetwork = new MiniSpark { Network = true }; plot = footerNetwork; }
        else { footerStorage = new PixelMeter(0, Theme.Teal, 8) { Blocks = 18 }; plot = footerStorage; }
        plot.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(plot, 5); cell.Children.Add(plot); footerPlots[index] = plot;
        var group = new Border { Height = 52, CornerRadius = new CornerRadius(10), Background = Theme.Bg, BorderBrush = Theme.Line,
            BorderThickness = new Thickness(1), Padding = new Thickness(8, 0, 8, 0), Margin = new Thickness(6, 0, 6, 0), Child = cell,
            VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "查看" + title };
        group.MouseLeftButtonUp += (s, e) => SwitchView(new[] { "CPU", "GPU", "内存", "网络", "存储" }[index]);
        footerGroups[index] = group; footerGrid.Children.Add(group); cell.SizeChanged += (s, e) => LayoutFooterCell(index);
    }

    void LayoutFooter()
    {
        bool twoRows = footerGrid.ActualWidth > 0 && footerGrid.ActualWidth < 1040;
        if (footerTwoRows != twoRows) {
            footerTwoRows = twoRows; footerGrid.ColumnDefinitions.Clear(); footerGrid.RowDefinitions.Clear();
            for (int i = 0; i < (twoRows ? 6 : 5); i++) footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });
            if (twoRows) { footerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) }); footerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) }); }
            workspaceFooter.Height = twoRows ? 134 : 76;
            for (int i = 0; i < 5; i++) {
                Grid.SetRow(footerGroups[i], twoRows && i >= 3 ? 2 : 0);
                Grid.SetColumn(footerGroups[i], twoRows ? (i < 3 ? i * 2 : (i - 3) * 3) : i);
                Grid.SetColumnSpan(footerGroups[i], twoRows ? (i < 3 ? 2 : 3) : 1);
            }
        }
        for (int i = 0; i < 5; i++) LayoutFooterCell(i);
    }

    void LayoutFooterCell(int index)
    {
        var cell = footerCells[index]; if (cell == null) return;
        bool compact = cell.ActualWidth < 260;
        double plot = compact ? (index >= 3 ? 28 : index == 2 ? 36 : 48) : (index >= 3 ? 56 : 72);
        cell.ColumnDefinitions[1].Width = new GridLength(compact ? (index >= 2 ? 32 : 36) : 42);
        cell.ColumnDefinitions[2].Width = new GridLength(compact ? 6 : 8);
        cell.ColumnDefinitions[3].Width = new GridLength(index >= 3 ? (compact ? 96 : 118) : index == 2 ? (compact ? 92 : 112) : (compact ? 66 : 80));
        cell.ColumnDefinitions[4].Width = new GridLength(compact ? 8 : 10);
        cell.ColumnDefinitions[5].Width = new GridLength(plot);
        footerNames[index].FontSize = compact ? 16 : 18;
        footerReadings[index].FontSize = index == 3 ? (compact ? 13 : 15) : compact ? 16 : 18;
        footerPlots[index].Width = plot;
        var meter = footerPlots[index] as PixelMeter;
        int blocks = compact ? (index >= 3 ? 6 : 8) : 12;
        if (meter != null && meter.Blocks != blocks) { meter.Blocks = blocks; meter.InvalidateVisual(); }
    }

    void UpdatePeripheralFooter(Metrics metrics)
    {
        var net = metrics == null ? null : metrics.Network;
        NumericMotion.Text(NetworkValue, "↑ " + PeripheralMetrics.CompactRate(net == null ? null : net.UploadBytesPerSecond));
        NumericMotion.Text(networkDownload, "↓ " + PeripheralMetrics.CompactRate(net == null ? null : net.DownloadBytesPerSecond));
        footerNetwork.Data = History; footerNetwork.Refresh();
        footerGroups[3].ToolTip = net == null ? "网络数据尚不可用" : !net.DownloadBytesPerSecond.HasValue ? "等待下一次网络计数采样" :
            "上传 " + PeripheralMetrics.Bytes(net.UploadBytesPerSecond, true) + "\n下载 " + PeripheralMetrics.Bytes(net.DownloadBytesPerSecond, true) + "\n" + string.Join("、", net.Interfaces.Select(x => x.Name));
        var volume = PeripheralMetrics.PrimaryVolume(metrics);
        NumericMotion.Text(StorageValue, volume == null ? "—" : volume.UsedPercent.ToString("0.0") + "%");
        storageCapacity.Text = volume == null ? "—" : PeripheralMetrics.Capacity(volume.UsedBytes, volume.TotalBytes);
        footerStorage.Value = volume == null ? 0 : volume.UsedPercent;
        footerGroups[4].ToolTip = volume == null ? "存储容量尚不可用" : volume.Path + " · " + volume.FileSystem + "\n已用 " + PeripheralMetrics.Bytes(volume.UsedBytes) + " / " + PeripheralMetrics.Bytes(volume.TotalBytes) + "\n可用 " + PeripheralMetrics.Bytes(volume.AvailableBytes);
    }
}