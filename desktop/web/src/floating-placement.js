// Inspectors belong to the content workspace and prefer the trigger's right edge.
export function inspectorHorizontalPosition({anchorRight,viewportWidth,sidebarRight=0,preferredWidth=600,margin=20,gap=20}){
 const minLeft=Math.max(margin,sidebarRight>0?sidebarRight+margin:margin);
 const width=Math.min(preferredWidth,Math.max(1,viewportWidth-minLeft-margin));
 const maxLeft=Math.max(minLeft,viewportWidth-margin-width);
 return {left:Math.min(maxLeft,Math.max(minLeft,anchorRight+gap)),width};
}
