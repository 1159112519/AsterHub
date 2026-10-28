using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

public static partial class Theme
{
    public static readonly SolidColorBrush Accent = B("#DDC3B3"), AccentSoft = B("#554947"),
        Sidebar = B("#383132"), TerminalBg = B("#272224");
    public static SolidColorBrush TerminalBackground { get { return TerminalBg; } }

    public static Button Action(string title, Action action)
    {
        var button = Button(title, action);
        button.SetResourceReference(FrameworkElement.StyleProperty, "QuietButton");
        button.Background = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Foreground = Muted;
        button.Padding = new Thickness(12, 0, 12, 0);
        return button;
    }

    public static Button IconAction(string symbol, string label, Action action)
    {
        var button = Action("", action);
        var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(new IconView(symbol, 18));
        if (!string.IsNullOrEmpty(label)) {
            var text = Text(label, 14, Muted); text.Margin = new Thickness(8, 0, 0, 0); content.Children.Add(text);
        }
        button.Content = content; button.ToolTip = label;
        button.Height = button.MinHeight = 40;
        button.Padding = new Thickness(10, 0, 10, 0);
        return button;
    }

    public static Button Nav(string title, Action action)
    {
        var button = Action(title, action);
        button.Height = button.MinHeight = 38;
        button.Padding = new Thickness(16, 0, 16, 0);
        return button;
    }
}

public sealed partial class MonitorWindow
{
    PaneEdgeHandle railResizer;
    double sidebarWidth = double.NaN;
    Border deviceRail, pageIconTile;
    StackPanel railDevices, railNavigation;
    TextBlock railStatus, shellHeading, shellAddress, railSystem, railCpu, railGpu;
    TextBlock cpuDetail, gpuDetail, memoryDetail, footerLive;
    MiniSpark footerCpu;
    GpuMini footerGpu;
    PixelMeter footerMemory;
    bool railCompact;
    StackPanel railBrandText, railMetadata;
    FrameworkElement railLabel, railCaption, railArchive;
    Button railSettings, railAbout;
    IconView pageIcon;
    readonly List<TextBlock> railGroupLabels = new List<TextBlock>();
    readonly Dictionary<string, Border> railDots = new Dictionary<string, Border>();
    Metrics lastGood, lastStatus;
    DateTime? lastGoodUtc;
    string healthKey;
    DateTime nextHealth = DateTime.MinValue;
    bool healthBusy;
    ConnectionResult healthResult;

    void BuildRailResizer()
    {
        railResizer = new PaneEdgeHandle("workspace.sidebar", 240, 200, 340,
            () => Root.ColumnDefinitions[0].ActualWidth,
            width => { sidebarWidth = width; Root.ColumnDefinitions[0].Width = new GridLength(width); },
            () => Root.ActualWidth - 620);
        Grid.SetRowSpan(railResizer, 4); Panel.SetZIndex(railResizer, 20); Root.Children.Add(railResizer);
    }

    void BuildDeviceRail()
    {
        var dock = new DockPanel { Margin = new Thickness(12, 24, 12, 12) };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 0, 24) };
        brand.Children.Add(new Image { Source = Icon, Width = 36, Height = 36, Margin = new Thickness(0, 0, 12, 0) });
        railBrandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        railBrandText.Children.Add(Theme.Text("AQ Server", 19, Theme.Ink, false, true));
        var tagline = Theme.Text("设备工作台", 12, Theme.Muted); tagline.Margin = new Thickness(0, 3, 0, 0);
        railBrandText.Children.Add(tagline); brand.Children.Add(railBrandText);
        DockPanel.SetDock(brand, Dock.Top); dock.Children.Add(brand);

        var bottom = new StackPanel();
        railArchive = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 1, 0, 0), Margin = new Thickness(6, 12, 6, 8), Padding = new Thickness(4, 14, 4, 8) };
        var archive = new StackPanel { Orientation = Orientation.Horizontal };
        archive.Children.Add(new IconView("Database", 16, Theme.Green));
        railStatus = Theme.Text("历史记录已开启", 12, Theme.Muted); railStatus.Margin = new Thickness(9, 0, 0, 0);
        archive.Children.Add(railStatus); ((Border)railArchive).Child = archive; bottom.Children.Add(railArchive);
        railSettings = RailUtility("Settings2", "偏好设置", OpenSettings);
        railAbout = RailUtility("Info", "关于与更新", () => new AboutWindow(this, Exit).ShowDialog());
        bottom.Children.Add(railSettings); bottom.Children.Add(railAbout);
        DockPanel.SetDock(bottom, Dock.Bottom); dock.Children.Add(bottom);

        var main = new StackPanel();
        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = main };
        var label = new Grid { Height = 32, Margin = new Thickness(12, 0, 4, 4) };
        label.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        label.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        label.Children.Add(Theme.Text("设备", 12, Theme.Muted));
        var add = Theme.IconAction("Plus", "", OpenSettings); add.Width = 28; add.Height = add.MinHeight = 28; add.Padding = new Thickness(5); add.ToolTip = "添加或管理设备";
        Grid.SetColumn(add, 1); label.Children.Add(add); main.Children.Add(label); railLabel = label;
        railDevices = new StackPanel(); main.Children.Add(railDevices);
        railNavigation = new StackPanel { Margin = new Thickness(0, 8, 0, 0) }; main.Children.Add(railNavigation);
        railCaption = new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(12, 18, 12, 18) }; main.Children.Add(railCaption);
        railMetadata = new StackPanel { Margin = new Thickness(12, 0, 12, 12) };
        var metadataHeading = Theme.Text("当前设备", 12, Theme.Muted); metadataHeading.Margin = new Thickness(0, 0, 0, 8); railMetadata.Children.Add(metadataHeading);
        railSystem = SidebarValue(railMetadata, "系统"); railCpu = SidebarValue(railMetadata, "处理器"); railGpu = SidebarValue(railMetadata, "显卡");
        main.Children.Add(railMetadata); dock.Children.Add(scroller);
        deviceRail = new Border { Background = Theme.Sidebar, BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 1, 0), Child = dock };
        Grid.SetRowSpan(deviceRail, 4); Root.Children.Add(deviceRail);
    }

    Button RailUtility(string icon, string title, Action action)
    {
        var button = Theme.IconAction(icon, title, action);
        button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 2, 0, 2); button.Padding = new Thickness(12, 0, 12, 0);
        button.ToolTip = title; return button;
    }

    TextBlock SidebarValue(StackPanel owner, string label)
    {
        var row = new Grid { MinHeight = 30 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.Children.Add(Theme.Text(label, 12, Theme.Muted));
        var value = Theme.Text("读取中", 12, Theme.Ink); value.TextAlignment = TextAlignment.Right; value.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(value, 1); row.Children.Add(value); owner.Children.Add(row); return value;
    }

    void RefreshDeviceRail()
    {
        if (railDevices == null) return;
        railDevices.Children.Clear(); railDots.Clear();
        foreach (var item in config.Servers) {
            var server = item;
            bool selected = active != null && ArchiveStore.Key(active) == ArchiveStore.Key(server);
            var content = new Grid { Margin = new Thickness(12, 7, 12, 7) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            content.Children.Add(new IconView(server.IsLocal ? "Laptop" : "Server", 20, selected ? Theme.Accent : Theme.Muted));
            var text = new StackPanel { Margin = new Thickness(12, 0, 8, 0), Visibility = railCompact ? Visibility.Collapsed : Visibility.Visible };
            text.Children.Add(Theme.Text(server.Name, 14, Theme.Ink, false, selected));
            var sub = Theme.Text(server.IsLocal ? "本机" : server.Host, 12, Theme.Muted); sub.Margin = new Thickness(0, 2, 0, 0); text.Children.Add(sub);
            Grid.SetColumn(text, 1); content.Children.Add(text);
            var dot = new Border { Width = 5, Height = 5, CornerRadius = new CornerRadius(3), Background = Theme.Muted, VerticalAlignment = VerticalAlignment.Center, Visibility = railCompact ? Visibility.Collapsed : Visibility.Visible };
            railDots[ArchiveStore.Key(server)] = dot; Grid.SetColumn(dot, 2); content.Children.Add(dot);
            var button = Theme.Action("", () => ChooseServer(server)); button.Content = content;
            button.HorizontalContentAlignment = HorizontalAlignment.Stretch; button.Padding = new Thickness(0); button.MinHeight = railCompact ? 44 : 54;
            button.Margin = new Thickness(0, 0, 0, 4); button.Background = selected ? Theme.AccentSoft : Brushes.Transparent;
            button.ToolTip = server.IsLocal ? "本机设备" : server.Name + " · " + server.User + "@" + server.Host + ":" + server.Port;
            railDevices.Children.Add(button);
        }
        if (shellHeading != null) shellHeading.Text = active == null ? "AQ Server" : active.Name;
        UpdateShellReading(lastStatus ?? last);
    }

void UpdateShellReading(Metrics reading){foreach(var device in config.Servers){Border dot;if(!railDots.TryGetValue(ArchiveStore.Key(device),out dot))continue;var sample=capture==null?null:capture.Latest(device);var metric=sample==null?(active!=null&&ArchiveStore.Key(active)==ArchiveStore.Key(device)?reading:null):sample.Metrics;dot.Background=metric==null?Theme.Muted:string.IsNullOrEmpty(metric.Error)?Theme.Green:Theme.Amber;}UpdatePeripheralFooter(lastGood);if(active==null)return;bool error=reading!=null&&!string.IsNullOrEmpty(reading.Error);if(shellAddress!=null){shellAddress.Text=error?(lastGood==null?"连接中":"重连中"):active.IsLocal?"● 本机":"● SSH 已连接";shellAddress.Foreground=error?Theme.Amber:Theme.Green;}var info=lastGood==null?null:lastGood.Hardware;if(railSystem!=null){railSystem.Text=info!=null?info.OperatingSystem:"读取中";railSystem.ToolTip=railSystem.Text;railCpu.Text=lastGood==null?"读取中":lastGood.Cores+" 逻辑处理器";railCpu.ToolTip=info==null?null:info.CpuModel;railGpu.Text=lastGood==null?"读取中":lastGood.Gpus.Count==0?"未检测到":lastGood.Gpus.Select(x=>x.Name).Distinct().Count()==1?lastGood.Gpus.Count+" × "+ShortGpu(lastGood.Gpus[0].Name):lastGood.Gpus.Count+" 张显卡";railGpu.ToolTip=lastGood==null?null:string.Join("\n",lastGood.Gpus.Select(x=>"GPU "+x.Index+" · "+x.Name));}if(footerLive!=null){footerLive.Text=error?"● 重连中":"● 持续存档";footerLive.Foreground=error?Theme.Amber:Theme.Green;}if(footerCpu!=null){footerCpu.Data=History;footerCpu.Refresh();}if(lastGood==null){cpuDetail.Text="—";gpuDetail.Text="—";footerGpu.Set(new List<Gpu>());footerMemory.Value=0;NumericMotion.Text(MemoryValue,"—");memoryDetail.Text="";}if(lastGood!=null){cpuDetail.Text=lastGood.Cores+" 线程";gpuDetail.Text=lastGood.Gpus.Count==0?"无显卡":lastGood.Gpus.Count+" 张显卡";DiskValue.ToolTip=lastGood.Gpus.Count==0?"未检测到 NVIDIA 显卡":"GPU 平均利用率";footerGpu.Set(lastGood.Gpus);footerMemory.Value=ResourceSample.MemoryPercent(lastGood.Mem);var values=System.Text.RegularExpressions.Regex.Matches(lastGood.Mem??"",@"[0-9.]+");if(values.Count>=2){NumericMotion.Text(MemoryValue,(GpuCard.Number(values[0].Value)/1024).ToString("0.0")+" GiB");memoryDetail.Text=" / "+(GpuCard.Number(values[1].Value)/1024).ToString("0.0")+" GiB";}}}

static string ShortGpu(string name){return (name??"").Replace("NVIDIA GeForce ","").Replace("NVIDIA ","");}

string HealthKey(Server s){return ArchiveStore.Key(s)+"|"+s.KeyPath;}

string ConnectionLabel(){if(active!=null&&active.IsLocal)return "本机";return healthResult!=null&&healthResult.Success&&active!=null&&healthKey==HealthKey(active)?healthResult.Milliseconds+" ms":"— ms";}

internal void RequestConnectionHealth(bool force=false){if((testMode&&!force)||active==null||healthBusy||exitAllowed)return;var device=active.Copy();string key=HealthKey(device);if(healthKey==key&&DateTime.UtcNow<nextHealth)return;healthBusy=true;Task.Run(()=>ConnectionProbe.Run(device)).ContinueWith(job=>{if(Dispatcher.HasShutdownStarted)return;Dispatcher.BeginInvoke((System.Action)(()=>{healthBusy=false;if(exitAllowed||active==null||HealthKey(active)!=key)return;healthKey=key;healthResult=job.IsFaulted?new ConnectionResult{Success=false,Message="连接检测失败"}:job.Result;nextHealth=DateTime.UtcNow.AddSeconds(30);if(healthResult.Success&&lastGood!=null&&Connection.Text!="重连中"){Connection.Text=ConnectionLabel();Connection.Foreground=Theme.Green;}Connection.ToolTip=healthResult.Details;}));});}
}
