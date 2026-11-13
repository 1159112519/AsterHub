using System;
using System.Linq;
public sealed class TerminalPreferences {
 public string FontFamily{get;set;}public int FontSize{get;set;}public double LineHeight{get;set;}public double LetterSpacing{get;set;}public string CursorStyle{get;set;}public bool CursorBlink{get;set;}public int Scrollback{get;set;}
 public TerminalPreferences(){FontFamily="Cascadia Mono";FontSize=16;LineHeight=1.35;LetterSpacing=0;CursorStyle="bar";CursorBlink=true;Scrollback=5000;}
 public TerminalPreferences Copy(){var p=(TerminalPreferences)MemberwiseClone();p.Normalize();return p;}
 public void Normalize(){if(string.IsNullOrWhiteSpace(FontFamily)||FontFamily.Length>128||FontFamily.Any(char.IsControl))FontFamily="Cascadia Mono";FontSize=Math.Max(10,Math.Min(32,FontSize));LineHeight=double.IsNaN(LineHeight)||double.IsInfinity(LineHeight)?1.35:Math.Max(1,Math.Min(1.8,LineHeight));LetterSpacing=double.IsNaN(LetterSpacing)||double.IsInfinity(LetterSpacing)?0:Math.Max(0,Math.Min(2,LetterSpacing));if(!new[]{"bar","block","underline"}.Contains(CursorStyle))CursorStyle="bar";Scrollback=Math.Max(500,Math.Min(100000,Scrollback));}
}
