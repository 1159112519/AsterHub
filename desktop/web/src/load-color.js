export function loadColor(value){
 if(!Number.isFinite(value))return 'var(--muted)';
 const n=Math.max(0,Math.min(100,value));
 if(n<55)return `color-mix(in srgb,var(--blue) ${Math.round(35*(1-n/55))}%,var(--green))`;
 if(n<85)return `color-mix(in srgb,var(--amber) ${Math.round((n-55)/30*100)}%,var(--green))`;
 return `color-mix(in srgb,var(--red,#ca9289) ${Math.round((n-85)/15*70)}%,var(--amber))`;
}
