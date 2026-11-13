export const colors={cpu:'var(--blue)',gpu:'var(--chart-gpu,var(--green))',memory:'var(--purple)',network:'var(--amber)',storage:'var(--muted)'};
export function wave(seed=1,n=66,base=40,amp=16){return Array.from({length:n},(_,i)=>Math.max(2,Math.min(99,base+Math.sin(i*.33+seed)*amp*.45+Math.sin(i*.89+seed*3)*amp*.22+Math.cos(i*1.77+seed)*amp*.16+Math.sin(i*.1)*amp*.25)));}
export function minutesLabel(index,total=66,start=9*60){const value=start+Math.round(index/(total-1)*15);return `${String(Math.floor(value/60)).padStart(2,'0')}:${String(value%60).padStart(2,'0')}`;}
