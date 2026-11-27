import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { StorefrontCartContext } from './cart-context';
import {
  addCartItem,
  computeCartCount,
  computeCartSubtotal,
  createEmptyCartState,
  generateIdempotencyKey,
  loadCartState,
  removeCartItem,
  saveCartState,
  setCartItemQuantity,
  type CartItem,
  type PersistedCartState,
} from './cart-store';

export function StorefrontCartProvider({
  shopSlug,
  children,
}: {
  shopSlug: string;
  children: ReactNode;
}) {
  const [state, setState] = useState<PersistedCartState>(() => loadCartState(shopSlug));

  useEffect(() => {
    setState(loadCartState(shopSlug));
  }, [shopSlug]);

  useEffect(() => {
    saveCartState(shopSlug, state);
  }, [shopSlug, state]);

  const addItem = useCallback((item: Omit<CartItem, 'quantity'>, delta = 1) => {
    setState((prev) => ({
      items: addCartItem(prev.items, item, delta),
      idempotencyKey: generateIdempotencyKey(),
    }));
  }, []);

  const setQuantity = useCallback((productId: string, quantity: number) => {
    setState((prev) => ({
      items: setCartItemQuantity(prev.items, productId, quantity),
      idempotencyKey: generateIdempotencyKey(),
    }));
  }, []);

  const removeItem = useCallback((productId: string) => {
    setState((prev) => ({
      items: removeCartItem(prev.items, productId),
      idempotencyKey: generateIdempotencyKey(),
    }));
  }, []);

  const clearCart = useCallback(() => {
    setState(createEmptyCartState());
  }, []);

  const getQuantity = useCallback(
    (productId: string) => state.items.find((i) => i.productId === productId)?.quantity ?? 0,
    [state.items],
  );

  const value = useMemo(
    () => ({
      shopSlug,
      items: state.items,
      totalCount: computeCartCount(state.items),
      subtotal: computeCartSubtotal(state.items),
      idempotencyKey: state.idempotencyKey,
      addItem,
      setQuantity,
      removeItem,
      clearCart,
      getQuantity,
    }),
    [shopSlug, state, addItem, setQuantity, removeItem, clearCart, getQuantity],
  );

  return (
    <StorefrontCartContext.Provider value={value}>{children}</StorefrontCartContext.Provider>
  );
}
