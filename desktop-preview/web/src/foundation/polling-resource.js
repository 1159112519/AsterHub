// Shared polling is serial, reference-counted, and ignores results after release.
export function createPollingResource({load, interval = 1500}) {
  if (!(interval > 0)) throw new RangeError('Invalid polling interval');
  const listeners = new Set(); let state = {data: null, error: null, loading: false};
  let timer = null, current = null, revision = 0, disposed = false;
  const emit = patch => { state = {...state, ...patch}; for (const entry of [...listeners]) entry.fn(); };
  const clearTimer = () => { if (timer !== null) clearTimeout(timer); timer = null; };
  function stop() {
    clearTimer(); revision++; const previous = current; current = null; previous?.controller.abort();
    state = {...state, loading: false};
  }
  function refresh() {
    if (disposed || !listeners.size) return Promise.resolve();
    if (current) return current.promise;
    clearTimer(); const controller = new AbortController(), id = ++revision;
    const run = {controller}; current = run; emit({loading: true});
    run.promise = Promise.resolve().then(() => controller.signal.aborted ? undefined : load(controller.signal)).then(data => {
      if (id === revision && listeners.size && !controller.signal.aborted) emit({data, error: null});
    }).catch(error => {
      if (id === revision && listeners.size && error.name !== 'AbortError') emit({error});
    }).finally(() => {
      if (current !== run) return;
      current = null; emit({loading: false});
      if (!disposed && listeners.size) timer = setTimeout(refresh, interval);
    });
    return run.promise;
  }
  return {
    getSnapshot: () => state,
    subscribe(fn) {
      if (disposed) return () => {};
      const entry = {fn}; listeners.add(entry); if (listeners.size === 1) refresh();
      return () => { if (listeners.delete(entry) && !listeners.size) stop(); };
    },
    refresh,
    dispose() { disposed = true; listeners.clear(); stop(); state = {data: null, error: null, loading: false}; },
    diagnostics: () => ({subscribers: listeners.size, pending: Boolean(current), scheduled: timer !== null, disposed})
  };
}
