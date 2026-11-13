import test from 'node:test';import assert from 'node:assert/strict';import {createTaskFocus} from './task-focus.js';
test('task focus waits for target mount and never leaks to another device',()=>{const focus=createTaskFocus();focus.request('B');assert.equal(focus.read('A'),0);const ticket=focus.read('B');assert.ok(ticket>0);focus.consume('B',ticket);assert.equal(focus.read('B'),0)});
test('stale task view cannot consume a newer click',()=>{const focus=createTaskFocus();focus.request('A');const old=focus.read('A');focus.request('A');focus.consume('A',old);assert.ok(focus.read('A')>old)});
