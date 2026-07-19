// Country code → display name + SVG flag class. Pure client-side formatting — NOT analysis; all
// aggregation happens in the backend. The access log stores the two-letter ISO country code; the
// full name and flag are derived here for display only.
//
// NOTE: emoji flags (🇦🇹) are used deliberately NOWHERE — Windows does not render regional-indicator
// flag emoji (it shows the bare letters instead). We use flag-icons (SVG) via a CSS class instead.

const regionNames = typeof Intl !== 'undefined' && 'DisplayNames' in Intl
  ? new Intl.DisplayNames(['en'], { type: 'region' })
  : null;

export function countryName(code: string | null | undefined): string {
  if (!code) return 'Unknown';
  const cc = code.toUpperCase();
  try {
    return regionNames?.of(cc) ?? cc;
  } catch {
    return cc;
  }
}

/** flag-icons CSS class for a country code, e.g. "at" → "fi fi-at". Empty for an invalid code. */
export function flagClass(code: string | null | undefined): string {
  if (!code || !/^[A-Za-z]{2}$/.test(code)) return '';
  return `fi fi-${code.toLowerCase()}`;
}
