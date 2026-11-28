import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import {
  getNextOrderStatuses,
  isValidJsonText,
  parseSettingDisplayValue,
  serializeSettingValue,
  slugifyCategoryName,
} from '@/features/merchant/merchant-state';
import '@/shared/lib/i18n';
import { setAppLanguage } from '@/shared/lib/i18n';
import { OrderStatusBadge } from '@/shared/ui/status-badge';

describe('merchant state machine and settings helpers', () => {
  it('enforces exact legal order status transitions matching backend domain', () => {
    expect(getNextOrderStatuses('Pending')).toEqual(['Confirmed', 'Cancelled']);
    expect(getNextOrderStatuses('Confirmed')).toEqual(['Preparing', 'Cancelled']);
    expect(getNextOrderStatuses('Preparing')).toEqual(['Shipped', 'Cancelled']);
    expect(getNextOrderStatuses('Shipped')).toEqual(['Delivered', 'Cancelled']);
    expect(getNextOrderStatuses('Delivered')).toEqual([]);
    expect(getNextOrderStatuses('Cancelled')).toEqual([]);
    expect(getNextOrderStatuses('Unknown')).toEqual([]);
  });

  it('validates JSON setting values strictly', () => {
    expect(isValidJsonText('"hello"')).toBe(true);
    expect(isValidJsonText('{"enabled":true}')).toBe(true);
    expect(isValidJsonText('42')).toBe(true);
    expect(isValidJsonText('{broken')).toBe(false);
    expect(isValidJsonText('')).toBe(false);
  });

  it('parses and serializes human-friendly shop settings safely as JSON', () => {
    expect(parseSettingDisplayValue('"@noor_support"')).toBe('@noor_support');
    expect(parseSettingDisplayValue('true')).toBe('true');
    expect(serializeSettingValue('@noor_support')).toBe('"@noor_support"');
    expect(serializeSettingValue('{"a":1}', { preserveStructuredJson: true })).toBe('{"a":1}');
    expect(serializeSettingValue('plain text', { preserveStructuredJson: true })).toBe(
      '"plain text"',
    );
  });

  it('slugifies category names cleanly', () => {
    expect(slugifyCategoryName('  Hot Coffee & Tea!  ')).toBe('hot-coffee-tea');
  });

  it('renders localized OrderStatusBadge in both FA and EN', () => {
    setAppLanguage('fa');
    const { rerender } = render(<OrderStatusBadge status="Pending" />);
    expect(screen.getByText('در انتظار تأیید')).toBeInTheDocument();

    setAppLanguage('en');
    rerender(<OrderStatusBadge status="Shipped" />);
    expect(screen.getByText('Shipped')).toBeInTheDocument();
  });
});
