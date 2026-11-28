const PERSIAN_DIGITS = '۰۱۲۳۴۵۶۷۸۹';
const ARABIC_DIGITS = '٠١٢٣٤٥٦٧٨٩';
const E164_PHONE_REGEX = /^\+[1-9]\d{6,14}$/;
const TELEGRAM_USERNAME_REGEX = /^[a-zA-Z][a-zA-Z0-9_]{3,31}$/;

/**
 * Converts OpenAPI 3.1 numeric union values (`number | string`) emitted by ASP.NET Core
 * System.Text.Json schemas into a safe finite number.
 */
export function toNumber(value: number | string | null | undefined, fallback = 0): number {
  if (typeof value === 'number') {
    return Number.isFinite(value) ? value : fallback;
  }
  if (typeof value === 'string' && value.trim() !== '') {
    const parsed = Number(toLatinDigits(value));
    return Number.isFinite(parsed) ? parsed : fallback;
  }
  return fallback;
}

export function toLatinDigits(raw: string): string {
  let out = '';
  for (const ch of raw) {
    const faIndex = PERSIAN_DIGITS.indexOf(ch);
    if (faIndex >= 0) {
      out += String(faIndex);
      continue;
    }
    const arIndex = ARABIC_DIGITS.indexOf(ch);
    if (arIndex >= 0) {
      out += String(arIndex);
      continue;
    }
    out += ch;
  }
  return out;
}

export function formatNumber(
  value: number | string | null | undefined,
  language: string,
  options?: Intl.NumberFormatOptions,
): string {
  const numeric = toNumber(value);
  const locale = language.startsWith('fa') ? 'fa-IR' : 'en-US';
  return new Intl.NumberFormat(locale, options).format(numeric);
}

export function getCurrencyUnit(currency: string, language: string): string {
  const code = (currency || 'IRT').toUpperCase();
  const isFa = language.startsWith('fa');
  if (isFa) {
    switch (code) {
      case 'IRT':
        return 'تومان';
      case 'IRR':
        return 'ریال';
      case 'USD':
        return 'دلار';
      case 'EUR':
        return 'یورو';
      default:
        return code;
    }
  }
  if (code === 'IRT') {
    return 'Toman';
  }
  return code;
}

export function formatMoney(
  amount: number | string,
  currency: string,
  language: string,
): string {
  const numeric = toNumber(amount);
  const code = (currency || 'IRT').toUpperCase();
  const isFa = language.startsWith('fa');
  const isZeroDecimal = code === 'IRT' || code === 'IRR';

  if (isFa) {
    const formatted = new Intl.NumberFormat('fa-IR', {
      maximumFractionDigits: isZeroDecimal ? 0 : 2,
    }).format(numeric);
    return `${formatted} ${getCurrencyUnit(code, 'fa')}`;
  }

  if (code === 'IRT' || code === 'IRR') {
    const formatted = new Intl.NumberFormat('en-US', {
      maximumFractionDigits: 0,
    }).format(numeric);
    return `${formatted} ${getCurrencyUnit(code, 'en')}`;
  }

  try {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: code,
      maximumFractionDigits: 2,
    }).format(numeric);
  } catch {
    const formatted = new Intl.NumberFormat('en-US', {
      maximumFractionDigits: 2,
    }).format(numeric);
    return `${formatted} ${code}`;
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

/**
 * Normalises local Iranian phone numbers (e.g. `09121234567`, `۰۹۱۲...`, `0098912...`)
 * into canonical E.164 format (`+989121234567`) while keeping other international numbers intact.
 */
export function normalizePhoneToE164(raw: string): string {
  const latin = toLatinDigits(raw).trim();
  if (!latin) {
    return '';
  }

  const compact = latin.replace(/[\s\-().]/g, '');
  if (compact.startsWith('00')) {
    return `+${compact.slice(2)}`;
  }
  if (/^09\d{9}$/.test(compact)) {
    return `+98${compact.slice(1)}`;
  }
  if (/^9\d{9}$/.test(compact)) {
    return `+98${compact}`;
  }
  if (/^989\d{9}$/.test(compact)) {
    return `+${compact}`;
  }
  return compact;
}

export function isValidOptionalPhone(raw: string): boolean {
  const normalized = normalizePhoneToE164(raw);
  if (!normalized) {
    return true;
  }
  return E164_PHONE_REGEX.test(normalized);
}

export function normalizeTelegramUsername(raw: string): string {
  return raw.trim().replace(/^@+/, '');
}

export function isValidOptionalTelegramUsername(raw: string): boolean {
  const normalized = normalizeTelegramUsername(raw);
  if (!normalized) {
    return true;
  }
  return TELEGRAM_USERNAME_REGEX.test(normalized);
}

export function buildTelegramChatUrl(
  username: string | null | undefined,
  telegramUserId: number | string,
): string {
  const clean = username ? normalizeTelegramUsername(username) : '';
  if (clean) {
    return `https://t.me/${encodeURIComponent(clean)}`;
  }
  return `tg://user?id=${encodeURIComponent(String(telegramUserId))}`;
}
