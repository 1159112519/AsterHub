import test from 'node:test';
import assert from 'node:assert/strict';
import {buildTaskGroups, countPendingTasks} from './task-center-model.js';

test('lists tasks for every device, including completed jobs, without treating them as pending',()=>{
 const groups=buildTaskGroups({state:'ready',latest:'0.4.2.4',ready:true},[
  {id:'ready',status:'completed'},{id:'copy',status:'running'},{id:'undo',status:'undoing'},
 ],[{id:'old',deviceId:'local',status:'ready'},{id:'new',deviceId:'remote',status:'paused'}]);
 assert.deepEqual(groups.map(g=>[g.kind,g.jobs.map(x=>x.id)]),[['update',['update']],['transfer',['copy','undo','ready']],['scan',['new','old']]]);
 assert.equal(countPendingTasks(groups),4);
});

test('does not invent an update download when checking or when a release is only offered',()=>{
 for(const state of ['idle','checking','available','current']) assert.deepEqual(buildTaskGroups({state,latest:'0.4.2.4',available:true},[],[]),[]);
 assert.deepEqual(buildTaskGroups({state:'error',available:false},[],[]),[]);
 const groups=buildTaskGroups({state:'cancelled',available:true,latest:'0.4.2.4'},[],[]);
 assert.equal(groups[0].jobs[0].status,'cancelled');
 assert.equal(countPendingTasks(groups),0);
});


test('background index preparation shares task inbox lifecycle without becoming a disk scan',()=>{
 const groups=buildTaskGroups({},[],[{id:'index-1',kind:'index',status:'running',current:'正在构建索引',enumerated:4096},{id:'index-2',kind:'index',status:'ready',finished:'2026-11-12T10:00:00Z'}]);
 assert.equal(groups[0].kind,'scan');assert.equal(groups[0].jobs[0].kind,'index');assert.equal(countPendingTasks(groups),1);
});

test('clearing finished task notifications preserves active work and ready updates after reopening',async()=>{
 const {clearFinishedTasks,recordTaskInbox,taskInboxGroups,readTaskInbox,writeTaskInbox}=await import('./task-inbox.js');
 const time='2026-11-12T10:00:00Z';
 const groups=buildTaskGroups({state:'ready',latest:'0.4.3.4'},[{id:'done',status:'completed'},{id:'running',status:'running'}],[{id:'failed',status:'failed'},{id:'paused',status:'paused'}]);
 const previous=recordTaskInbox({},[{kind:'cleanup',jobs:[{id:'archived',status:'completed'}]}],time);
 const cleared=clearFinishedTasks(previous,groups);
 let persisted;const storage={getItem:()=>persisted,setItem:(_,value)=>{persisted=value}};
 writeTaskInbox(storage,cleared);
 const reopened=recordTaskInbox(readTaskInbox(storage),groups,time);
 assert.deepEqual(taskInboxGroups(groups,reopened).map(({kind,jobs})=>[kind,jobs.map(x=>x.id)]),[['update',['update']],['transfer',['running']],['scan',['paused']]]);
 assert.equal(countPendingTasks(taskInboxGroups(groups,reopened)),3);
 const next=buildTaskGroups({state:'ready',latest:'0.4.3.4'},[{id:'running',status:'completed',finished:time}]);
 assert.deepEqual(taskInboxGroups(next,recordTaskInbox(reopened,next,time)).find(g=>g.kind==='transfer').jobs.map(x=>x.id),['running']);
});


test('clear finished preserves live resumable update downloads and only archives older failures',async()=>{
 const {canClearFinishedTask,clearFinishedTasks,taskInboxGroups,recordTaskInbox}=await import('./task-inbox.js');
 for(const status of ['downloading','cancelling','ready','installing','error','cancelled'])assert.equal(canClearFinishedTask('update',{status}),false,status);
 const previous=recordTaskInbox({},[{kind:'update',jobs:[{latest:'0.4.3.3',status:'error'}]}]);
 const groups=buildTaskGroups({state:'error',latest:'0.4.3.4',available:true});
 const cleared=clearFinishedTasks(previous,groups);
 assert.deepEqual(taskInboxGroups(groups,cleared).flatMap(group=>group.jobs.map(job=>job.latest)),['0.4.3.4']);
 assert.equal(cleared['update:0.4.3.3'].dismissed,true);
});
