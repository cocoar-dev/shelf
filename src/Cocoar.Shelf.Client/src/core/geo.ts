// Country code → display name + flag emoji. Pure client-side formatting (Intl + Unicode regional
// indicators) — NOT analysis; all aggregation happens in the backend. The access log stores the
// two-letter ISO country code; the full name and flag are derived here for display only.

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

export function countryFlag(code: string | null | undefined): string {
  if (!code) return '🏳️';
  const cc = code.toUpperCase();
  if (!/^[A-Z]{2}$/.test(cc)) return '🏳️';
  // Two ASCII letters → the two regional-indicator symbols that render as a flag.
  return String.fromCodePoint(...[...cc].map(c => 0x1f1e6 + c.charCodeAt(0) - 65));
}
