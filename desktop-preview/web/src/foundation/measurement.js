// Hardware support, connectivity and numeric zero are independent facts.
export function measurementState(value, {supported = true, stale = false} = {}) {
  if (supported === false) return 'unsupported';
  if (!Number.isFinite(value)) return 'missing';
  return stale ? 'stale' : 'ready';
}
