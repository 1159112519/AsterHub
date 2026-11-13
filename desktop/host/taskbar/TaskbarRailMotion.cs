using System;
// The rail and its reserved space share one continuous, interruptible progress value.
internal sealed class TaskbarRailMotion {
 internal const int IdleMs=2500,EnterMs=320,ExitMs=520;
 double amount;long last,until;bool initialized;
 internal double Progress{get{return amount*amount*(3-2*amount);}}
 internal bool Active(long now){return now<until?amount<1:amount>0;}
 internal void Reveal(long now,bool reduced){Update(now,reduced);until=now+IdleMs;if(reduced)amount=1;}
 internal void Reset(){amount=0;last=until=0;initialized=false;}
 internal void Update(long now,bool reduced){
  if(!initialized){last=now;initialized=true;}
  if(reduced)amount=now<until?1:0;
  else if(now>=last){long toward=Math.Max(0,Math.Min(now,until)-last),away=now-last-toward;amount=Math.Min(1,amount+toward/(double)EnterMs);amount=Math.Max(0,amount-away/(double)ExitMs);}
  last=now;
 }
}
