import { describe, expect, it } from 'vitest';
import { EventQueue } from '../src/queue';
import { createSender } from '../src/transport';
import type { WireEvent } from '../src/types';

const ev = (name: string): WireEvent => ({
  name,
  timestamp: '2026-11-16T10:00:00.000Z',
  anonymousId: 'a_1',
  sessionId: 's_1',
  consent: 'full',
});

describe('EventQueue', () => {
  it('sends in batches of the configured size', async () => {
    const batches: string[][] = [];
    const q = new EventQueue({ maxBatchSize: 2, sender: async (b) => (batches.push(b.map((e) => e.name)), true) });
    ['a', 'b', 'c'].forEach((n) => q.push(ev(n)));
    await q.flush();
    expect(batches.flat()).toEqual(['a', 'b', 'c']);
    expect(batches.every((b) => b.length <= 2)).toBe(true);
    expect(q.size).toBe(0);
  });

  it('keeps events and retries when sending fails', async () => {
    let ok = false;
    const q = new EventQueue({ maxBatchSize: 10, sender: async () => ok });
    q.push(ev('a'));
    expect(await q.flush()).toBe(false);
    expect(q.size).toBe(1);
    ok = true;
    expect(await q.flush()).toBe(true);
    expect(q.size).toBe(0);
  });

  it('treats a throwing sender as a failure', async () => {
    const q = new EventQueue({
      maxBatchSize: 10,
      sender: async () => {
        throw new Error('boom');
      },
    });
    q.push(ev('a'));
    expect(await q.flush()).toBe(false);
    expect(q.size).toBe(1);
  });

  it('drops the oldest events beyond the cap', () => {
    const q = new EventQueue({ maxBatchSize: 1000, maxQueueSize: 3, sender: async () => false });
    ['a', 'b', 'c', 'd', 'e'].forEach((n) => q.push(ev(n)));
    expect(q.size).toBe(3);
  });
});

describe('createSender', () => {
  const batch = [ev('a')];

  it('posts JSON with the key header', async () => {
    let seen: { url: string; init: RequestInit } | undefined;
    const send = createSender({
      endpoint: 'https://collect.example.com/',
      key: 'tk_1',
      fetchFn: async (url, init) => {
        seen = { url: String(url), init: init ?? {} };
        return new Response(null, { status: 202 });
      },
    });
    expect(await send(batch, { unloading: false })).toBe(true);
    expect(seen?.url).toBe('https://collect.example.com/v1/collect');
    expect((seen?.init.headers as Record<string, string>)['x-tarantula-key']).toBe('tk_1');
    expect(JSON.parse(String(seen?.init.body))).toEqual({ events: batch });
  });

  it('uses sendBeacon with the key in the query while the page is closing', async () => {
    let beaconUrl = '';
    const send = createSender({
      endpoint: 'https://c.example.com',
      key: 'tk 1',
      beacon: (u) => ((beaconUrl = u), true),
      fetchFn: async () => {
        throw new Error('fetch must not be used');
      },
    });
    expect(await send(batch, { unloading: true })).toBe(true);
    expect(beaconUrl).toBe('https://c.example.com/v1/collect?k=tk%201');
  });

  it('drops the batch on a permanent 4xx but retries on 429, 5xx and network errors', async () => {
    const respond = (status: number) =>
      createSender({ endpoint: 'https://c', key: 'k', fetchFn: async () => new Response(null, { status }) });
    expect(await respond(401)(batch, { unloading: false })).toBe(true);
    expect(await respond(400)(batch, { unloading: false })).toBe(true);
    expect(await respond(429)(batch, { unloading: false })).toBe(false);
    expect(await respond(503)(batch, { unloading: false })).toBe(false);

    const failing = createSender({
      endpoint: 'https://c',
      key: 'k',
      fetchFn: async () => {
        throw new TypeError('network');
      },
    });
    expect(await failing(batch, { unloading: false })).toBe(false);
  });
});
