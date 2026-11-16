/**
 * Removes the query string and fragment from an address. Query strings often carry tokens, codes and
 * search text, so they are not collected unless the site opts in with `captureQuery: true`.
 */
export function cleanUrl(href: string | undefined, keepQuery: boolean): string | undefined {
  if (!href) return href;
  if (keepQuery) return href;
  const cut = href.search(/[?#]/);
  return cut < 0 ? href : href.slice(0, cut);
}
