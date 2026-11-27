export interface CartItem {
  productId: string;
  name: string;
  nameEn: string | null;
  unitPrice: number;
  stock: number;
  imageUrl: string | null;
  quantity: number;
}

export interface PersistedCartState {
  items: CartItem[];
  idempotencyKey: string;
}

const E164_PHONE_REGEX = /^\+[1-9]\d{6,14}$/;

export function isValidOptionalE164Phone(raw: string): boolean {
  const trimmed = raw.trim();
  if (!trimmed) {
    return true;
  }
  return E164_PHONE_REGEX.test(trimmed);
}

export function generateIdempotencyKey(): string {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return `ord-${crypto.randomUUID()}`;
  }
  return `ord-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 12)}`;
}

function cartStorageKey(shopSlug: string): string {
  return `telegram-crm:cart:${shopSlug.toLowerCase()}`;
}

export function createEmptyCartState(): PersistedCartState {
  return {
    items: [],
    idempotencyKey: generateIdempotencyKey(),
  };
}

export function loadCartState(shopSlug: string): PersistedCartState {
  if (typeof window === 'undefined' || !shopSlug) {
    return createEmptyCartState();
  }
  try {
    const raw = window.sessionStorage.getItem(cartStorageKey(shopSlug));
    if (!raw) {
      return createEmptyCartState();
    }
    const parsed: unknown = JSON.parse(raw);
    if (!parsed || typeof parsed !== 'object') {
      return createEmptyCartState();
    }
    const obj = parsed as Record<string, unknown>;
    if (!Array.isArray(obj.items) || typeof obj.idempotencyKey !== 'string') {
      return createEmptyCartState();
    }
    const validItems = obj.items.filter(
      (item): item is CartItem =>
        Boolean(item) &&
        typeof item === 'object' &&
        typeof (item as CartItem).productId === 'string' &&
        typeof (item as CartItem).name === 'string' &&
        typeof (item as CartItem).unitPrice === 'number' &&
        typeof (item as CartItem).stock === 'number' &&
        typeof (item as CartItem).quantity === 'number' &&
        (item as CartItem).quantity > 0,
    );
    return {
      items: validItems,
      idempotencyKey: obj.idempotencyKey || generateIdempotencyKey(),
    };
  } catch {
    return createEmptyCartState();
  }
}

export function saveCartState(shopSlug: string, state: PersistedCartState): void {
  if (typeof window === 'undefined' || !shopSlug) {
    return;
  }
  try {
    if (state.items.length === 0) {
      window.sessionStorage.removeItem(cartStorageKey(shopSlug));
    } else {
      window.sessionStorage.setItem(cartStorageKey(shopSlug), JSON.stringify(state));
    }
  } catch {
    // Ignore restricted WebView storage errors; in-memory state stays active.
  }
}

export function addCartItem(
  items: readonly CartItem[],
  incoming: Omit<CartItem, 'quantity'>,
  delta = 1,
): CartItem[] {
  if (delta <= 0 || incoming.stock <= 0) {
    return [...items];
  }
  const existing = items.find((item) => item.productId === incoming.productId);
  if (!existing) {
    const clamped = Math.min(delta, incoming.stock, 1000);
    return [...items, { ...incoming, quantity: clamped }];
  }
  const maxAllowed = Math.min(incoming.stock, 1000);
  const nextQty = Math.min(existing.quantity + delta, maxAllowed);
  return items.map((item) =>
    item.productId === incoming.productId
      ? { ...incoming, quantity: Math.max(1, nextQty) }
      : item,
  );
}

export function setCartItemQuantity(
  items: readonly CartItem[],
  productId: string,
  nextQuantity: number,
): CartItem[] {
  if (nextQuantity <= 0) {
    return items.filter((item) => item.productId !== productId);
  }
  return items.map((item) => {
    if (item.productId !== productId) {
      return item;
    }
    const maxAllowed = Math.max(1, Math.min(item.stock > 0 ? item.stock : nextQuantity, 1000));
    return { ...item, quantity: Math.min(nextQuantity, maxAllowed) };
  });
}

export function removeCartItem(items: readonly CartItem[], productId: string): CartItem[] {
  return items.filter((item) => item.productId !== productId);
}

export function computeCartCount(items: readonly CartItem[]): number {
  return items.reduce((sum, item) => sum + item.quantity, 0);
}

export function computeCartSubtotal(items: readonly CartItem[]): number {
  return items.reduce((sum, item) => sum + item.unitPrice * item.quantity, 0);
}
