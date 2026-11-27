import { describe, expect, it } from 'vitest';
import { clearSession, getAccessToken, getSession, setSession } from '@/shared/api/session';
import { extractShopSlugFromStartParam } from '@/shared/lib/telegram';

describe('session storage policy (Contract v1.2 §5.3)', () => {
  it('stores JWT in memory and sessionStorage only, never localStorage', () => {
    setSession({
      accessToken: 'jwt-test-token',
      expiresAt: '2026-12-01T00:00:00Z',
      user: {
        id: '00000000-0000-0000-0000-000000000001',
        telegramUserId: 123456,
        firstName: 'Amin',
        lastName: null,
        languageCode: 'fa',
      },
      tenants: [],
      activeTenant: null,
    });

    expect(getAccessToken()).toBe('jwt-test-token');
    expect(getSession()?.user.firstName).toBe('Amin');
    expect(window.sessionStorage.length).toBe(1);
    expect(window.localStorage.length).toBe(0);

    clearSession();
    expect(getAccessToken()).toBeNull();
    expect(window.sessionStorage.length).toBe(0);
  });

  it('extracts valid shop slugs from Telegram startapp deep links', () => {
    expect(extractShopSlugFromStartParam('shop_noor-gallery')).toBe('noor-gallery');
    expect(extractShopSlugFromStartParam('shop_INVALID SPACE')).toBeNull();
    expect(extractShopSlugFromStartParam('other_param')).toBeNull();
    expect(extractShopSlugFromStartParam(null)).toBeNull();
  });
});
