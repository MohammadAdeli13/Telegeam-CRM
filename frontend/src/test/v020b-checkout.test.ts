import { describe, expect, it } from 'vitest';
import { getNextOrderStatuses } from '@/features/merchant/merchant-state';
import {
  formatCardNumberDisplay,
  formatOrderCode,
  toLatinDigits,
} from '@/shared/lib/format';

describe('v0.2.0-b checkout, shipping, and payment helpers', () => {
  it('formats sequential order numbers in FA and EN and falls back to ID prefix', () => {
    const enCode = formatOrderCode('abcdef12-3456-7890-abcd-ef1234567890', 1001, 'en');
    expect(toLatinDigits(enCode)).toContain('1001');

    const faCode = formatOrderCode('abcdef12-3456-7890-abcd-ef1234567890', 1001, 'fa');
    expect(toLatinDigits(faCode)).toContain('1001');

    const fallbackCode = formatOrderCode('abcdef12-3456-7890-abcd-ef1234567890', 0, 'en');
    expect(fallbackCode.toUpperCase()).toContain('ABCDEF12');
  });

  it('formats 16-digit card numbers into readable groups and handles empty values', () => {
    expect(formatCardNumberDisplay(null)).toBe('');
    expect(formatCardNumberDisplay('')).toBe('');

    const formatted = formatCardNumberDisplay('۶۰۳۷۹۹۱۱۲۲۳۳۴۴۵۵');
    expect(toLatinDigits(formatted).replace(/\D/g, '')).toBe('6037991122334455');
    expect(formatted.length).toBeGreaterThan(16);
  });

  it('exposes valid merchant order state transitions including Shipped and terminal states', () => {
    expect(getNextOrderStatuses('Pending')).toEqual(['Confirmed', 'Cancelled']);
    expect(getNextOrderStatuses('Preparing')).toEqual(['Shipped', 'Cancelled']);
    expect(getNextOrderStatuses('Delivered')).toEqual([]);
    expect(getNextOrderStatuses('Cancelled')).toEqual([]);
  });
});
