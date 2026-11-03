import {useEffect, useRef} from 'react';
import {createDismissStack} from './dismiss-stack.js';
let stack;
export function useDismissableLayer({open = true, refs, onDismiss, outside = true, escape = true}) {
  const latest = useRef(); latest.current = {refs, onDismiss, outside, escape}; const layer = useRef(null);
  useEffect(() => {
    if (!open) return;
    stack ??= createDismissStack(document);
    const entry = stack.add({contains: target => latest.current.refs.some(ref => ref.current?.contains(target)),
      onDismiss: (reason, event) => latest.current.onDismiss(reason, event),
      get outside() { return latest.current.outside; }, get escape() { return latest.current.escape; }});
    layer.current = entry;
    return () => { entry.remove(); if (layer.current === entry) layer.current = null; };
  }, [open]);
  return {isTop: () => Boolean(layer.current?.isTop())};
}
export function overlayDiagnostics() { return stack?.diagnostics() ?? {layers: 0, listeners: 0}; }
