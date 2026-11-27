import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { useStorefrontCart } from '@/features/storefront/cart-context';
import {
  addCartItem,
  computeCartCount,
  computeCartSubtotal,
  isValidOptionalE164Phone,
  removeCartItem,
  setCartItemQuantity,
} from '@/features/storefront/cart-store';
import { StorefrontCartProvider } from '@/features/storefront/StorefrontCartProvider';

const sampleProduct = {
  productId: '00000000-0000-0000-0000-000000000101',
  name: 'قهوه اسپرسو',
  nameEn: 'Espresso',
  unitPrice: 120,
  stock: 3,
  imageUrl: null,
};

function CartHarness() {
  const { items, totalCount, subtotal, idempotencyKey, addItem, clearCart } = useStorefrontCart();
  return (
    <div>
      <span data-testid="count">{totalCount}</span>
      <span data-testid="subtotal">{subtotal}</span>
      <span data-testid="idem">{idempotencyKey}</span>
      <span data-testid="lines">{items.length}</span>
      <button type="button" onClick={() => addItem(sampleProduct, 1)}>
        Add
      </button>
      <button type="button" onClick={clearCart}>
        Clear
      </button>
    </div>
  );
}

describe('P4b storefront cart logic and idempotency key lifecycle', () => {
  it('adds, clamps to available stock, updates, and removes items accurately', () => {
    let items = addCartItem([], sampleProduct, 2);
    expect(computeCartCount(items)).toBe(2);
    expect(computeCartSubtotal(items)).toBe(240);

    // Adding 5 more clamps at available stock (3)
    items = addCartItem(items, sampleProduct, 5);
    expect(computeCartCount(items)).toBe(3);

    // Setting quantity to 0 removes the item
    items = setCartItemQuantity(items, sampleProduct.productId, 0);
    expect(items).toHaveLength(0);

    // Remove non-existent item is a safe no-op
    expect(removeCartItem(items, 'missing')).toEqual([]);
  });

  it('validates optional E.164 phone numbers', () => {
    expect(isValidOptionalE164Phone('')).toBe(true);
    expect(isValidOptionalE164Phone('+989121234567')).toBe(true);
    expect(isValidOptionalE164Phone('09121234567')).toBe(false);
    expect(isValidOptionalE164Phone('+0123')).toBe(false);
  });

  it('persists cart in sessionStorage only (never localStorage) and rotates Idempotency-Key on mutation', async () => {
    const user = userEvent.setup();
    render(
      <StorefrontCartProvider shopSlug="noor-gallery">
        <CartHarness />
      </StorefrontCartProvider>,
    );

    const initialKey = screen.getByTestId('idem').textContent;
    expect(initialKey).toMatch(/^ord-/);
    expect(window.localStorage.length).toBe(0);

    await user.click(screen.getByRole('button', { name: 'Add' }));
    expect(screen.getByTestId('count')).toHaveTextContent('1');
    expect(screen.getByTestId('subtotal')).toHaveTextContent('120');

    const keyAfterAdd = screen.getByTestId('idem').textContent;
    expect(keyAfterAdd).not.toBe(initialKey);
    expect(window.sessionStorage.getItem('telegram-crm:cart:noor-gallery')).toContain(
      sampleProduct.productId,
    );
    expect(window.localStorage.length).toBe(0);

    await user.click(screen.getByRole('button', { name: 'Clear' }));
    expect(screen.getByTestId('count')).toHaveTextContent('0');
    expect(window.sessionStorage.getItem('telegram-crm:cart:noor-gallery')).toBeNull();
  });
});
