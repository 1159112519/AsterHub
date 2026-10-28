using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

public static class FooterLayoutTests
{
    static readonly List<string> log = new List<string>();
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("PASS " + message); }
    static T Field<T>(MonitorWindow w, string name) { return (T)typeof(MonitorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(w); }
    static Rect Bounds(FrameworkElement e, Visual parent) { return e.TransformToAncestor(parent).TransformBounds(new Rect(0, 0, e.ActualWidth, e.ActualHeight)); }
    static double TextWidth(TextBlock text) { return new FormattedText(text.Text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, Brushes.Black, 1).WidthIncludingTrailingWhitespace; }
    static IEnumerable<TextBlock> Texts(DependencyObject node) { var text = node as TextBlock; if (text != null) yield return text; for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++) foreach (var child in Texts(VisualTreeHelper.GetChild(node, i))) yield return child; }
    static void Save(FrameworkElement e, string path, int dpi)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth * dpi / 96), (int)Math.Ceiling(e.ActualHeight * dpi / 96), dpi, dpi, PixelFormats.Pbgra32);
        var scene = new DrawingVisual(); using (var dc = scene.RenderOpen()) dc.DrawRectangle(new VisualBrush(e) { Stretch = Stretch.Fill }, null, new Rect(0, 0, e.ActualWidth, e.ActualHeight)); bitmap.Render(scene); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); using (var file = File.Create(path)) png.Save(file);
    }
    public static async void Run(Application app, string folder)
    {
        Directory.CreateDirectory(folder); MonitorWindow window = null;
        try {
            window = new MonitorWindow(true) { ShowActivated = false, ShowInTaskbar = false, Left = -18000, Top = -18000, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show(); window.Height = 800;
            foreach (string theme in Theme.Keys) {
                window.ApplyAppearance(theme);
                foreach (int width in new[] { 760, 1000, 1280, 1440, 1920 }) {
                    window.Width = width; window.Root.Width = width - 48; window.Root.Height = 752;
                    window.Root.Measure(new Size(width - 48, 752)); window.Root.Arrange(new Rect(0, 0, width - 48, 752)); window.UpdateLayout();
                    var data = Program.Fixture("eight"); data.Cpu = "2.1%"; data.Mem = "46660 / 947361 MiB";
                    DesignReview.Seed(window); window.SetMetrics(data, false); await Task.Delay(550); window.UpdateLayout();
                    var footer = Field<Border>(window, "workspaceFooter"); var cells = Field<Grid[]>(window, "footerCells"); var groups = Field<Border[]>(window, "footerGroups");
                    var plots = Field<FrameworkElement[]>(window, "footerPlots"); var hosts = Field<FrameworkElement[]>(window, "footerValueHosts"); var labels = Field<TextBlock[]>(window, "footerNames");
                    string tag = theme + "-" + width;
                    Check(cells.Length == 5 && groups.All(x => x.Visibility == Visibility.Visible), tag + " five persistent metric groups");
                    var rowY = groups.Select(g => Math.Round(Bounds(g, footer).Top)).Distinct().ToArray();
                    Check(rowY.Length == (footer.ActualWidth - footer.Padding.Left - footer.Padding.Right < 1040 ? 2 : 1), tag + " readable five-column or 3+2 layout");
                    for (int i = 0; i < 5; i++) {
                        var lane = Bounds(cells[i], footer); var lr = Bounds(labels[i], footer); var vr = Bounds(hosts[i], footer); var pr = Bounds(plots[i], footer); var group = Bounds(groups[i], footer);
                        Check(group.Left + 6 <= lr.Left && pr.Right + 6 <= group.Right, tag + " plot visibly inside its own group " + i);
                        Check(lr.Right + 3 <= vr.Left && vr.Right + 3 <= pr.Left, tag + " ordered label/value/plot " + i);
                        Check(lane.Left >= footer.Padding.Left - .6 && lane.Right <= footer.ActualWidth - footer.Padding.Right + .6, tag + " lane fits footer " + i);
                        Check(Math.Abs((lr.Left + pr.Right) / 2 - (lane.Left + lane.Right) / 2) < .8, tag + " complete content centered in group " + i);
                        Check(Math.Abs((lr.Top + lr.Bottom) / 2 - (vr.Top + vr.Bottom) / 2) < 1 && Math.Abs((lr.Top + lr.Bottom) / 2 - (pr.Top + pr.Bottom) / 2) < 1, tag + " common vertical center " + i);
                        foreach (var text in Texts(cells[i])) Check(text.TextAlignment == TextAlignment.Center && TextWidth(text) <= text.ActualWidth + .7, tag + " full centered text " + i + " " + text.Text);
                        for (int j = i + 1; j < 5; j++) {
                            var other = Bounds(groups[j], footer); Check(!group.IntersectsWith(other), tag + " groups never overlap " + i + "/" + j);
                            if (Math.Abs(other.Top - group.Top) < .5) Check(other.Left - group.Right >= 10, tag + " visible group separation " + i + "/" + j);
                        }
                    }
                    foreach (int meterIndex in new[] { 2, 4 }) {
                        var drawing = VisualTreeHelper.GetDrawing(VisualTreeHelper.GetChild(plots[meterIndex], 0) as Visual);
                        Check(drawing != null && drawing.Bounds.Width > 10, tag + " memory/storage meter has visible blocks " + meterIndex);
                    }
                    var before = plots.Select(x => Bounds(x, footer)).ToArray();
                    data.Cpu = "100.0%"; data.Mem = "1024000 / 33554432 MiB";
                    foreach (var gpu in data.Gpus) gpu.Util = "100";
                    data.Network.UploadBytesPerSecond = 9999999999; data.Network.DownloadBytesPerSecond = 3;
                    data.Volumes[0].UsedBytes = data.Volumes[0].TotalBytes - 1;
                    window.SetMetrics(data, false);
                    for (int frame = 0; frame < 8; frame++) {
                        await Task.Delay(65); window.UpdateLayout();
                        for (int i = 0; i < 5; i++) { var rect = Bounds(plots[i], footer); Check(Math.Abs(rect.X - before[i].X) < .1 && Math.Abs(rect.Width - before[i].Width) < .1, tag + " digit and unit animation reserves plot " + i + " frame " + frame); }
                    }
                    foreach (var cell in cells) foreach (var text in Texts(cell)) Check(TextWidth(text) <= text.ActualWidth + .7, tag + " changed text fits " + text.Text);
                    foreach (int dpi in new[] { 96, 144 }) Save(footer, Path.Combine(folder, tag + "-footer-" + dpi + ".png"), dpi);
                    foreach (string page in new[] { "网络", "存储" }) { window.SwitchView(page); window.UpdateLayout(); window.RenderImage(Path.Combine(folder, tag + "-" + (page == "网络" ? "network" : "storage") + ".png"), width, 800, 96); }
                }
            }
            window.Exit(); window = null; File.WriteAllLines(Path.Combine(folder, "footer-results.txt"), new[] { "FAILURES=0", "CHECKS=" + log.Count }.Concat(log)); app.Shutdown(0);
        } catch (Exception error) { File.WriteAllText(Path.Combine(folder, "footer-error.txt"), error.ToString()); File.WriteAllLines(Path.Combine(folder, "footer-results.txt"), log); if (window != null) window.Exit(); app.Shutdown(1); }
    }
}