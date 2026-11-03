// Cancellation stops local waiting. Native jobs require their own control action.
export function createRequestScope() {
  const lanes = new Map();
  function cancel(key) { const entry = lanes.get(key); if (entry) { lanes.delete(key); entry.abort(); } }
  return {
    get size() { return lanes.size; },
    begin(key) {
      cancel(key); const controller = new AbortController(); lanes.set(key, controller);
      return {signal: controller.signal,
        isCurrent: () => lanes.get(key) === controller && !controller.signal.aborted,
        finish() { if (lanes.get(key) === controller) lanes.delete(key); }};
    },
    cancel,
    cancelAll() { for (const key of [...lanes.keys()]) cancel(key); }
  };
}
