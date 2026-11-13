import test from 'node:test';import assert from 'node:assert/strict';import {directoryBackdrop} from './directory-surface.js';
test('directory backdrop returns to the settled colour before the portal is removed',()=>{assert.equal(directoryBackdrop(0),1);assert.equal(directoryBackdrop(1),1);assert.equal(directoryBackdrop(.5),.92);assert.ok(directoryBackdrop(.999)>.999)});
