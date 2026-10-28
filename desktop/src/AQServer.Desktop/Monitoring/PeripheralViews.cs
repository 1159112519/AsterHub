using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

public sealed partial class MonitorWindow
{
    PeripheralChart peripheralChart;
    TextBlock peripheralPrimary, peripheralSecondary, peripheralCaption;
    StackPanel peripheralRows;
    string peripheralRowsKey;
    readonly List<TextBlock> peripheralRowValues = new List<TextBlock>();

    void BuildPeripheralView()
    {
        bool network = View == "网络";
        var content = new StackPanel();
        var head = new Grid { Margin = new Thickness(0, 0, 0, 24) };
        head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition());
        var primary = new StackPanel(); primary.Children.Add(Theme.Text(network ? "下载" : "系统分区已用", 14, Theme.Muted));
        peripheralPrimary = Theme.Text("—", 36, Theme.Green, true, true); peripheralPrimary.Margin = new Thickness(0, 8, 0, 0); primary.Children.Add(peripheralPrimary); head.Children.Add(primary);
        var secondary = new StackPanel(); secondary.Children.Add(Theme.Text(network ? "上传" : "已用 / 总容量", 14, Theme.Muted));
        peripheralSecondary = Theme.Text("—", network ? 36 : 22, Theme.Teal, true, true); peripheralSecondary.Margin = new Thickness(0, 8, 0, 0); secondary.Children.Add(peripheralSecondary); Grid.SetColumn(secondary, 1); head.Children.Add(secondary);
        content.Children.Add(head);
        peripheralChart = new PeripheralChart { Storage = !network, Interval = config.Interval, Height = 280 };
        content.Children.Add(peripheralChart); peripheralCaption = Theme.Text("", 13, Theme.Muted); peripheralCaption.Margin = new Thickness(0, 16, 0, 0); content.Children.Add(peripheralCaption);
        Body.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(24), Child = content });
        peripheralRows = new StackPanel(); peripheralRows.Children.Add(Theme.Text(network ? "活动网络接口" : "分区与卷", 16, Theme.Ink, false, true));
        Body.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(24), Margin = new Thickness(0, 20, 0, 0), Child = peripheralRows });
        peripheralRowsKey = null; peripheralRowValues.Clear(); UpdatePeripheralView();
    }
    void UpdatePeripheralView()
    {
        if (peripheralChart == null || (View != "网络" && View != "存储")) return;
        bool network = View == "网络"; var metrics = lastGood ?? last; var net = metrics == null ? null : metrics.Network; var volume = PeripheralMetrics.PrimaryVolume(metrics);
        NumericMotion.Text(peripheralPrimary, network ? PeripheralMetrics.Bytes(net == null ? null : net.DownloadBytesPerSecond, true) : volume == null ? "—" : volume.UsedPercent.ToString("0.0") + "%");
        NumericMotion.Text(peripheralSecondary, network ? PeripheralMetrics.Bytes(net == null ? null : net.UploadBytesPerSecond, true) : volume == null ? "—" : PeripheralMetrics.Bytes(volume.UsedBytes) + " / " + PeripheralMetrics.Bytes(volume.TotalBytes));
        peripheralCaption.Text = network ? "最近 5 分钟 · 下载 / 上传" : volume == null ? "等待容量信息" : volume.Path + " · " + volume.FileSystem + " · 可用 " + PeripheralMetrics.Bytes(volume.AvailableBytes);
        peripheralChart.Refresh(History);
        string[] labels = network ? (net == null ? new string[0] : net.Interfaces.Select(x => x.Name).ToArray()) : metrics == null ? new string[0] : metrics.Volumes.Select(x => x.Path + " · " + x.FileSystem).ToArray();
        string key = string.Join("|", labels);
        if (peripheralRowsKey != key) {
            peripheralRowsKey = key; while (peripheralRows.Children.Count > 1) peripheralRows.Children.RemoveAt(1); peripheralRowValues.Clear();
            if (labels.Length == 0) { var empty = Theme.Text(network ? "暂未读取到活动网络接口" : "暂未读取到存储分区", 14, Theme.Muted); empty.Margin = new Thickness(0, 18, 0, 0); peripheralRows.Children.Add(empty); }
            foreach (string label in labels) {
                var row = new Grid { MinHeight = 54, Margin = new Thickness(0, 12, 0, 0) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                var name = Theme.Text(label, 14, Theme.Ink); name.ToolTip = label; row.Children.Add(name);
                var value = Theme.Text("—", 14, Theme.Muted); value.TextAlignment = TextAlignment.Right; value.TextWrapping = TextWrapping.Wrap; Grid.SetColumn(value, 1); row.Children.Add(value); peripheralRowValues.Add(value); peripheralRows.Children.Add(row);
            }
        }
        for (int i = 0; i < peripheralRowValues.Count; i++) {
            if (network) { var adapter = net.Interfaces[i]; peripheralRowValues[i].Text = "↓ " + PeripheralMetrics.Bytes(adapter.DownloadBytesPerSecond, true) + "   ↑ " + PeripheralMetrics.Bytes(adapter.UploadBytesPerSecond, true); }
            else { var disk = metrics.Volumes[i]; peripheralRowValues[i].Text = PeripheralMetrics.Bytes(disk.UsedBytes) + " / " + PeripheralMetrics.Bytes(disk.TotalBytes) + " · " + disk.UsedPercent.ToString("0.0") + "%\n可用 " + PeripheralMetrics.Bytes(disk.AvailableBytes); }
        }
    }
}