/** The few element fields we need, so these functions can be tested without a browser. */
export interface ElementLike {
  tagName: string;
  id?: string;
  classList?: ArrayLike<string>;
  parentElement?: ElementLike | null;
}

const MAX_DEPTH = 4;
const MAX_CLASSES = 2;

/**
 * A short CSS-like path to the clicked element, for heatmaps: "main > div.card > button#buy".
 * It never includes text or attribute values, because those can contain personal data.
 */
export function selectorFor(element: ElementLike): string {
  const parts: string[] = [];
  let node: ElementLike | null | undefined = element;
  for (let depth = 0; node && depth < MAX_DEPTH; depth++) {
    const tag = node.tagName.toLowerCase();
    if (node.id) {
      parts.unshift(`${tag}#${cssSafe(node.id)}`);
      break; // an id is unique enough
    }
    const classes = Array.from(node.classList ?? [])
      .slice(0, MAX_CLASSES)
      .map(cssSafe)
      .filter(Boolean);
    parts.unshift(classes.length ? `${tag}.${classes.join('.')}` : tag);
    node = node.parentElement;
  }
  return parts.join(' > ');
}

function cssSafe(value: string): string {
  return value.replace(/[^A-Za-z0-9_-]/g, '').slice(0, 40);
}

export interface ClickPoint {
  t: number;
  x: number;
  y: number;
}

/** Three quick clicks on the same spot usually mean frustration: something looks clickable but is not. */
export function isRageClick(
  history: readonly ClickPoint[],
  click: ClickPoint,
  windowMs = 1000,
  radiusPx = 30,
  count = 3,
): boolean {
  const near = history.filter(
    (h) => click.t - h.t <= windowMs && Math.hypot(h.x - click.x, h.y - click.y) <= radiusPx,
  );
  return near.length + 1 >= count;
}

export const SCROLL_MILESTONES = [25, 50, 75, 100] as const;

/** Which scroll-depth milestones were newly passed. `reached` is updated in place. */
export function newScrollMilestones(
  scrollTop: number,
  viewportHeight: number,
  documentHeight: number,
  reached: Set<number>,
): number[] {
  if (documentHeight <= 0) return [];
  const percent = Math.min(100, Math.round(((scrollTop + viewportHeight) / documentHeight) * 100));
  const fresh = SCROLL_MILESTONES.filter((m) => percent >= m && !reached.has(m));
  fresh.forEach((m) => reached.add(m));
  return fresh;
}
