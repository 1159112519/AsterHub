using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

public sealed class PeripheralChart : ResourceChart
{
    public List<ResourceSample> Data = new List<ResourceSample>();
    public bool Storage;
    public int Interval = 5;
    double maximum = 1024;
    DateTime end = DateTime.UtcNow;
    long lastTick;
    public void Refresh(List<ResourceSample> data)
    {
        Data = data; end = DateTime.UtcNow;
        double peak = Data.Count == 0 ? 0 : Data.Max(x => Math.Max(x.Download.GetValueOrDefault(), x.Upload.GetValueOrDefault()));
        maximum = Storage ? 100 : Math.Pow(2, Math.Ceiling(Math.Log(Math.Max(1024, peak * 1.15), 2)));
        InvalidateVisual();
    }
    protected override Rect Plot { get { return new Rect(84, 14, Math.Max(1, ActualWidth - 98), Math.Max(1, ActualHeight - 46)); } }
    protected override long PlotFrom { get { return end.AddMinutes(-5).Ticks; } }
    protected override long PlotTo { get { return end.Ticks; } }
    protected override string AxesKey { get { return (Storage ? "storage" : "network") + maximum.ToString(CultureInfo.InvariantCulture); } }
    protected override void DrawAxes(DrawingContext dc)
    {
        var plot = Plot;
        for (int i = 0; i <= 4; i++) {
            double value = maximum * (4 - i) / 4, y = plot.Top + plot.Height * i / 4;
            dc.DrawLine(new Pen(Theme.Line, 1), new Point(plot.Left, y), new Point(plot.Right, y));
            Label(dc, Storage ? value.ToString("0") + "%" : PeripheralMetrics.Bytes(value, true), 0, y - 7, Theme.Muted);
        }
        Label(dc, "−5 分钟", plot.Left, plot.Bottom + 12, Theme.Muted); Label(dc, "现在", plot.Right - 26, plot.Bottom + 12, Theme.Muted);
    }
    void Line(DrawingContext dc, Func<ResourceSample, double?> value, Brush color)
    {
        var path = new StreamGeometry(); bool broken = true; DateTime previous = DateTime.MinValue;
        using (var context = path.Open()) foreach (var item in Data) {
            double? number = value(item); double age = (end - item.Time).TotalSeconds;
            if (!number.HasValue || double.IsNaN(number.Value) || age < 0 || age > 300) { broken = true; continue; }
            var point = new Point(Plot.Left + Plot.Width * (1 - age / 300), Plot.Bottom - Plot.Height * Math.Max(0, Math.Min(1, number.Value / maximum)));
            if (broken || (item.Time - previous).TotalSeconds > Math.Max(Interval * 3, 15)) context.BeginFigure(point, false, false); else context.LineTo(point, true, false);
            previous = item.Time; broken = false;
        }
        path.Freeze(); dc.DrawGeometry(null, new Pen(color, 2) { LineJoin = PenLineJoin.Round }, path);
    }
    protected override void DrawSeries(DrawingContext dc)
    {
        Line(dc, x => Storage ? x.Storage : x.Download, Theme.Green);
        if (!Storage) Line(dc, x => x.Upload, Theme.Teal);
        var latest = Data.LastOrDefault(x => (Storage ? x.Storage : x.Download).HasValue);
        if (latest == null) { EndMotion(); Label(dc, "等待有效采样…", Plot.Left + 18, Plot.Top + Plot.Height / 2, Theme.Muted); return; }
        double value = (Storage ? latest.Storage : latest.Download).Value;
        var point = new Point(Plot.Left + Plot.Width * (1 - (end - latest.Time).TotalSeconds / 300), Plot.Bottom - Plot.Height * Math.Max(0, Math.Min(1, value / maximum)));
        dc.DrawEllipse(Theme.Green, null, point, 3, 3);
        bool arrived = lastTick > 0 && latest.Time.Ticks > lastTick;
        long previous = lastTick; lastTick = latest.Time.Ticks;
        if (arrived) Arrival(point, Plot.Width * (lastTick - previous) / (double)TimeSpan.FromMinutes(5).Ticks);
    }
    protected override HoverReading FindHover(Point point)
    {
        if (!Plot.Contains(point) || Data.Count == 0) return null;
        long target = PlotFrom + (long)((PlotTo - PlotFrom) * (point.X - Plot.Left) / Plot.Width);
        int index = ChartHover.Nearest(Data, target, x => x.Time.Ticks); var sample = Data[index];
        double? value = Storage ? sample.Storage : sample.Download;
        bool gap = !value.HasValue || Math.Abs(target - sample.Time.Ticks) > TimeSpan.FromSeconds(Math.Max(3, Interval * 1.5)).Ticks;
        return new HoverReading { TargetUtc = new DateTime(target, DateTimeKind.Utc), SampleUtc = sample.Time, Gap = gap,
            Value = value.HasValue ? value.Value / maximum * 100 : double.NaN, Metric = Storage ? "存储" : "网络",
            Detail = Storage ? "已用 " + (sample.Storage.HasValue ? sample.Storage.Value.ToString("0.0") + "%" : "—") :
                "下载 " + PeripheralMetrics.Bytes(sample.Download, true) + "\n上传 " + PeripheralMetrics.Bytes(sample.Upload, true) };
    }
}