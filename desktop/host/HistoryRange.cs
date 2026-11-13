using System;
using System.Collections.Generic;
using System.Globalization;

internal sealed class HistoryRange {
 internal DateTime From,To;
 internal static HistoryRange Parse(Dictionary<string,object> p,DateTime now){
  object from,to;bool hasFrom=p.TryGetValue("fromUtc",out from),hasTo=p.TryGetValue("toUtc",out to);
  if(hasFrom!=hasTo)throw new ArgumentException("请选择完整的开始和结束时间。");
  if(!hasFrom){int minutes=p.ContainsKey("minutes")?Convert.ToInt32(p["minutes"]):60;if(minutes<1||minutes>1440)throw new ArgumentException("历史时间范围无效。");return new HistoryRange{From=now.AddMinutes(-minutes),To=now};}
  try{double a=Convert.ToDouble(from,CultureInfo.InvariantCulture),b=Convert.ToDouble(to,CultureInfo.InvariantCulture);var epoch=new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc);
   if(from==null||to==null||double.IsNaN(a)||double.IsNaN(b)||double.IsInfinity(a)||double.IsInfinity(b)||a<0||b<=a||b-a>366d*86400000||b>(now-epoch).TotalMilliseconds+60000)throw new ArgumentException();return new HistoryRange{From=epoch.AddMilliseconds(a),To=epoch.AddMilliseconds(b)};
  }catch(Exception ex){if(!(ex is ArgumentException||ex is FormatException||ex is OverflowException))throw;throw new ArgumentException("开始时间应早于结束时间，结束时间不能晚于当前时间，单次范围不超过 366 天。");}
 }
}
