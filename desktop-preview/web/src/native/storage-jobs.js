import {createPollingResource} from '../foundation/polling-resource.js';
import {native} from './bridge.js';
export const isActiveTask = task => Boolean(task && ['queued','running','enumerating','aggregating','paused'].includes(task.status));
// A single job-list request serves the sidebar and any future task surface.
export const storageJobs = createPollingResource({interval: 1500, load: signal => native.request('storage.jobs', {}, {signal})});
export function storageStatusResource(deviceId) {
  return createPollingResource({interval: 900, load: signal => native.request('storage.status', {deviceId}, {signal})});
}
