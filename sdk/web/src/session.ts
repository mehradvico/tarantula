import { randomId } from './ids';
import type { Clock, KeyValueStore } from './types';

const KEY = 'tk_session';
export const SESSION_TIMEOUT_MS = 30 * 60 * 1000;

interface Stored {
  id: string;
  last: number;
}

/** A session continues while the user stays active; after 30 minutes of silence a new one starts. */
export class SessionManager {
  constructor(
    private readonly store: KeyValueStore,
    private readonly now: Clock,
    private readonly timeoutMs: number = SESSION_TIMEOUT_MS,
    private readonly newId: () => string = () => randomId('s'),
  ) {}

  /** Marks activity and returns the current session id. */
  touch(): string {
    const now = this.now();
    const current = this.read();
    const id = current && now - current.last <= this.timeoutMs ? current.id : this.newId();
    this.store.set(KEY, JSON.stringify({ id, last: now } satisfies Stored));
    return id;
  }

  clear(): void {
    this.store.remove(KEY);
  }

  private read(): Stored | null {
    const raw = this.store.get(KEY);
    if (!raw) return null;
    try {
      const parsed = JSON.parse(raw) as Partial<Stored>;
      return typeof parsed.id === 'string' && typeof parsed.last === 'number' ? { id: parsed.id, last: parsed.last } : null;
    } catch {
      return null;
    }
  }
}
