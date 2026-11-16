import { describe, expect, it } from 'vitest';
import { TarantulaClient } from '../src/client';
import { SessionManager } from '../src/session';
import { memoryStore } from '../src/store';
import type { Consent, Sender, WireEvent } from '../src/types';

function setup(consent?: Consent, store = memoryStore()) {
  const sent: WireEvent[] = [];
  let now = 1_700_000_000_000;
  const sender: Sender = async (batch) => {
    sent.push(...batch);
    return true;
  };
  const client = new TarantulaClient({
    store,
    now: () => now,
    sender,
    initialConsent: consent,
    page: () => ({ url: 'https://shop.example/cart?x=1', referrer: 'https://google.com/' }),
  });
  return { client, sent, store, advance: (ms: number) => (now += ms) };
}

describe('consent', () => {
  it('tracks nothing until the user agrees', async () => {
    const { client, sent } = setup();
    client.track('$pageview');
    await client.flush();
    expect(sent).toHaveLength(0);
    expect(client.getConsent()).toBe('none');
  });

  it('does not store any id while consent is none', () => {
    const { client, store } = setup('none');
    client.track('x');
    expect(store.get('tk_anon')).toBeNull();
    expect(store.get('tk_session')).toBeNull();
  });

  it('anonymous consent keeps ids in memory only and sends no custom properties', async () => {
    const { client, sent, store } = setup('anonymous');
    client.identify('user-1', 'vendor-1');
    client.track('buy', { sku: 'A1' });
    await client.flush();
    expect(store.get('tk_anon')).toBeNull();
    expect(store.get('tk_session')).toBeNull();
    expect(sent[0]?.userId).toBeUndefined();
    expect(sent[0]?.properties).toBeUndefined();
    expect(sent[0]?.consent).toBe('anonymous');
  });

  it('analytics consent persists the anonymous id but never attaches the User', async () => {
    const { client, sent, store } = setup('analytics');
    client.identify('user-1', 'vendor-1');
    client.track('buy', { sku: 'A1' });
    await client.flush();
    expect(store.get('tk_anon')).toMatch(/^a_/);
    expect(sent[0]?.userId).toBeUndefined();
    expect(sent[0]?.vendorId).toBeUndefined();
    expect(sent[0]?.properties).toEqual({ sku: 'A1' });
  });

  it('full consent attaches the User and Vendor', async () => {
    const { client, sent } = setup('full');
    client.identify('user-1', 'vendor-1');
    client.track('buy');
    await client.flush();
    expect(sent[0]).toMatchObject({ userId: 'user-1', vendorId: 'vendor-1', consent: 'full' });
  });

  it('withdrawing consent forgets the device and drops queued events', async () => {
    const { client, sent, store } = setup('full');
    client.identify('user-1');
    client.track('a');
    client.setConsent('none');
    await client.flush();
    expect(sent).toHaveLength(0);
    expect(store.get('tk_anon')).toBeNull();
    expect(store.get('tk_user')).toBeNull();
    expect(store.get('tk_session')).toBeNull();
  });

  it('remembers the stored consent choice on the next visit', () => {
    const store = memoryStore();
    setup(undefined, store).client.setConsent('full'); // the user's choice is stored; a default is not
    const next = setup(undefined, store);
    expect(next.client.getConsent()).toBe('full');
    expect(setup('full', memoryStore()).store.get('tk_consent')).toBeNull();
  });
});

describe('events', () => {
  it('reuses the anonymous id and fills page fields', async () => {
    const { client, sent } = setup('full');
    client.track('a');
    client.track('b');
    await client.flush();
    expect(sent[0]?.anonymousId).toBe(sent[1]?.anonymousId);
    expect(sent[0]).toMatchObject({ url: 'https://shop.example/cart?x=1', referrer: 'https://google.com/' });
    expect(new Date(sent[0]!.timestamp).getTime()).toBe(1_700_000_000_000);
  });

  it('reset() unlinks the previous User on sign-out', async () => {
    const { client, sent } = setup('full');
    client.identify('user-1');
    client.track('a');
    client.reset();
    client.track('b');
    await client.flush();
    expect(sent[0]?.userId).toBe('user-1');
    expect(sent[1]?.userId).toBeUndefined();
    expect(sent[1]?.anonymousId).not.toBe(sent[0]?.anonymousId);
  });
});

describe('sessions', () => {
  it('keeps a session while the user is active and renews it after 30 minutes of silence', () => {
    const store = memoryStore();
    let now = 0;
    let n = 0;
    const sessions = new SessionManager(store, () => now, 30 * 60 * 1000, () => `s${++n}`);
    const first = sessions.touch();
    now += 29 * 60 * 1000;
    expect(sessions.touch()).toBe(first);
    now += 31 * 60 * 1000;
    expect(sessions.touch()).not.toBe(first);
  });

  it('survives a corrupted stored value', () => {
    const store = memoryStore();
    store.set('tk_session', '{broken');
    expect(new SessionManager(store, () => 0).touch()).toMatch(/^s_/);
  });
});
