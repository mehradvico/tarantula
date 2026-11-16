import { describe, expect, it } from 'vitest';
import { type ElementLike, isRageClick, newScrollMilestones, selectorFor } from '../src/heatmap';

const el = (tagName: string, extra: Partial<ElementLike> = {}): ElementLike => ({ tagName, ...extra });

describe('selectorFor', () => {
  it('builds a short path and stops at an id', () => {
    const button = el('BUTTON', { id: 'buy', parentElement: el('DIV', { classList: ['card', 'big', 'x'], parentElement: el('MAIN') }) });
    expect(selectorFor(button)).toBe('button#buy');
    const inner = el('SPAN', { classList: ['label'], parentElement: el('DIV', { classList: ['card', 'big', 'x'], parentElement: el('MAIN') }) });
    expect(selectorFor(inner)).toBe('main > div.card.big > span.label');
  });

  it('limits the depth and strips unsafe characters', () => {
    const chain = [1, 2, 3, 4, 5, 6].reduce<ElementLike | null>((parent, i) => el('DIV', { classList: [`l${i}`], parentElement: parent }), null);
    expect(selectorFor(chain!).split(' > ')).toHaveLength(4);
    expect(selectorFor(el('A', { classList: ['we"ird<x>'] }))).toBe('a.weirdx');
  });
});

describe('isRageClick', () => {
  it('flags three quick clicks on the same spot', () => {
    const history = [
      { t: 0, x: 100, y: 100 },
      { t: 300, x: 102, y: 99 },
    ];
    expect(isRageClick(history, { t: 600, x: 101, y: 101 })).toBe(true);
  });

  it('ignores slow or distant clicks', () => {
    expect(isRageClick([{ t: 0, x: 100, y: 100 }, { t: 100, x: 100, y: 100 }], { t: 5000, x: 100, y: 100 })).toBe(false);
    expect(isRageClick([{ t: 0, x: 0, y: 0 }, { t: 100, x: 500, y: 500 }], { t: 200, x: 100, y: 100 })).toBe(false);
  });
});

describe('newScrollMilestones', () => {
  it('reports each milestone once', () => {
    const reached = new Set<number>();
    expect(newScrollMilestones(0, 500, 2000, reached)).toEqual([25]);
    expect(newScrollMilestones(0, 500, 2000, reached)).toEqual([]);
    expect(newScrollMilestones(500, 500, 2000, reached)).toEqual([50]);
    expect(newScrollMilestones(1500, 500, 2000, reached)).toEqual([75, 100]);
  });

  it('is safe on an empty document', () => {
    expect(newScrollMilestones(0, 500, 0, new Set())).toEqual([]);
  });
});
