// Cache size is a feature contract: it must never grow with every click.
export class BoundedCache {
  #entries = new Map();
  #weight = 0;
  constructor({maxEntries = 64, maxWeight = Infinity, weight = () => 1} = {}) {
    if (!Number.isInteger(maxEntries) || maxEntries < 1 || !(maxWeight > 0)) throw new RangeError('Invalid cache capacity');
    this.maxEntries = maxEntries; this.maxWeight = maxWeight; this.measure = weight;
  }
  get size() { return this.#entries.size; }
  get weight() { return this.#weight; }
  has(key) { return this.#entries.has(key); }
  get(key) {
    const entry = this.#entries.get(key); if (!entry) return undefined;
    this.#entries.delete(key); this.#entries.set(key, entry); return entry.value;
  }
  delete(key) {
    const entry = this.#entries.get(key); if (!entry) return false;
    this.#weight -= entry.weight; return this.#entries.delete(key);
  }
  set(key, value) {
    const weight = this.measure(value);
    if (!Number.isFinite(weight) || weight < 0) throw new RangeError('Invalid cache entry weight');
    this.delete(key);
    if (weight > this.maxWeight) return false;
    this.#entries.set(key, {value, weight}); this.#weight += weight;
    while (this.size > this.maxEntries || this.#weight > this.maxWeight) this.delete(this.#entries.keys().next().value);
    return true;
  }
  clear() { this.#entries.clear(); this.#weight = 0; }
}
