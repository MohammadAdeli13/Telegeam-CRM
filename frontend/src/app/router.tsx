import { useEffect } from 'react';
import { createBrowserRouter, Navigate, useNavigate } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { AuthPage } from '@/features/auth/AuthPage';
import { OnboardingPage } from '@/features/auth/OnboardingPage';
import { MerchantLayout } from '@/features/merchant/MerchantLayout';
import {
  MerchantDashboardPage,
  MerchantSectionPlaceholder,
} from '@/features/merchant/MerchantPages';
import { StorefrontLayout } from '@/features/storefront/StorefrontLayout';
import {
  StorefrontCartPlaceholder,
  StorefrontCatalogPage,
  StorefrontOrdersPlaceholder,
} from '@/features/storefront/StorefrontPages';
import { extractShopSlugFromStartParam } from '@/shared/lib/telegram';
import { LoadingState } from '@/shared/ui/state-views';

function EntryRedirect() {
  const navigate = useNavigate();
  const { status, session, telegram } = useAuth();

  useEffect(() => {
    const deepLinkSlug = extractShopSlugFromStartParam(telegram.startParam);
    if (deepLinkSlug) {
      void navigate(`/s/${deepLinkSlug}`, { replace: true });
      return;
    }

    if (status === 'bootstrapping') {
      return;
    }

    if (status === 'authenticated' && session) {
      if (session.activeTenant) {
        void navigate('/merchant', { replace: true });
      } else if (session.tenants.length === 0) {
        void navigate('/onboarding', { replace: true });
      } else {
        void navigate('/auth', { replace: true });
      }
      return;
    }

    void navigate('/auth', { replace: true });
  }, [navigate, session, status, telegram.startParam]);

  return (
    <div className="mx-auto max-w-lg px-4 py-8">
      <LoadingState rows={2} />
    </div>
  );
}

export const router = createBrowserRouter([
  { path: '/', element: <EntryRedirect /> },
  { path: '/auth', element: <AuthPage /> },
  { path: '/onboarding', element: <OnboardingPage /> },
  {
    path: '/merchant',
    element: <MerchantLayout />,
    children: [
      { index: true, element: <MerchantDashboardPage /> },
      {
        path: 'products',
        element: (
          <MerchantSectionPlaceholder
            titleKey="merchant.placeholders.productsTitle"
            descKey="merchant.placeholders.productsDesc"
          />
        ),
      },
      {
        path: 'orders',
        element: (
          <MerchantSectionPlaceholder
            titleKey="merchant.placeholders.ordersTitle"
            descKey="merchant.placeholders.ordersDesc"
          />
        ),
      },
      {
        path: 'customers',
        element: (
          <MerchantSectionPlaceholder
            titleKey="merchant.placeholders.customersTitle"
            descKey="merchant.placeholders.customersDesc"
          />
        ),
      },
      {
        path: 'settings',
        element: (
          <MerchantSectionPlaceholder
            titleKey="merchant.placeholders.settingsTitle"
            descKey="merchant.placeholders.settingsDesc"
          />
        ),
      },
    ],
  },
  {
    path: '/s/:shopSlug',
    element: <StorefrontLayout />,
    children: [
      { index: true, element: <StorefrontCatalogPage /> },
      { path: 'cart', element: <StorefrontCartPlaceholder /> },
      { path: 'orders', element: <StorefrontOrdersPlaceholder /> },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
