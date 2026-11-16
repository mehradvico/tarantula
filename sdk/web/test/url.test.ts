import { describe, expect, it } from 'vitest';
import { cleanUrl } from '../src/url';

describe('cleanUrl', () => {
  it('drops the query string and fragment by default', () => {
    expect(cleanUrl('https://app.example/pay?token=SECRET#step2', false)).toBe('https://app.example/pay');
    expect(cleanUrl('https://app.example/a#top', false)).toBe('https://app.example/a');
  });

  it('keeps an address without a query as it is', () => {
    expect(cleanUrl('https://app.example/store/product/12', false)).toBe('https://app.example/store/product/12');
  });

  it('keeps the query only when the site opts in', () => {
    expect(cleanUrl('https://app.example/s?q=dog', true)).toBe('https://app.example/s?q=dog');
  });

  it('passes undefined and empty values through', () => {
    expect(cleanUrl(undefined, false)).toBeUndefined();
    expect(cleanUrl('', false)).toBe('');
  });
});
