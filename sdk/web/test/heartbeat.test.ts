import { describe, expect, it } from 'vitest';
import { DEFAULT_HEARTBEAT_MS, startHeartbeat } from '../src/heartbeat';

function fakeTimers() {
  let tick: (() => void) | undefined;
  let every = 0;
  let cleared = false;
  return {
    setIntervalFn: (cb: () => void, ms: number) => {
      tick = cb;
      every = ms;
      return 1;
    },
    clearIntervalFn: () => (cleared = true),
    fire: () => tick?.(),
    get every() {
      return every;
    },
    get cleared() {
      return cleared;
    },
  };
}

describe('startHeartbeat', () => {
  it('beats every 30 seconds by default', () => {
    const t = fakeTimers();
    startHeartbeat({ track: () => {}, isVisible: () => true, ...t });
    expect(t.every).toBe(DEFAULT_HEARTBEAT_MS);
  });

  it('sends $heartbeat only while the page is visible', () => {
    const t = fakeTimers();
    const sent: string[] = [];
    let visible = true;
    startHeartbeat({ track: (n) => sent.push(n), isVisible: () => visible, ...t });
    t.fire();
    visible = false;
    t.fire();
    visible = true;
    t.fire();
    expect(sent).toEqual(['$heartbeat', '$heartbeat']);
  });

  it('can be stopped', () => {
    const t = fakeTimers();
    const stop = startHeartbeat({ track: () => {}, isVisible: () => true, ...t });
    stop();
    expect(t.cleared).toBe(true);
  });
});
