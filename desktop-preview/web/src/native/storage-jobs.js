import {createPollingResource} from '../foundation/polling-resource.js';
import {native} from './bridge.js';
import {createStorageDeviceState} from './storage-device-state.js';
export const isActiveTask = task => Boolean(task && ['queued','running','enumerating','aggregating','paused'].includes(task.status));
// A single job-list request serves the sidebar and any future task surface.
export const storageJobs = createPollingResource({interval: 1500, load: signal => native.request('storage.jobs', {}, {signal})});
export const storageDeviceState = createStorageDeviceState({request: (...args) => native.request(...args)});
export const storageStatusResource = deviceId => storageDeviceState.resource(deviceId);
