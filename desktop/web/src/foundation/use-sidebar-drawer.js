import {useCallback,useEffect,useRef} from 'react';
import {flushSync} from 'react-dom';
import {reducedMotion} from '../motion';
import {drawerGeometry} from './sidebar-drawer-geometry';

// Layout changes once; the drawer is clipped and the workspace only translates.
// Neither text nor the live chart surface is scaled during the transition.
export function useSidebarDrawer(collapsed,setCollapsed){
 const running=useRef(null);
 const cleanup=useCallback(()=>{const task=running.current;if(!task)return;running.current=null;task.animations.forEach(a=>a.cancel());task.snapshot?.remove();if(task.sidebar)task.sidebar.style.visibility=task.visibility;},[]);
 useEffect(()=>cleanup,[cleanup]);
 return useCallback(event=>{
  if(running.current)return;
  const root=event?.currentTarget?.closest('.native-workbench'),sidebar=root?.querySelector(':scope > .sidebar'),workspace=root?.querySelector(':scope > .workspace');
  if(!sidebar||!workspace||reducedMotion()){flushSync(()=>setCollapsed(v=>!v));return;}
  const before=sidebar.getBoundingClientRect(),oldSidebar=sidebar.cloneNode(true),oldClass=root.className;
  const anchors=['.sidebar-collapse-toggle','.brand-mark'];
  const beforeAnchors=anchors.map(selector=>sidebar.querySelector(selector)?.getBoundingClientRect());
  flushSync(()=>setCollapsed(v=>!v));
  const after=sidebar.getBoundingClientRect(),geometry=drawerGeometry(before.width,after.width);
  const afterAnchors=anchors.map(selector=>sidebar.querySelector(selector)?.getBoundingClientRect());
  if(!geometry||!geometry.mainOffset)return;
  const opening=after.width>before.width,surface=opening?sidebar.cloneNode(true):oldSidebar;
  const snapshot=document.createElement('div');snapshot.className=(opening?root.className:oldClass)+' sidebar-drawer-snapshot';snapshot.inert=true;snapshot.setAttribute('aria-hidden','true');
  Object.assign(snapshot.style,{position:'fixed',left:before.left+'px',top:before.top+'px',width:geometry.surfaceWidth+'px',height:before.height+'px',display:'block',minHeight:'0',overflow:'hidden',zIndex:'180',pointerEvents:'none',background:'transparent'});
  for(const item of surface.querySelectorAll('[id]'))item.removeAttribute('id');
  Object.assign(surface.style,{position:'absolute',left:'0',top:'0',width:geometry.surfaceWidth+'px',height:'100%',boxSizing:'border-box',transform:'none',overflow:'hidden',visibility:'visible',margin:'0'});
  snapshot.appendChild(surface);document.body.appendChild(snapshot);
  const visibility=sidebar.style.visibility;
  // Keep the destination visible beneath the outgoing surface. Its final geometry
  // is already laid out, so the last frame and the live sidebar are identical.
  const destination=sidebar.animate(opening?[{opacity:0},{opacity:0}]:[{opacity:0},{opacity:0,offset:.68},{opacity:1,offset:.96},{opacity:1}],{duration:340,easing:'cubic-bezier(.22,1,.36,1)',fill:'both'});
  const options={duration:340,easing:'cubic-bezier(.22,1,.36,1)',fill:'both'};
  const clip=snapshot.animate([{clipPath:`inset(0 ${geometry.fromInset}px 0 0)`},{clipPath:`inset(0 ${geometry.toInset}px 0 0)`}],options);
  const move=workspace.animate([{transform:`translateX(${geometry.mainOffset}px)`},{transform:'translateX(0)'}],options);
  const dissolve=snapshot.animate(opening?[{opacity:1},{opacity:1}]:[{opacity:1},{opacity:1,offset:.12},{opacity:0,offset:.84},{opacity:0}],options);
  const anchorAnimations=anchors.flatMap((selector,i)=>{const node=surface.querySelector(selector),a=beforeAnchors[i],b=afterAnchors[i];if(!node||!a||!b)return [];const dx=b.left-a.left;return [node.animate([{transform:`translateX(${opening?-dx:0}px)`},{transform:`translateX(${opening?0:dx}px)`}],options)]});
  const labels=[...surface.querySelectorAll('.sidebar-brand-name,.nav-item>span,.sidebar-section-label,.device-item>span')].map(label=>label.animate(opening?[{opacity:0},{opacity:1,offset:.65},{opacity:1}]:[{opacity:1,transform:'translateX(0)'},{opacity:0,transform:'translateX(-12px)'}],{...options,duration:opening?340:180}));
  running.current={snapshot,sidebar,visibility,animations:[clip,move,destination,dissolve,...labels,...anchorAnimations]};
  Promise.allSettled([clip.finished,move.finished,destination.finished,dissolve.finished]).then(cleanup);
 },[collapsed,setCollapsed,cleanup]);
}
