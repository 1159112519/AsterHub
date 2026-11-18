import test from 'node:test';import assert from 'node:assert/strict';import {previewContentOffset} from './taskbar-preview-geometry.mjs';
test('one, two and three columns center their visible ink without changing internal spacing',()=>{
 for(const [width,ranges] of [[92,[{left:4,right:71}]],[180,[{left:4,right:71},{left:91,right:159}]],[268,[{left:4,right:71},{left:91,right:159},{left:179,right:246}]]]){
  const shift=previewContentOffset(width,ranges);assert.equal((Math.min(...ranges.map(r=>r.left))+Math.max(...ranges.map(r=>r.right)))/2+shift,width/2);
  assert.equal(ranges.at(-1).left-ranges[0].left,(ranges.at(-1).left+shift)-(ranges[0].left+shift));
 }
});
test('empty targets stay centered and invalid geometry never produces NaN',()=>{
 assert.equal(previewContentOffset(180,[{left:36,right:48},{left:132,right:144}]),0);
 assert.equal(previewContentOffset(180,[]),0);assert.equal(previewContentOffset(NaN,[{left:0,right:10}]),0);
});
