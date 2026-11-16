import { randomId } from './ids';
import { EventQueue } from './queue';
import { SessionManager } from './session';
import { memoryStore } from './store';
import type { Clock, Consent, KeyValueStore, Sender, WireEvent } from './types';

const CONSENT_KEY = 'tk_consent';
const ANON_KEY = 'tk_anon';
const USER_KEY = 'tk_user';
const CONSENTS: readonly Consent[] = ['none', 'anonymous', 'analytics', 'full'];

export interface ClientDeps {
  store: KeyValueStore;
  now: Clock;
  sender: Sender;
  maxBatchSize?: number;
  initialConsent?: Consent;
  /** Current page address and referrer. Injected so the client does not touch the DOM. */
  page?: () => { url?: string; referrer?: string };
}

/**
 * The SDK core. Privacy rules live here:
 *  - 'none'      -> nothing is tracked, nothing is stored.
 *  - 'anonymous' -> ids exist only in memory and change on every page load.
 *  - 'analytics' -> a persistent anonymous id and session, but the User is never attached.
 *  - 'full'      -> the User and Vendor ids set with identify() are attached too.
 * The server applies the same rules again, so a modified SDK cannot bypass them.
 */
export class TarantulaClient {
  private consent: Consent;
  private userId: string | undefined;
  private vendorId: string | undefined;
  private readonly queue: EventQueue;
  private readonly persistedSession: SessionManager;
  private readonly volatileSession: SessionManager;
  private readonly volatileAnonymousId = randomId('a');

  constructor(private readonly deps: ClientDeps) {
    const stored = deps.store.get(CONSENT_KEY);
    this.consent = isConsent(stored) ? stored : (deps.initialConsent ?? 'none');
    this.queue = new EventQueue({ sender: deps.sender, maxBatchSize: deps.maxBatchSize ?? 20 });
    this.persistedSession = new SessionManager(deps.store, deps.now);
    this.volatileSession = new SessionManager(memoryStore(), deps.now);
    this.userId = deps.store.get(USER_KEY) ?? undefined;
  }

  getConsent(): Consent {
    return this.consent;
  }

  setConsent(next: Consent): void {
    this.consent = next;
    this.deps.store.set(CONSENT_KEY, next);
    if (next === 'none') {
      // Withdrawn consent: forget everything we hold about this device.
      this.queue.clear();
      this.deps.store.remove(ANON_KEY);
      this.deps.store.remove(USER_KEY);
      this.persistedSession.clear();
      this.userId = undefined;
      this.vendorId = undefined;
    }
  }

  /** Tells the SDK who the signed-in User (and optionally Vendor) is. Only sent with 'full' consent. */
  identify(userId: string, vendorId?: string): void {
    this.userId = userId;
    this.vendorId = vendorId;
    if (this.consent === 'full') this.deps.store.set(USER_KEY, userId);
  }

  /** Call on sign-out so the next person on this device is not linked to the previous User. */
  reset(): void {
    this.userId = undefined;
    this.vendorId = undefined;
    this.deps.store.remove(USER_KEY);
    this.deps.store.remove(ANON_KEY);
    this.persistedSession.clear();
  }

  track(name: string, properties?: Record<string, unknown>): void {
    if (this.consent === 'none') return;

    const persistent = this.consent !== 'anonymous';
    const page = this.deps.page?.() ?? {};
    const event: WireEvent = {
      name,
      timestamp: new Date(this.deps.now()).toISOString(),
      anonymousId: persistent ? this.persistentAnonymousId() : this.volatileAnonymousId,
      sessionId: (persistent ? this.persistedSession : this.volatileSession).touch(),
      consent: this.consent,
    };
    if (page.url) event.url = page.url;
    if (page.referrer) event.referrer = page.referrer;
    if (properties && this.consent !== 'anonymous') event.properties = properties;
    if (this.consent === 'full') {
      if (this.userId) event.userId = this.userId;
      if (this.vendorId) event.vendorId = this.vendorId;
    }
    this.queue.push(event);
  }

  flush(unloading = false): Promise<boolean> {
    return this.queue.flush(unloading);
  }

  get pending(): number {
    return this.queue.size;
  }

  private persistentAnonymousId(): string {
    let id = this.deps.store.get(ANON_KEY);
    if (!id) {
      id = randomId('a');
      this.deps.store.set(ANON_KEY, id);
    }
    return id;
  }
}

function isConsent(value: string | null): value is Consent {
  return value !== null && (CONSENTS as readonly string[]).includes(value);
}
