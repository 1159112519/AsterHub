// A request owns its timer and abort listener until one terminal outcome.
export function createClient(transport, timeout = 12000) {
  let sequence = 0, disposed = false;
  const pending = new Map(), listeners = new Set();
  function settle(id, error, result) {
    const entry = pending.get(id); if (!entry) return;
    pending.delete(id); clearTimeout(entry.timer); entry.signal?.removeEventListener('abort', entry.abort);
    error ? entry.reject(error) : entry.resolve(result);
  }
  const receive = event => {
    if (disposed) return;
    const message = event.data;
    if (['capture', 'capture-light', 'devices', 'navigate', 'update-progress'].includes(message?.type)) {
      for (const entry of [...listeners]) if (entry.type === message.type) entry.fn(message.value);
      return;
    }
    settle(message?.id, message?.ok ? null : new Error(message?.error || '请求失败'), message?.result);
  };
  transport?.addEventListener('message', receive);
  return {
    get available() { return Boolean(transport) && !disposed; },
    subscribe(fn, type = 'capture') {
      if (disposed) return () => {};
      const entry = {fn, type}; listeners.add(entry); return () => listeners.delete(entry);
    },
    request(method, params = {}, options = timeout) {
      if (disposed) return Promise.reject(new Error('连接已关闭'));
      if (!transport) return Promise.reject(new Error('请从 AsterHub.Preview.exe 打开此界面'));
      const {signal, timeout: requestTimeout = timeout} = typeof options === 'number' ? {timeout: options} : (options || {});
      const aborted = () => new DOMException('请求已取消', 'AbortError');
      if (signal?.aborted) return Promise.reject(aborted());
      if (!Number.isFinite(requestTimeout) || requestTimeout <= 0) return Promise.reject(new RangeError('无效的请求超时'));
      const id = String(++sequence);
      return new Promise((resolve, reject) => {
        const entry = {resolve, reject, signal, abort: () => settle(id, aborted())};
        entry.timer = setTimeout(() => settle(id, new Error('请求超时，请重试')), requestTimeout);
        pending.set(id, entry); signal?.addEventListener('abort', entry.abort, {once: true});
        try { transport.postMessage({id, method, params}); } catch (error) { settle(id, error); }
      });
    },
    dispose() {
      if (disposed) return; disposed = true; transport?.removeEventListener('message', receive); listeners.clear();
      for (const id of [...pending.keys()]) settle(id, new Error('连接已关闭'));
    },
    diagnostics: () => ({pending: pending.size, subscribers: listeners.size, disposed})
  };
}
export const native = createClient(globalThis.chrome?.webview);
