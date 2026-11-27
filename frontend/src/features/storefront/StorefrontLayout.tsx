import { useQuery } from '@tanstack/react-query';
import { ShoppingBag, ShoppingCart, Store } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, NavLink, Outlet, useParams } from 'react-router';
import { apiClient } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/button';
import { ErrorState, LoadingState } from '@/shared/ui/state-views';
import { ToolbarControls } from '@/shared/ui/toolbar-controls';

export function StorefrontLayout() {
  const { t } = useTranslation();
  const { shopSlug = '' } = useParams();

  const shopQuery = useQuery({
    queryKey: ['storefront-shop', shopSlug],
    enabled: Boolean(shopSlug),
    queryFn: async () => {
      const { data, error, response } = await apiClient.GET('/api/v1/storefront/{shopSlug}', {
        params: { path: { shopSlug } },
      });
      if (response.status === 404) {
        return null;
      }
      if (error || !data) {
        throw new Error('Failed to load shop');
      }
      return data;
    },
  });

  const navItems = [
    { to: `/s/${shopSlug}`, end: true, key: 'storefront.nav.shop', icon: Store },
    { to: `/s/${shopSlug}/cart`, end: false, key: 'storefront.nav.cart', icon: ShoppingCart },
    { to: `/s/${shopSlug}/orders`, end: false, key: 'storefront.nav.orders', icon: ShoppingBag },
  ] as const;

  return (
    <div className="flex min-h-dvh flex-col bg-background text-foreground">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-primary focus:px-3 focus:py-2 focus:text-primary-foreground"
      >
        {t('app.skipToContent')}
      </a>

      <header className="sticky top-0 z-20 border-b border-border bg-surface/95 backdrop-blur-sm">
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-2 px-4 py-2.5">
          <div className="flex min-w-0 items-center gap-2.5">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/15 text-primary">
              <Store className="size-4" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="truncate text-sm font-bold text-foreground">
                {shopQuery.data?.name ?? shopSlug}
              </p>
              <p className="truncate text-xs text-muted-foreground" dir="ltr">
                /{shopSlug}
              </p>
            </div>
          </div>
          <ToolbarControls />
        </div>
      </header>

      <main id="main-content" className="mx-auto w-full max-w-3xl flex-1 px-4 pt-4 pb-24">
        {shopQuery.isLoading ? (
          <LoadingState rows={3} />
        ) : shopQuery.isError ? (
          <ErrorState onRetry={() => void shopQuery.refetch()} />
        ) : shopQuery.data === null ? (
          <ErrorState
            title={t('storefront.shopNotFoundTitle')}
            message={t('storefront.shopNotFoundDesc')}
            onRetry={() => void shopQuery.refetch()}
          />
        ) : (
          <Outlet context={shopQuery.data} />
        )}

        {shopQuery.data === null ? (
          <div className="mt-4 flex justify-center">
            <Button asChild variant="outline">
              <Link to="/auth">{t('storefront.backHome')}</Link>
            </Button>
          </div>
        ) : null}
      </main>

      {shopQuery.data ? (
        <nav
          aria-label="Storefront navigation"
          className="safe-bottom fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface/95 backdrop-blur-sm"
        >
          <ul className="mx-auto grid max-w-3xl grid-cols-3">
            {navItems.map(({ to, end, key, icon: Icon }) => (
              <li key={to}>
                <NavLink
                  to={to}
                  end={end}
                  className={({ isActive }) =>
                    cn(
                      'flex min-h-14 flex-col items-center justify-center gap-1 px-2 py-1.5 text-xs font-medium transition-colors',
                      isActive ? 'text-primary' : 'text-muted-foreground hover:text-foreground',
                    )
                  }
                >
                  <Icon className="size-5" aria-hidden="true" />
                  <span>{t(key)}</span>
                </NavLink>
              </li>
            ))}
          </ul>
        </nav>
      ) : null}
    </div>
  );
}
