import type { Sender } from './types';

export interface TransportOptions {
  endpoint: string;
  key: string;
  fetchFn?: typeof fetch;
  /** navigator.sendBeacon, injectable for tests. */
  beacon?: (url: string, data: Blob) => boolean;
}

/**
 * Builds the function that posts a batch to the ingest API.
 * - While the page is closing it uses sendBeacon (the only call that survives unloading).
 * - A 4xx answer (except 429) is final: retrying cannot help, so the batch is dropped.
 * - Network errors, 429 and 5xx are retried later.
 */
export function createSender(options: TransportOptions): Sender {
  const url = `${options.endpoint.replace(/\/+$/, '')}/v1/collect`;
  const doFetch = options.fetchFn ?? (typeof fetch === 'function' ? fetch.bind(globalThis) : undefined);
  const beacon =
    options.beacon ??
    (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function'
      ? (u: string, d: Blob) => navigator.sendBeacon(u, d)
      : undefined);

  return async (batch, { unloading }) => {
    const body = JSON.stringify({ events: batch });

    if (unloading && beacon) {
      // Beacons cannot set headers, so the key travels in the query string.
      return beacon(`${url}?k=${encodeURIComponent(options.key)}`, new Blob([body], { type: 'text/plain' }));
    }
    if (!doFetch) return false;

    try {
      const response = await doFetch(url, {
        method: 'POST',
        headers: { 'content-type': 'application/json', 'x-tarantula-key': options.key },
        body,
        keepalive: true,
      });
      if (response.ok) return true;
      return response.status >= 400 && response.status < 500 && response.status !== 429;
    } catch {
      return false;
    }
  };
}
