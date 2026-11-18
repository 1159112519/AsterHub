// Center the visible union, not the wider reserved layout cells.
export function previewContentOffset(width,ranges){
 const valid=ranges.filter(r=>Number.isFinite(r.left)&&Number.isFinite(r.right)&&r.right>=r.left);
 if(!Number.isFinite(width)||!valid.length)return 0;
 return (width-Math.min(...valid.map(r=>r.left))-Math.max(...valid.map(r=>r.right)))/2;
}
