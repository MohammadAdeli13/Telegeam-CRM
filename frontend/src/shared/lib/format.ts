/**
 * Converts OpenAPI 3.1 numeric union values (`number | string`) emitted by ASP.NET Core
 * System.Text.Json schemas into a safe finite number.
 */
export function toNumber(value: number | string | null | undefined, fallback = 0): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : fallback;
  }
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }
  return fallback;
}

export function formatMoney(
  amount: number | string,
  currency: string,
  language: string,
): string {
  const numeric = toNumber(amount);
  const locale = language.startsWith('fa') ? 'fa-IR' : 'en-US';
  try {
    return new Intl.NumberFormat(locale, {
      style: 'currency',
      currency: currency || 'IRR',
      maximumFractionDigits: currency === 'IRR' ? 0 : 2,
    }).format(numeric);
  } catch {
    return `${new Intl.NumberFormat(locale).format(numeric)} ${currency}`;
  }
}

export function formatDate(isoDate: string, language: string): string {
  const date = new Date(isoDate);
  if (Number.isNaN(date.getTime())) {
    return isoDate;
  }
  const locale = language.startsWith('fa') ? 'fa-IR-u-ca-persian' : 'en-US';
  return new Intl.DateTimeFormat(locale, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  }).format(date);
}
