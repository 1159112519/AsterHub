import {test} from 'node:test';import assert from 'node:assert/strict';import {resolveAppearance,skinPacks,contrast,applyAppearance} from './resolve-appearance.js';
test('all numbered skins remain readable in both explicit and system modes',()=>{for(const p of skinPacks)for(const appearanceMode of ['dark','light','system'])for(const systemDark of [false,true]){const a=resolveAppearance({theme:p.id,appearanceMode},systemDark);assert.equal(a.mode,appearanceMode==='system'?(systemDark?'dark':'light'):appearanceMode);assert(contrast(a.tokens.text,a.tokens.surface)>=4.5);assert(contrast(a.tokens.green,a.tokens['green-soft'])>=4.5);assert(contrast(a.tokens['chart-gpu'],a.tokens.surface)>=4.5)}});
test('classic preferences keep their own palette when icon follow is off',()=>{for(const theme of ['graphite','slate','forest','ocean','light'])assert.equal(resolveAppearance({theme}).tokens,null);assert.equal(resolveAppearance({theme:'graphite',followIcon:false},false,'09').id,'graphite');assert.equal(resolveAppearance({theme:'bad'}).id,'graphite')});
test('complete skins vary surfaces and functional colors; clean light colors do not use white or grey overlays',()=>{
 for(const mode of ['dark','light']){
  const skins=skinPacks.map(p=>resolveAppearance({theme:p.id,appearanceMode:mode}).tokens);
  assert.equal(new Set(skins.map(t=>t.bg)).size,9);assert.equal(new Set(skins.map(t=>t.surface)).size,9);
  assert(new Set(skins.map(t=>t.blue)).size>=7);assert(new Set(skins.map(t=>t.purple)).size>=7);
  for(const t of skins){for(const c of [t.blue,t.purple,t.amber,t['chart-gpu']])assert(contrast(c,t.surface)>=4.5);if(mode==='light'){assert.notEqual(t.surface.toLowerCase(),'#ffffff');const rgb=t.bg.slice(1).match(/../g).map(x=>parseInt(x,16));assert(Math.max(...rgb)-Math.min(...rgb)>15);}}
 }
 assert.equal(resolveAppearance({theme:'06'}).tokens.bg,'#111D2B');assert.equal(resolveAppearance({theme:'08'}).tokens.bg,'#211B1B');
});
test('return to classic clears all inline numbered-skin color overrides',()=>{const values=new Map(),root={dataset:{},style:{setProperty:(k,v)=>values.set(k,v),removeProperty:k=>values.delete(k)}};applyAppearance(resolveAppearance({theme:'03'}),root);assert(values.size>0);applyAppearance(resolveAppearance({theme:'graphite'}),root);assert.equal(values.size,0);assert.equal(root.dataset.theme,'graphite')});

test('obsolete icon follow cannot silently replace an independently selected skin',()=>{for(const theme of ['graphite','slate','forest','ocean','light','03'])for(const iconId of ['01','06','09'])assert.equal(resolveAppearance({theme,followIcon:true},false,iconId).id,theme)});
