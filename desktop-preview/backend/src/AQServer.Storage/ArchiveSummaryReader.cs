using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

// Reads chart fields without materializing archived process/hardware trees.
// Every skipped value is still checked for valid JSON structure.
internal sealed class ArchiveSummaryReader {
 readonly string text;readonly CancellationToken cancel;int at,depth;
 ArchiveSummaryReader(string text,CancellationToken cancel){this.text=text;this.cancel=cancel;}
 internal static ArchivePoint Read(string text,CancellationToken cancel){if(text==null||text.Length>4*1024*1024)throw new InvalidDataException();return new ArchiveSummaryReader(text,cancel).Point();}
 void Space(){while(at<text.Length&&char.IsWhiteSpace(text[at]))at++;}
 bool Take(char value){Space();if(at<text.Length&&text[at]==value){at++;return true;}return false;}
 void Need(char value){if(!Take(value))throw new InvalidDataException("存档 JSON 结构无效");}
 void Check(){if((at&4095)==0)cancel.ThrowIfCancellationRequested();}
 string String(bool keep){
  Space();if(at>=text.Length||text[at++]!='"')throw new InvalidDataException();int start=at;StringBuilder value=null;
  while(at<text.Length){Check();char c=text[at++];if(c=='"')return !keep?null:value==null?text.Substring(start,at-start-1):value.ToString();
   if(c<32)throw new InvalidDataException();
   if(c=='\\'){
    if(keep&&value==null)value=new StringBuilder(text.Substring(start,at-start-1));if(at>=text.Length)throw new InvalidDataException();char e=text[at++],decoded;
    switch(e){case '"':decoded='"';break;case '\\':decoded='\\';break;case '/':decoded='/';break;case 'b':decoded='\b';break;case 'f':decoded='\f';break;case 'n':decoded='\n';break;case 'r':decoded='\r';break;case 't':decoded='\t';break;case 'u':
     if(at+4>text.Length)throw new InvalidDataException();int hex=0;for(int i=0;i<4;i++){char digit=text[at++];int n=digit>='0'&&digit<='9'?digit-'0':digit>='a'&&digit<='f'?digit-'a'+10:digit>='A'&&digit<='F'?digit-'A'+10:-1;if(n<0)throw new InvalidDataException();hex=hex*16+n;}decoded=(char)hex;break;
     default:throw new InvalidDataException();}if(keep)value.Append(decoded);
   }else if(keep&&value!=null)value.Append(c);
  }throw new InvalidDataException();
 }
 void Literal(string expected){for(int i=0;i<expected.Length;i++)if(at>=text.Length||text[at++]!=expected[i])throw new InvalidDataException();}
 void Skip(){
  Space();Check();if(at>=text.Length||++depth>64)throw new InvalidDataException();char c=text[at];
  if(c=='"')String(false);
  else if(c=='{'){at++;if(!Take('}'))do{String(false);Need(':');Skip();if(Take('}'))break;Need(',');}while(true);}
  else if(c=='['){at++;if(!Take(']'))do{Skip();if(Take(']'))break;Need(',');}while(true);}
  else if(c=='n')Literal("null");else if(c=='t')Literal("true");else if(c=='f')Literal("false");
  else{if(c=='-')at++;if(at>=text.Length)throw new InvalidDataException();if(text[at]=='0')at++;else{if(text[at]<'1'||text[at]>'9')throw new InvalidDataException();while(at<text.Length&&text[at]>='0'&&text[at]<='9')at++;}if(at<text.Length&&text[at]=='.'){at++;int start=at;while(at<text.Length&&text[at]>='0'&&text[at]<='9')at++;if(at==start)throw new InvalidDataException();}if(at<text.Length&&(text[at]=='e'||text[at]=='E')){at++;if(at<text.Length&&(text[at]=='+'||text[at]=='-'))at++;int start=at;while(at<text.Length&&text[at]>='0'&&text[at]<='9')at++;if(at==start)throw new InvalidDataException();}}
  depth--;
 }
 string Scalar(){Space();if(at<text.Length&&text[at]=='"')return String(true);int start=at;Skip();string value=text.Substring(start,at-start);return value=="null"?null:value;}
 ArchivePoint Point(){
  var point=new ArchivePoint{Cpu=double.NaN,Memory=double.NaN,Error=""};int version=1,interval=0;var ids=new List<string>();var usages=new List<double>();Need('{');
  if(!Take('}'))do{string key=String(true);Need(':');switch(key){
   case "V":version=Convert.ToInt32(Scalar(),CultureInfo.InvariantCulture);break;
   case "T":point.T=Convert.ToInt64(Scalar(),CultureInfo.InvariantCulture);break;
   case "Interval":interval=Convert.ToInt32(Scalar(),CultureInfo.InvariantCulture);break;
   case "Cpu":point.Cpu=ResourceSample.ParsePercent(Scalar());break;
   case "Mem":point.Memory=ResourceSample.MemoryPercent(Scalar());break;
   case "Error":point.Error=Scalar()??"";break;
   case "Gpus":Gpus(ids,usages);break;
   default:Skip();break;
  }if(Take('}'))break;Need(',');}while(true);
  Space();cancel.ThrowIfCancellationRequested();if(at!=text.Length||version!=1||point.T<DateTime.MinValue.Ticks||point.T>DateTime.MaxValue.Ticks)throw new InvalidDataException();
  point.Interval=Math.Max(2,interval);point.GpuIds=ids.ToArray();point.GpuUsage=usages.ToArray();point.Count=ids.Count;double sum=0;int count=0;foreach(var usage in usages)if(!double.IsNaN(usage)){sum+=usage;count++;}point.Gpu=count==0?double.NaN:sum/count;return point;
 }
 void Gpus(List<string> ids,List<double> usages){
  ids.Clear();usages.Clear();Space();if(at<text.Length&&text[at]=='n'){Literal("null");return;}Need('[');if(Take(']'))return;
  do{string id=null;double usage=double.NaN;Need('{');if(!Take('}'))do{string key=String(true);Need(':');if(key=="Index")id=Scalar();else if(key=="Util")usage=ResourceSample.ParsePercent(Scalar());else Skip();if(Take('}'))break;Need(',');}while(true);ids.Add(id);usages.Add(usage);if(Take(']'))break;Need(',');}while(true);
 }
}
