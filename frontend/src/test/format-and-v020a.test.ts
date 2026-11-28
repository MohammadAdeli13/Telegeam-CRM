import { describe, expect, it } from 'vitest';
import {
  buildTelegramChatUrl,
  formatMoney,
  formatNumber,
  isValidOptionalPhone,
  isValidOptionalTelegramUsername,
  normalizePhoneToE164,
  normalizeTelegramUsername,
} from '@/shared/lib/format';

describe('v0.2.0-a formatting, currency (IRT/IRR), phone & Telegram chat helpers', () => {
  it('formats Persian Toman and Rial with number first and Persian digits', () => {
    expect(formatMoney(150000, 'IRT', 'fa')).toBe('۱۵۰٬۰۰۰ تومان');
    expect(formatMoney(150000, 'IRR', 'fa')).toBe('۱۵۰٬۰۰۰ ریال');
    expect(formatMoney(150000, 'IRT', 'en')).toBe('150,000 Toman');
    expect(formatMoney(49.5, 'USD', 'en')).toBe('$49.50');
  });

  it('formats numbers cleanly in Persian and English', () => {
    expect(formatNumber(25, 'fa')).toBe('۲۵');
    expect(formatNumber(25, 'en')).toBe('25');
  });

  it('normalizes local Iranian mobile numbers and Persian digits to E.164', () => {
    expect(normalizePhoneToE164('09121234567')).toBe('+989121234567');
    expect(normalizePhoneToE164('۰۹۱۲-۱۲۳-۴۵۶۷')).toBe('+989121234567');
    expect(normalizePhoneToE164('00989121234567')).toBe('+989121234567');
    expect(normalizePhoneToE164('+989121234567')).toBe('+989121234567');

    expect(isValidOptionalPhone('')).toBe(true);
    expect(isValidOptionalPhone('09121234567')).toBe(true);
    expect(isValidOptionalPhone('۰۹۱۲۱۲۳۴۵۶۷')).toBe(true);
    expect(isValidOptionalPhone('123')).toBe(false);
  });

  it('normalizes Telegram usernames and builds direct chat URLs', () => {
    expect(normalizeTelegramUsername('@sara_shop')).toBe('sara_shop');
    expect(isValidOptionalTelegramUsername('@sara_shop')).toBe(true);
    expect(isValidOptionalTelegramUsername('123bad')).toBe(false);

    expect(buildTelegramChatUrl('@sara_shop', 700001)).toBe('https://t.me/sara_shop');
    expect(buildTelegramChatUrl(null, 700001)).toBe('tg://user?id=700001');
  });
});
