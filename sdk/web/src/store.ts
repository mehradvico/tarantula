import type { KeyValueStore } from './types';

export function memoryStore(): KeyValueStore {
  const map = new Map<string, string>();
  return {
    get: (key) => map.get(key) ?? null,
    set: (key, value) => void map.set(key, value),
    remove: (key) => void map.delete(key),
  };
}

/** localStorage when it works (it can throw in private mode or when blocked), memory otherwise. */
export function browserStore(): KeyValueStore {
  try {
    const ls = globalThis.localStorage;
    const probe = '__tk_probe__';
    ls.setItem(probe, '1');
    ls.removeItem(probe);
    return {
      get: (key) => ls.getItem(key),
      set: (key, value) => ls.setItem(key, value),
      remove: (key) => ls.removeItem(key),
    };
  } catch {
    return memoryStore();
  }
}
