import { describe, expect, it } from 'vitest';
import {
  clearSession,
  getAccessToken,
  getSession,
  patchActiveTenantInSession,
  setSession,
} from '@/shared/api/session';
import { extractShopSlugFromStartParam } from '@/shared/lib/telegram';

describe('session storage policy (Contract v1.2 §5.3)', () => {
  it('stores JWT in memory and sessionStorage only, never localStorage', () => {
    setSession({
      accessToken: 'jwt-test-token',
      expiresAt: '2026-12-01T00:00:00Z',
      user: {
        id: '00000000-0000-0000-0000-000000000001',
        telegramUserId: 123456,
        username: 'amin_dev',
        firstName: 'Amin',
        lastName: null,
        languageCode: 'fa',
      },
      tenants: [
        {
          id: '00000000-0000-0000-0000-000000000010',
          slug: 'noor-gallery',
          name: 'گالری نور',
          role: 'MerchantOwner',
          defaultLanguage: 'fa',
          currency: 'IRT',
          logoUrl: null,
          defaultProductImageUrl: null,
        },
      ],
      activeTenant: {
        id: '00000000-0000-0000-0000-000000000010',
        slug: 'noor-gallery',
        name: 'گالری نور',
        role: 'MerchantOwner',
        defaultLanguage: 'fa',
        currency: 'IRT',
        logoUrl: null,
        defaultProductImageUrl: null,
      },
    });

    expect(getAccessToken()).toBe('jwt-test-token');
    expect(getSession()?.user.username).toBe('amin_dev');
    expect(window.sessionStorage.length).toBe(1);
    expect(window.localStorage.length).toBe(0);

    const patched = patchActiveTenantInSession({
      currency: 'USD',
      logoUrl: '/files/logo.png',
    });
    expect(patched?.activeTenant?.currency).toBe('USD');
    expect(patched?.activeTenant?.logoUrl).toBe('/files/logo.png');
    expect(patched?.tenants[0]?.currency).toBe('USD');
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
