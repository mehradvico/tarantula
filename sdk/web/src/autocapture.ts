import type { TarantulaClient } from './client';
import { type ClickPoint, type ElementLike, isRageClick, newScrollMilestones, selectorFor } from './heatmap';
import { startHeartbeat } from './heartbeat';
import type { AutocaptureOptions } from './types';

const SCROLL_THROTTLE_MS = 250;

/** Hooks page views, clicks (for heatmaps), scroll depth and page-leave flushing. Returns a function that removes them. */
export function installAutocapture(
  client: TarantulaClient,
  options: AutocaptureOptions,
  win: Window = window,
): () => void {
  const doc = win.document;
  const cleanups: Array<() => void> = [];

  if (options.pageviews !== false) {
    const view = () => client.track('$pageview', { path: win.location.pathname });
    view();

    // Single-page apps change the address without reloading, so hook the History API too.
    const history = win.history;
    const push = history.pushState.bind(history);
    const replace = history.replaceState.bind(history);
    history.pushState = (...args) => {
      push(...args);
      view();
    };
    history.replaceState = (...args) => {
      replace(...args);
    };
    const onPop = () => view();
    win.addEventListener('popstate', onPop);
    cleanups.push(() => {
      history.pushState = push;
      history.replaceState = replace;
      win.removeEventListener('popstate', onPop);
    });
  }

  if (options.clicks !== false) {
    const recent: ClickPoint[] = [];
    const onClick = (e: MouseEvent) => {
      const target = e.target as Element | null;
      if (!target || typeof target.tagName !== 'string') return;
      const point: ClickPoint = { t: Date.now(), x: e.pageX, y: e.pageY };
      const root = doc.documentElement;
      const props = {
        selector: selectorFor(target as unknown as ElementLike),
        x: Math.round(point.x),
        y: Math.round(point.y),
        pageWidth: root.scrollWidth,
        pageHeight: root.scrollHeight,
        viewportWidth: win.innerWidth,
        viewportHeight: win.innerHeight,
        path: win.location.pathname,
      };
      client.track('$click', props);
      if (isRageClick(recent, point)) client.track('$rageclick', props);
      recent.push(point);
      while (recent.length > 10) recent.shift();
    };
    doc.addEventListener('click', onClick, { capture: true, passive: true });
    cleanups.push(() => doc.removeEventListener('click', onClick, { capture: true }));
  }

  if (options.scroll !== false) {
    let reached = new Set<number>();
    let last = 0;
    const onScroll = () => {
      const now = Date.now();
      if (now - last < SCROLL_THROTTLE_MS) return;
      last = now;
      const root = doc.documentElement;
      for (const depth of newScrollMilestones(win.scrollY, win.innerHeight, root.scrollHeight, reached)) {
        client.track('$scroll', { depth, path: win.location.pathname });
      }
    };
    const onNavigate = () => {
      reached = new Set();
    };
    win.addEventListener('scroll', onScroll, { passive: true });
    win.addEventListener('popstate', onNavigate);
    cleanups.push(() => {
      win.removeEventListener('scroll', onScroll);
      win.removeEventListener('popstate', onNavigate);
    });
  }

  if (options.heartbeat !== false) {
    cleanups.push(
      startHeartbeat(
        {
          track: (name) => client.track(name),
          isVisible: () => doc.visibilityState === 'visible',
          setIntervalFn: (cb, ms) => win.setInterval(cb, ms),
          clearIntervalFn: (h) => win.clearInterval(h as number),
        },
        options.heartbeatMs,
      ),
    );
  }

  // Send what is queued when the page is hidden or closed; sendBeacon survives unloading.
  const onHide = () => {
    if (doc.visibilityState === 'hidden') void client.flush(true);
  };
  const onPageHide = () => void client.flush(true);
  doc.addEventListener('visibilitychange', onHide);
  win.addEventListener('pagehide', onPageHide);
  cleanups.push(() => {
    doc.removeEventListener('visibilitychange', onHide);
    win.removeEventListener('pagehide', onPageHide);
  });

  return () => cleanups.forEach((fn) => fn());
}
