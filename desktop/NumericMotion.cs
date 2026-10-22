using System;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Controls;
// Uses the approved HTML's 900 ms cubic ease-out, on the existing shared clock.
public sealed class NumericMotion : IFrameMotion {
 static readonly ConditionalWeakTable<TextBlock,NumericMotion> Values=new ConditionalWeakTable<TextBlock,NumericMotion>();
 readonly TextBlock text;double[] current=new double[0],start=new double[0],target=new double[0];double started;bool initialized,moving;string[] formats=new string[0],fragments=new string[0];
 public double DisplayValue{get{return current.Length==0?double.NaN:current[0];}}public bool IsAnimating{get{return moving;}}
 NumericMotion(TextBlock element){text=element;text.Unloaded+=(s,e)=>Finish();text.IsVisibleChanged+=(s,e)=>{if(!text.IsVisible)Finish();};}
 public static NumericMotion For(TextBlock element){return Values.GetValue(element,x=>new NumericMotion(x));}
 public static void Text(TextBlock element,string value){var controller=For(element);var matches=Regex.Matches(value??"",@"-?[0-9]+(?:\.[0-9]+)?");if(matches.Count==0){controller.Stop();controller.initialized=false;element.Text=value;return;}var numbers=new double[matches.Count];var masks=new string[matches.Count];var parts=new string[matches.Count+1];int end=0;for(int i=0;i<matches.Count;i++){var match=matches[i];parts[i]=value.Substring(end,match.Index-end);end=match.Index+match.Length;if(!double.TryParse(match.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out numbers[i])){controller.Stop();controller.initialized=false;element.Text=value;return;}int dot=match.Value.IndexOf('.');masks[i]=dot<0?"0":"0."+new string('0',match.Length-dot-1);}parts[parts.Length-1]=value.Substring(end);controller.SetValues(numbers,masks,parts);}
 public void Set(double value,string numberFormat,string unit){if(double.IsNaN(value)||double.IsInfinity(value)){Stop();initialized=false;text.Text="—";return;}SetValues(new[]{value},new[]{numberFormat},new[]{"",unit});}
 void SetValues(double[] values,string[] masks,string[] parts){bool sameShape=target.Length==values.Length;bool unchanged=initialized&&sameShape&&target.SequenceEqual(values);if(moving&&!unchanged)Advance((MotionClock.Now-started)*1000);formats=masks;fragments=parts;if(unchanged){Render();return;}target=values;if(!initialized||!sameShape||!text.IsLoaded||!text.IsVisible||!Motion.Enabled){initialized=true;Finish();return;}start=(double[])current.Clone();started=MotionClock.Now;moving=true;MotionClock.Add(this);}
 public bool Frame(double now){if(!text.IsLoaded||!text.IsVisible||!Motion.Enabled){Finish();return false;}Advance((now-started)*1000);return moving;}
 public void Advance(double elapsed){double t=Math.Max(0,Math.Min(1,elapsed/900));for(int i=0;i<target.Length;i++)current[i]=start[i]+(target[i]-start[i])*(1-Math.Pow(1-t,3));if(t>=1){current=(double[])target.Clone();Stop();}Render();}
 void Render(){if(fragments.Length==0)return;var output=new StringBuilder(fragments[0]);for(int i=0;i<current.Length;i++)output.Append(current[i].ToString(formats[i],CultureInfo.InvariantCulture)).Append(fragments[i+1]);string value=output.ToString();if(text.Text!=value)text.Text=value;}
 void Stop(){moving=false;MotionClock.Remove(this);}void Finish(){Stop();if(!initialized)return;current=(double[])target.Clone();Render();}
}
