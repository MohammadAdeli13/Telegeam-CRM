import { lazy } from 'react';
import { createBrowserRouter, Navigate } from 'react-router';
import { AuthPage } from '@/features/auth/AuthPage';
import { OnboardingPage } from '@/features/auth/OnboardingPage';
import { EntryRedirect } from './EntryRedirect';
import { LazyRoute } from './LazyRoute';

const MerchantLayout = lazy(() =>
  import('@/features/merchant/MerchantLayout').then((m) => ({ default: m.MerchantLayout })),
);
const MerchantDashboardPage = lazy(() =>
  import('@/features/merchant/MerchantDashboardPage').then((m) => ({
    default: m.MerchantDashboardPage,
  })),
);
const MerchantProductsPage = lazy(() =>
  import('@/features/merchant/MerchantProductsPage').then((m) => ({
    default: m.MerchantProductsPage,
  })),
);
const MerchantOrdersPage = lazy(() =>
  import('@/features/merchant/MerchantOrdersPage').then((m) => ({
    default: m.MerchantOrdersPage,
  })),
);
const MerchantCustomersPage = lazy(() =>
  import('@/features/merchant/MerchantCustomersPage').then((m) => ({
    default: m.MerchantCustomersPage,
  })),
);
const MerchantSettingsPage = lazy(() =>
  import('@/features/merchant/MerchantSettingsPage').then((m) => ({
    default: m.MerchantSettingsPage,
  })),
);

const StorefrontLayout = lazy(() =>
  import('@/features/storefront/StorefrontLayout').then((m) => ({ default: m.StorefrontLayout })),
);
const StorefrontCatalogPage = lazy(() =>
  import('@/features/storefront/StorefrontCatalogPage').then((m) => ({
    default: m.StorefrontCatalogPage,
  })),
);
const StorefrontProductDetailPage = lazy(() =>
  import('@/features/storefront/StorefrontProductDetailPage').then((m) => ({
    default: m.StorefrontProductDetailPage,
  })),
);
const StorefrontCartPage = lazy(() =>
  import('@/features/storefront/StorefrontCartPage').then((m) => ({
    default: m.StorefrontCartPage,
  })),
);
const StorefrontOrdersPage = lazy(() =>
  import('@/features/storefront/StorefrontOrdersPage').then((m) => ({
    default: m.StorefrontOrdersPage,
  })),
);
const StorefrontOrderDetailPage = lazy(() =>
  import('@/features/storefront/StorefrontOrderDetailPage').then((m) => ({
    default: m.StorefrontOrderDetailPage,
  })),
);

export const router = createBrowserRouter([
  { path: '/', element: <EntryRedirect /> },
  { path: '/auth', element: <AuthPage /> },
  { path: '/onboarding', element: <OnboardingPage /> },
  {
    path: '/merchant',
    element: (
      <LazyRoute>
        <MerchantLayout />
      </LazyRoute>
    ),
    children: [
      {
        index: true,
        element: (
          <LazyRoute>
            <MerchantDashboardPage />
          </LazyRoute>
        ),
      },
      {
        path: 'products',
        element: (
          <LazyRoute>
            <MerchantProductsPage />
          </LazyRoute>
        ),
      },
      {
        path: 'orders',
        element: (
          <LazyRoute>
            <MerchantOrdersPage />
          </LazyRoute>
        ),
      },
      {
        path: 'customers',
        element: (
          <LazyRoute>
            <MerchantCustomersPage />
          </LazyRoute>
        ),
      },
      {
        path: 'settings',
        element: (
          <LazyRoute>
            <MerchantSettingsPage />
          </LazyRoute>
        ),
      },
    ],
  },
  {
    path: '/s/:shopSlug',
    element: (
      <LazyRoute>
        <StorefrontLayout />
      </LazyRoute>
    ),
    children: [
      {
        index: true,
        element: (
          <LazyRoute>
            <StorefrontCatalogPage />
          </LazyRoute>
        ),
      },
      {
        path: 'products/:productId',
        element: (
          <LazyRoute>
            <StorefrontProductDetailPage />
          </LazyRoute>
        ),
      },
      {
        path: 'cart',
        element: (
          <LazyRoute>
            <StorefrontCartPage />
          </LazyRoute>
        ),
      },
      {
        path: 'orders',
        element: (
          <LazyRoute>
            <StorefrontOrdersPage />
          </LazyRoute>
        ),
      },
      {
        path: 'orders/:orderId',
        element: (
          <LazyRoute>
            <StorefrontOrderDetailPage />
          </LazyRoute>
        ),
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
