import { describe, expect, it } from 'vitest';
import en from '@/locales/en.json';
import fa from '@/locales/fa.json';

function flattenKeys(obj: Record<string, unknown>, prefix = ''): string[] {
  const keys: string[] = [];
  for (const [key, value] of Object.entries(obj)) {
    const full = prefix ? `${prefix}.${key}` : key;
    if (value && typeof value === 'object' && !Array.isArray(value)) {
      keys.push(...flattenKeys(value as Record<string, unknown>, full));
    } else {
      keys.push(full);
    }
  }
  return keys.sort();
}

describe('i18n locale parity', () => {
  it('maintains 100% key parity between fa.json and en.json', () => {
    const faKeys = flattenKeys(fa);
    const enKeys = flattenKeys(en);
    expect(faKeys).toEqual(enKeys);
    expect(faKeys.length).toBeGreaterThan(20);
  });
});
