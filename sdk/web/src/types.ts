/** What the end user agreed to. Mirrors the server's ConsentLevel. */
export type Consent = 'none' | 'anonymous' | 'analytics' | 'full';

export interface AutocaptureOptions {
  pageviews?: boolean;
  clicks?: boolean;
  scroll?: boolean;
  /** Keeps readers counted as "online". On by default. */
  heartbeat?: boolean;
  heartbeatMs?: number;
}

export interface TarantulaOptions {
  /** Public write key of the project. */
  key: string;
  /** Base URL of the ingest API, for example https://collect.example.com */
  endpoint: string;
  /** Starting consent. Defaults to the stored choice, or 'none'. Nothing is tracked until the user agrees. */
  consent?: Consent;
  /** Keep query strings and fragments in collected addresses. Off by default: they often hold tokens or search text. */
  captureQuery?: boolean;
  flushIntervalMs?: number;
  maxBatchSize?: number;
  autocapture?: boolean | AutocaptureOptions;
}

/** One event exactly as the server's /v1/collect expects it. */
export interface WireEvent {
  name: string;
  timestamp: string;
  anonymousId: string;
  sessionId: string;
  userId?: string;
  vendorId?: string;
  url?: string;
  referrer?: string;
  consent: Consent;
  properties?: Record<string, unknown>;
}

export interface KeyValueStore {
  get(key: string): string | null;
  set(key: string, value: string): void;
  remove(key: string): void;
}

export type Clock = () => number;

/** Sends a batch. Resolves true when the batch is done (sent or permanently refused), false to retry later. */
export type Sender = (batch: WireEvent[], options: { unloading: boolean }) => Promise<boolean>;
