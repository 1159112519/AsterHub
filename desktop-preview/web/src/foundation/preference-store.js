// One source watcher serves any number of React consumers.
export function createPreferenceStore(read, watch) {
  const listeners = new Set(); let stop = null, snapshot = read();
  const changed = () => { const value = read(); if (Object.is(value, snapshot)) return; snapshot = value; for (const entry of [...listeners]) entry.fn(); };
  return {
    getSnapshot: () => read(),
    subscribe(fn) {
      const entry = {fn}; listeners.add(entry);
      if (!stop) { snapshot = read(); stop = watch(changed); changed(); }
      return () => { if (!listeners.delete(entry)) return; if (!listeners.size) { stop?.(); stop = null; } };
    },
    diagnostics: () => ({subscribers: listeners.size, watchers: stop ? 1 : 0})
  };
}
