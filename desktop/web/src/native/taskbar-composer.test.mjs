import test from 'node:test';import assert from 'node:assert/strict';
import {displayModule,editLayout,migrateLayout} from './taskbar-composer.js';
const fixture=()=>migrateLayout({slots:6,page:1,pages:[['cpu',null,'gpu','memory','future','storageWrite'],['cpu','down']]});
test('legacy six cells preserve all three columns and coordinates',()=>{
 const layout=fixture();assert.equal(layout.pages.length,2);assert.equal(layout.activePageId,layout.pages[1].id);
 assert.deepEqual(layout.pages[0].slots.map(s=>s.module?.metricId??null),['cpu',null,'gpu','memory','future','storageWrite']);
 assert.equal(layout.pages[0].grid.columns,3);assert.equal(layout.pages[1].slots[0].module.metricId,'cpu');
});
test('increasing a prior two-column page restores archived third-column slots without duplicates',()=>{
 const base=fixture(),page=base.pages[0],archived=page.slots.filter(s=>s.column===2);
 const previous={...base,pages:[{...page,grid:{rows:2,columns:2},slots:page.slots.filter(s=>s.column<2),retiredSlots:archived},...base.pages.slice(1)]};
 previous.pages[0].slots[1]={...previous.pages[0].slots[1],module:archived[0].module};
 const widened=editLayout(previous,page.id,{type:'grid',columns:3}).layout.pages[0];
 assert.deepEqual(widened.slots.map(s=>s.module?.metricId??null),['cpu','gpu',null,'memory','future','storageWrite']);
 assert.equal(widened.slots[5].id,archived[1].id);assert.deepEqual(widened.retiredSlots,[archived[0]]);
});
test('preview swaps modules without moving slot identities, including after serialization',()=>{
 const base=fixture(),[a,b]=base.pages[0].slots;const draft=editLayout(base,base.pages[0].id,{type:'swap',from:a.id,to:b.id}).layout;
 assert.equal(draft.pages[0].slots[1].module.id,a.module.id);assert.equal(draft.pages[0].slots[1].id,b.id);assert.equal(draft.pages[0].slots[0].module,null);
 assert.equal(JSON.parse(JSON.stringify(draft)).pages[0].slots[1].module.id,a.module.id);assert.equal(base.pages[0].slots[0].module.id,a.module.id);
});
test('library cannot add a duplicate to the page or insert content into a different metric',()=>{
 const base=fixture(),page=base.pages[0], [a,b,,c]=page.slots;
 const duplicate=editLayout(base,page.id,{type:'library',metric:'cpu',to:b.id});assert.equal(duplicate.focus,a.id);assert.equal(duplicate.layout,base);
 assert.equal(editLayout(base,page.id,{type:'library',metric:'memory',part:'line',to:a.id,index:0}).layout,base);
 assert.ok(editLayout(base,page.id,{type:'library',metric:'memory',part:'line',to:c.id,index:0}).layout.pages[0].slots[3].module.parts.includes('line'));
});
test('three columns replace the value component and keep existing wider-layout compositions dormant',()=>{
 const base=migrateLayout({slots:4,pages:[['cpu',null,'gpu',null]]}),page=base.pages[0],cpu=page.slots[0];
 const composed=editLayout(base,page.id,{type:'library',to:cpu.id,metric:'cpu',part:'line',index:0}).layout;
 assert.deepEqual(composed.pages[0].slots[0].module.parts,['line']);
 const dual=editLayout(composed,page.id,{type:'library',to:cpu.id,metric:'cpu',part:'number',index:1}).layout;
 const expanded=editLayout(dual,page.id,{type:'grid',columns:3}).layout;
 const stored=expanded.pages[0].slots[0].module;
 assert.deepEqual(stored.parts,['line','number']);assert.deepEqual(displayModule(stored,3).parts,['number']);
 assert.equal(editLayout(expanded,page.id,{type:'order',from:cpu.id}).layout,expanded);
 assert.deepEqual(editLayout(expanded,page.id,{type:'grid',columns:2}).layout.pages[0].slots[0].module.parts,['line','number']);
 const chart=editLayout(expanded,page.id,{type:'library',to:cpu.id,metric:'cpu',part:'line',index:1}).layout;
 assert.deepEqual(chart.pages[0].slots[0].module.parts,['line']);
 const number=editLayout(chart,page.id,{type:'library',to:cpu.id,metric:'cpu',part:'number',index:0}).layout;
 assert.deepEqual(number.pages[0].slots[0].module.parts,['number']);
});
test('return to shelf preserves a two-column composition and resizing never deletes occupied columns',()=>{
 const base=migrateLayout({slots:4,pages:[['cpu',null,'gpu',null]]}),page=base.pages[0], [a,b]=page.slots;
 const composed=editLayout(base,page.id,{type:'library',to:a.id,metric:'cpu',part:'line',index:1}).layout;
 const removed=editLayout(composed,page.id,{type:'remove',from:a.id}).layout;
 const restored=editLayout(removed,page.id,{type:'library',to:b.id,metric:'cpu'}).layout;
 assert.deepEqual(restored.pages[0].slots[1].module.parts,['number','line']);
 const crowded=fixture();assert.equal(editLayout(crowded,crowded.pages[0].id,{type:'grid',columns:1}).layout,crowded);
});
test('resetting horizontal offsets leaves vertical alignment and other scopes intact',()=>{
 const base=fixture(),page=base.pages[0];base.typography={global:{labelX:1,number:-.25},metrics:{cpu:{numberX:2},gpu:{unitX:-1}}};
 const reset=editLayout(base,page.id,{type:'resetTypographyX',metric:'cpu'}).layout;
 assert.deepEqual(reset.typography,{global:{labelX:1,number:-.25},metrics:{cpu:{},gpu:{unitX:-1}}});
 assert.equal(base.typography.metrics.cpu.numberX,2);assert.equal(reset.pages[0].slots[0].module,base.pages[0].slots[0].module);
});
test('per-metric typography retains the global setting and other metric offsets',()=>{
 const base=fixture(),p=base.pages[0];const g=editLayout(base,p.id,{type:'typography',part:'label',value:.5}).layout;
 const m=editLayout(g,p.id,{type:'typography',metric:'cpu',part:'number',value:-.25}).layout;
 assert.equal(m.typography.global.label,.5);assert.equal(m.typography.metrics.cpu.number,-.25);assert.equal(g.typography.metrics,undefined);
});
