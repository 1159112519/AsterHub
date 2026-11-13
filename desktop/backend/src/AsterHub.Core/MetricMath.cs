using System.Globalization;
public static class MetricMath {
 public static double Number(string text){double value;return double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out value)?value:0;}
}
