using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public sealed partial class SettingsWindow
{
    StackPanel connectionPage, appearancePage, terminalPage, recordingPage;
    ScrollViewer preferenceScroll;
    readonly List<Button> preferenceTabs = new List<Button>();

    StackPanel BuildSettingsPages(Grid main)
    {
        connectionPage = new StackPanel(); appearancePage = new StackPanel(); terminalPage = new StackPanel(); recordingPage = new StackPanel();
        var right = new DockPanel(); Grid.SetColumn(right, 2); main.Children.Add(right);
        var tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4) };
        string[] titles = { "连接", "外观", "终端", "记录与启动" };
        for (int i = 0; i < titles.Length; i++) {
            int selected = i;
            var button = Theme.Nav(titles[i], () => SelectSettingsPage(selected));
            button.Height = button.MinHeight = 36; button.FontSize = 14; button.MinWidth = 64;
            button.Margin = new Thickness(0, 0, i == 3 ? 0 : 2, 0); button.Padding = new Thickness(12, 0, 12, 0);
            tabs.Children.Add(button); preferenceTabs.Add(button);
        }
        var tabSurface = new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(11), Child = tabs, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
        DockPanel.SetDock(tabSurface, Dock.Top); right.Children.Add(tabSurface);
        preferenceScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        right.Children.Add(new Border { CornerRadius = new CornerRadius(12), Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), Padding = new Thickness(20), Child = preferenceScroll });
        SelectSettingsPage(0); return connectionPage;
    }

    internal void SelectSettingsPage(int index)
    {
        for (int i = 0; i < preferenceTabs.Count; i++) {
            preferenceTabs[i].Background = i == index ? Theme.AccentSoft : Brushes.Transparent;
            preferenceTabs[i].Foreground = i == index ? Theme.Ink : Theme.Muted;
            preferenceTabs[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
        }
        preferenceScroll.Content = new[] { connectionPage, appearancePage, terminalPage, recordingPage }[index];
        preferenceScroll.ScrollToTop(); if (test != null) test.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
