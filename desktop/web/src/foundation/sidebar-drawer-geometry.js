export function drawerGeometry(fromWidth,toWidth){
 if(!Number.isFinite(fromWidth)||!Number.isFinite(toWidth)||fromWidth<=0||toWidth<=0)return null;
 const surfaceWidth=Math.max(fromWidth,toWidth);
 return {fromWidth,toWidth,surfaceWidth,mainOffset:fromWidth-toWidth,fromInset:surfaceWidth-fromWidth,toInset:surfaceWidth-toWidth};
}
