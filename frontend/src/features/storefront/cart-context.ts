import { createContext, useContext } from 'react';
import type { CartItem } from './cart-store';

export interface StorefrontCartContextValue {
  shopSlug: string;
  items: CartItem[];
  totalCount: number;
  subtotal: number;
  idempotencyKey: string;
  addItem: (item: Omit<CartItem, 'quantity'>, delta?: number) => void;
  setQuantity: (productId: string, quantity: number) => void;
  removeItem: (productId: string) => void;
  clearCart: () => void;
  getQuantity: (productId: string) => number;
}

export const StorefrontCartContext = createContext<StorefrontCartContextValue | null>(null);

export function useStorefrontCart(): StorefrontCartContextValue {
  const value = useContext(StorefrontCartContext);
  if (!value) {
    throw new Error('useStorefrontCart must be used inside StorefrontCartProvider.');
  }
  return value;
}
