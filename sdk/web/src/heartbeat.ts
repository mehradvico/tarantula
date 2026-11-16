export const DEFAULT_HEARTBEAT_MS = 30_000;

export interface HeartbeatDeps {
  track: (name: string) => void;
  isVisible: () => boolean;
  setIntervalFn: (callback: () => void, ms: number) => unknown;
  clearIntervalFn: (handle: unknown) => void;
}

/**
 * Sends a `$heartbeat` while the page is open and visible, so someone who is only reading
 * still counts as online. A hidden tab sends nothing, so it stops counting after the live window ends.
 * Returns a function that stops it.
 */
export function startHeartbeat(deps: HeartbeatDeps, everyMs: number = DEFAULT_HEARTBEAT_MS): () => void {
  const handle = deps.setIntervalFn(() => {
    if (deps.isVisible()) deps.track('$heartbeat');
  }, everyMs);
  return () => deps.clearIntervalFn(handle);
}
