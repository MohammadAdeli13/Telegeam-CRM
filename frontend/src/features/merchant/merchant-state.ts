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

/**
 * Unwraps JSON string values into plain text for friendly inputs while preserving
 * structured JSON (objects, arrays, numbers, booleans) as readable text.
 */
export function parseSettingDisplayValue(rawJson: string): string {
  try {
    const parsed: unknown = JSON.parse(rawJson);
    if (typeof parsed === 'string') {
      return parsed;
    }
    if (parsed === null || parsed === undefined) {
      return '';
    }
    return JSON.stringify(parsed);
  } catch {
    return rawJson;
  }
}

/**
 * Converts a friendly plain-text input into a valid JSON document for the backend.
 * Plain text is always serialized safely as a JSON string unless `preserveStructuredJson`
 * is enabled and the user supplied an explicit JSON object/array/boolean/number.
 */
export function serializeSettingValue(
  input: string,
  options?: { preserveStructuredJson?: boolean },
): string {
  const trimmed = input.trim();
  if (options?.preserveStructuredJson && trimmed.length > 0) {
    const first = trimmed[0];
    if (
      first === '{' ||
      first === '[' ||
      trimmed === 'true' ||
      trimmed === 'false' ||
      /^-?\d+(?:\.\d+)?$/.test(trimmed)
    ) {
      try {
        JSON.parse(trimmed);
        return trimmed;
      } catch {
        // Fall through to string serialization.
      }
    }
  }
  return JSON.stringify(trimmed);
}
