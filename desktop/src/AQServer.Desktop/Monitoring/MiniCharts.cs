using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls;

public sealed class MiniSpark : FrameworkElement, IFrameMotion
{
    public IList<ResourceSample> Data;
    public bool Network;
    List<double> from = new List<double>(), target = new List<double>();
    List<ResourceSample> samples = new List<ResourceSample>();
    double started, blend = 1;
    readonly ToolTip tip = new ToolTip();
    int hover = -1;
    readonly bool memory;

    public MiniSpark(bool useMemory = false)
    {
        Width = 92; Height = 28;
        VerticalAlignment = VerticalAlignment.Center;
        memory = useMemory;
        MouseMove += (s, e) => {
            if (samples.Count == 0) return;
            int index = Math.Max(0, Math.Min(samples.Count - 1,
                (int)Math.Round(e.GetPosition(this).X / Math.Max(1, ActualWidth) * (samples.Count - 1))));
            if (index == hover) return;
            hover = index;
            tip.Content = samples[index].Time.ToLocalTime().ToString("HH:mm:ss") + "  " +
                (Network ? "↑ " + PeripheralMetrics.Bytes(samples[index].Upload,true) + "  ↓ " + PeripheralMetrics.Bytes(samples[index].Download,true) : (memory ? samples[index].Memory : samples[index].Cpu).ToString("0.0") + "%");
            tip.PlacementTarget = this;
            tip.IsOpen = true;
        };
        MouseLeave += (s, e) => { tip.IsOpen = false; hover = -1; };
        Unloaded += (s, e) => { MotionClock.Remove(this); tip.IsOpen = false; };
    }

    public void Refresh()
    {
        var next = (Data ?? new List<ResourceSample>())
            .Where(x => Network ? x.Download.HasValue && x.Upload.HasValue : !double.IsNaN(memory ? x.Memory : x.Cpu)).TakeLastCompat(32).ToList();
        if (samples.Count == next.Count && samples.Count > 0 && samples.Last().Time == next.Last().Time) return;
        samples = next;
        hover = -1;
        from = target.ToList();
        double peak = Network && next.Count > 0 ? Math.Max(1024,next.Max(x => x.Download.GetValueOrDefault()+x.Upload.GetValueOrDefault())) : 100;
        target = next.Select(x => Math.Max(0, Math.Min(100, Network ? 100*(x.Download.GetValueOrDefault()+x.Upload.GetValueOrDefault())/peak : memory ? x.Memory : x.Cpu))).ToList();
        if (from.Count != target.Count) from = target.ToList();
        blend = IsLoaded && IsVisible && Motion.Enabled ? 0 : 1;
        started = MotionClock.Now;
        if (blend < 1) MotionClock.Add(this);
        InvalidateVisual();
    }

    public bool Frame(double now)
    {
        blend = Motion.Ease(Math.Min(1, (now - started) / .48));
        InvalidateVisual();
        return blend < 1 && IsVisible && IsLoaded;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double bottom = Math.Max(2, ActualHeight - 2), height = Math.Max(1, ActualHeight - 4);
        var guide = Theme.Line.CloneCurrentValue(); guide.Opacity = .65;
        dc.DrawLine(new Pen(guide, 1), new Point(0, 2), new Point(ActualWidth, 2));
        dc.DrawLine(new Pen(guide, 1), new Point(0, bottom), new Point(ActualWidth, bottom));
        if (target.Count < 2) return;
        var geometry = new StreamGeometry();
        Point last = new Point();
        using (var context = geometry.Open()) {
            for (int i = 0; i < target.Count; i++) {
                double value = from[i] + (target[i] - from[i]) * blend;
                last = new Point(ActualWidth * i / (target.Count - 1), bottom - value / 100 * height);
                if (i == 0) context.BeginFigure(last, false, false);
                else context.LineTo(last, true, false);
            }
        }
        dc.DrawGeometry(null, new Pen(Theme.Accent, 1.6) { LineJoin = PenLineJoin.Round }, geometry);
        dc.DrawEllipse(Theme.Accent, null, last, 2, 2);
    }
}

public static class FooterSequence
{
    public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> items, int count)
    {
        var list = items.ToList();
        return list.Skip(Math.Max(0, list.Count - count));
    }
}

public sealed class GpuMini : FrameworkElement, IFrameMotion
{
    double[] start = new double[0], target = new double[0];
    string[] labels = new string[0];
    double began, blend = 1;
    readonly ToolTip tip = new ToolTip();
    int hover = -1;

    public GpuMini()
    {
        Width = 92; Height = 28;
        VerticalAlignment = VerticalAlignment.Center;
        MouseMove += (s, e) => {
            if (target.Length == 0) return;
            int index = Math.Max(0, Math.Min(target.Length - 1,
                (int)(e.GetPosition(this).X / Math.Max(1, ActualWidth) * target.Length)));
            if (hover == index) return;
            hover = index;
            tip.Content = labels[index] + "  " + target[index].ToString("0.0") + "%";
            tip.PlacementTarget = this;
            tip.IsOpen = true;
        };
        MouseLeave += (s, e) => { hover = -1; tip.IsOpen = false; };
        Unloaded += (s, e) => { MotionClock.Remove(this); tip.IsOpen = false; };
    }

    public void Set(IEnumerable<Gpu> gpus)
    {
        var list = gpus.ToList();
        var next = list.Select(x => Math.Max(0, Math.Min(100, GpuCard.Number(x.Util)))).ToArray();
        start = target.Length == next.Length ? target : next;
        target = next;
        labels = list.Select(x => "GPU " + x.Index).ToArray();
        hover = -1;
        blend = IsLoaded && IsVisible && Motion.Enabled ? 0 : 1;
        began = MotionClock.Now;
        if (blend < 1) MotionClock.Add(this);
        InvalidateVisual();
    }

    public bool Frame(double now)
    {
        blend = Motion.Ease(Math.Min(1, (now - began) / .46));
        InvalidateVisual();
        return blend < 1 && IsVisible && IsLoaded;
    }

    protected override void OnRender(DrawingContext dc)
    {
        double bottom = Math.Max(2, ActualHeight - 2), height = Math.Max(1, ActualHeight - 4);
        if (target.Length == 0) {
            dc.DrawLine(new Pen(Theme.Line, 1), new Point(0, bottom), new Point(ActualWidth, bottom));
            return;
        }
        double unit = ActualWidth / target.Length;
        double width = Math.Max(.5, Math.Min(8, unit - Math.Min(3, unit / 3)));
        for (int i = 0; i < target.Length; i++) {
            double x = unit * i + (unit - width) / 2;
            dc.DrawRoundedRectangle(Theme.Raised, null, new Rect(x, 2, width, height), 1, 1);
            double value = start[i] + (target[i] - start[i]) * blend;
            if (value <= 0) continue;
            double used = Math.Max(1, height * value / 100);
            dc.DrawRoundedRectangle(Theme.Accent, null, new Rect(x, bottom - used, width, used), 1, 1);
        }
    }
}
