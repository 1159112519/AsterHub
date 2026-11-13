// Only the top layer handles outside/Escape; one document listener pair.
export function createDismissStack(root) {
  const layers = []; let watching = false;
  const top = () => layers.at(-1);
  const pointer = event => { const layer = top(); if (layer && layer.outside !== false && !layer.contains(event.target)) layer.onDismiss('outside', event); };
  const key = event => { const layer = top(); if (event.key === 'Escape' && layer && layer.escape !== false && !event.defaultPrevented) {
    event.preventDefault?.(); event.stopPropagation?.(); layer.onDismiss('escape', event);
  }};
  function watch() { if (watching) return; watching = true; root.addEventListener('pointerdown', pointer, true); root.addEventListener('keydown', key, true); }
  function unwatch() { if (!watching || layers.length) return; watching = false; root.removeEventListener('pointerdown', pointer, true); root.removeEventListener('keydown', key, true); }
  return {
    add(layer) { layers.push(layer); watch(); return {
      isTop: () => top() === layer,
      remove() { const index = layers.indexOf(layer); if (index !== -1) layers.splice(index, 1); unwatch(); }
    }; },
    diagnostics: () => ({layers: layers.length, listeners: watching ? 2 : 0})
  };
}
