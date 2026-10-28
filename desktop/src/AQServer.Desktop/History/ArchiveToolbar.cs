using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

public partial class ArchivePanel
{
    ComboBox rangePicker;
    Button archiveActions;
    bool settingRange;

    void BuildRangeToolbar()
    {
        var content = new StackPanel();
        var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 18) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rangePicker = new ComboBox {
            ItemsSource = new[] { "最近 1 小时", "最近 6 小时", "最近 24 小时", "最近 7 天", "今天", "昨天", "自定义时间" },
            SelectedIndex = 0, Width = 164, MinHeight = 38, FontSize = 15,
            HorizontalAlignment = HorizontalAlignment.Left, ToolTip = "选择查询时间范围"
        };
        rangePicker.SelectionChanged += (s, e) => {
            if (settingRange || rangePicker.SelectedIndex < 0) return;
            int choice = rangePicker.SelectedIndex;
            if (choice == 6) { if (start != null) start.Focus(); return; }
            var now = DateTime.Now;
            var from = choice == 4 ? DateTime.Today : choice == 5 ? DateTime.Today.AddDays(-1) : now.AddHours(-new[] { 1, 6, 24, 168 }[choice]);
            var to = choice == 5 ? DateTime.Today : now;
            SetRange(from, to, true);
        };
        toolbar.Children.Add(rangePicker);
        query = Theme.Button("查询", QueryNow);
        query.MinWidth = 76;
        query.Background = query.BorderBrush = Theme.Accent;
        query.Foreground = Theme.Name == "light" ? Brushes.White : Theme.B("#171C24");
        query.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(query, 1);
        toolbar.Children.Add(query);
        archiveActions = Theme.Button("存档与导出", ShowArchiveActions);
        archiveActions.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(archiveActions, 2);
        toolbar.Children.Add(archiveActions);
        csv = ArchiveAction("导出 CSV 表格", () => Export(false));
        backup = ArchiveAction("导出历史存档…", () => Export(true));
        csv.IsEnabled = backup.IsEnabled = false;
        var menu = new ContextMenu { Placement = PlacementMode.Bottom, PlacementTarget = archiveActions };
        menu.Items.Add(csv);
        menu.Items.Add(backup);
        menu.Items.Add(new Separator());
        menu.Items.Add(ArchiveAction("打开历史存档…", OpenArchive));
        menu.Items.Add(ArchiveAction("打开存档文件夹", OpenFolder));
        menu.Items.Add(new Separator());
        menu.Items.Add(ArchiveAction("返回当前设备记录", () => {
            external = null;
            settingRange = true;
            rangePicker.SelectedIndex = 0;
            settingRange = false;
            var now = DateTime.Now;
            SetRange(now.AddHours(-1), now, true);
        }));
        archiveActions.ContextMenu = menu;
        content.Children.Add(toolbar);

        var dates = new Grid();
        dates.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        dates.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
        dates.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        start = DateField(dates, 0, "开始时间");
        end = DateField(dates, 2, "结束时间");
        var separator = Theme.Text("—", 14, Theme.Muted);
        separator.HorizontalAlignment = HorizontalAlignment.Center;
        separator.Margin = new Thickness(0, 22, 0, 0);
        Grid.SetColumn(separator, 1);
        dates.Children.Add(separator);
        content.Children.Add(dates);
        var metadata = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = true };
        var zone = Theme.Text("本机时间", 12, Theme.Muted);
        zone.ToolTip = TimeZoneInfo.Local.DisplayName;
        zone.Margin = new Thickness(20, 0, 0, 0);
        DockPanel.SetDock(zone, Dock.Right);
        metadata.Children.Add(zone);
        storage = Theme.Text("", 12, Theme.Muted);
        metadata.Children.Add(storage);
        content.Children.Add(metadata);
        scope = Theme.Text("", 13, Theme.Teal);
        scope.Visibility = Visibility.Collapsed;
        scope.TextWrapping = TextWrapping.Wrap;
        scope.Margin = new Thickness(0, 12, 0, 0);
        content.Children.Add(scope);
        status = Theme.Text("选择时间范围，查询本机保存的历史记录。", 13, Theme.Muted);
        status.TextWrapping = TextWrapping.Wrap;
        status.Margin = new Thickness(0, 8, 0, 0);
        content.Children.Add(status);
        Children.Add(Card(content));
    }

    static MenuItem ArchiveAction(string title, Action action)
    {
        var item = new MenuItem { Header = title, FontSize = 15, MinHeight = 38 };
        item.Click += (s, e) => action();
        return item;
    }

    void ShowArchiveActions()
    {
        var menu = archiveActions.ContextMenu;
        menu.Resources = Theme.Resources();
        menu.PlacementTarget = archiveActions;
        menu.IsOpen = true;
    }

    TextBox DateField(Grid parent, int column, string label)
    {
        var group = new StackPanel();
        group.Children.Add(Theme.Text(label, 13, Theme.Muted));
        var field = new TextBox {
            FontFamily = Theme.Mono, FontSize = 16, MinHeight = 42,
            Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 7, 0, 0),
            ToolTip = "本机时间 · yyyy-MM-dd HH:mm:ss"
        };
        System.Windows.Documents.Typography.SetNumeralAlignment(field, FontNumeralAlignment.Tabular);
        field.TextChanged += (s, e) => {
            if (settingRange || rangePicker == null) return;
            settingRange = true;
            rangePicker.SelectedIndex = 6;
            settingRange = false;
        };
        field.KeyDown += (s, e) => { if (e.Key == Key.Enter) { QueryNow(); e.Handled = true; } };
        group.Children.Add(field);
        Grid.SetColumn(group, column);
        parent.Children.Add(group);
        return field;
    }
}
