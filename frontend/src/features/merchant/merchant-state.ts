import type { OrderStatusValue } from '@/shared/ui/status-badge';

const ALLOWED_TRANSITIONS: Record<OrderStatusValue, readonly OrderStatusValue[]> = {
  Pending: ['Confirmed', 'Cancelled'],
  Confirmed: ['Preparing', 'Cancelled'],
  Preparing: ['Shipped', 'Cancelled'],
  Shipped: ['Delivered', 'Cancelled'],
  Delivered: [],
  Cancelled: [],
};

export const ORDER_STATUS_LIST: readonly OrderStatusValue[] = [
  'Pending',
  'Confirmed',
  'Preparing',
  'Shipped',
  'Delivered',
  'Cancelled',
];

export function getNextOrderStatuses(currentStatus: string): readonly OrderStatusValue[] {
  if (currentStatus in ALLOWED_TRANSITIONS) {
    return ALLOWED_TRANSITIONS[currentStatus as OrderStatusValue];
  }
  return [];
}

export function slugifyCategoryName(input: string): string {
  return input
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9-]+/g, '-')
    .replace(/-{2,}/g, '-')
    .replace(/^-|-$/g, '')
    .slice(0, 40);
}

export function isValidJsonText(raw: string): boolean {
  if (!raw.trim()) {
    return false;
  }
  try {
    JSON.parse(raw);
    return true;
  } catch {
    return false;
  }
}
