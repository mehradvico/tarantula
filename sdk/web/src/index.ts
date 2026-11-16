import { installAutocapture } from './autocapture';
import { TarantulaClient } from './client';
import { browserStore } from './store';
import { createSender } from './transport';
import { cleanUrl } from './url';
import type { AutocaptureOptions, Consent, TarantulaOptions } from './types';

export type { Consent, TarantulaOptions } from './types';

let client: TarantulaClient | null = null;
let timer: ReturnType<typeof setInterval> | undefined;
let uninstall: (() => void) | undefined;

function current(): TarantulaClient | null {
  return client;
}

export const tarantula = {
  /** Starts the SDK. Nothing is tracked until the user has given consent (see setConsent). */
  init(options: TarantulaOptions): void {
    if (client) return;
    const c = new TarantulaClient({
      store: browserStore(),
      now: () => Date.now(),
      sender: createSender({ endpoint: options.endpoint, key: options.key }),
      maxBatchSize: options.maxBatchSize,
      initialConsent: options.consent,
      page: () => ({
        url: cleanUrl(globalThis.location?.href, options.captureQuery === true),
        referrer: cleanUrl(globalThis.document?.referrer || undefined, false),
      }),
    });
    client = c;

    timer = setInterval(() => void c.flush(), options.flushIntervalMs ?? 5000);

    const auto = options.autocapture ?? true;
    if (auto && typeof window !== 'undefined') {
      uninstall = installAutocapture(c, auto === true ? ({} as AutocaptureOptions) : auto);
    }
  },

  track(name: string, properties?: Record<string, unknown>): void {
    current()?.track(name, properties);
  },

  /** Link this device to a signed-in User (and optionally a Vendor). Only used with 'full' consent. */
  identify(userId: string, vendorId?: string): void {
    current()?.identify(userId, vendorId);
  },

  setConsent(consent: Consent): void {
    current()?.setConsent(consent);
  },

  getConsent(): Consent {
    return current()?.getConsent() ?? 'none';
  },

  /** Call on sign-out. */
  reset(): void {
    current()?.reset();
  },

  flush(): Promise<boolean> {
    return current()?.flush() ?? Promise.resolve(true);
  },

  /** Stops timers and listeners. Mostly for tests and single-page app teardown. */
  shutdown(): void {
    if (timer) clearInterval(timer);
    uninstall?.();
    timer = undefined;
    uninstall = undefined;
    client = null;
  },
};

if (typeof window !== 'undefined') {
  (window as unknown as { tarantula: typeof tarantula }).tarantula = tarantula;
}

export default tarantula;
