import {
  ExternalLink,
  LayoutDashboard,
  Package,
  Settings,
  ShoppingBag,
  Store,
  Users,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Link, Navigate, NavLink, Outlet } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { cn } from '@/shared/lib/cn';
import { Button } from '@/shared/ui/button';
import { ToolbarControls } from '@/shared/ui/toolbar-controls';

const NAV_ITEMS = [
  { to: '/merchant', end: true, key: 'merchant.nav.dashboard', icon: LayoutDashboard },
  { to: '/merchant/products', end: false, key: 'merchant.nav.products', icon: Package },
  { to: '/merchant/orders', end: false, key: 'merchant.nav.orders', icon: ShoppingBag },
  { to: '/merchant/customers', end: false, key: 'merchant.nav.customers', icon: Users },
  { to: '/merchant/settings', end: false, key: 'merchant.nav.settings', icon: Settings },
] as const;

export function MerchantLayout() {
  const { t } = useTranslation();
  const { status, session } = useAuth();

  if (status === 'unauthenticated' || status === 'reopen_required') {
    return <Navigate to="/auth" replace />;
  }

  if (status === 'authenticated' && session && !session.activeTenant) {
    return <Navigate to={session.tenants.length === 0 ? '/onboarding' : '/auth'} replace />;
  }

  const activeShop = session?.activeTenant;

  return (
    <div className="flex min-h-dvh flex-col bg-background text-foreground lg:flex-row">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:start-4 focus:top-4 focus:z-50 focus:rounded-lg focus:bg-primary focus:px-3 focus:py-2 focus:text-primary-foreground"
      >
        {t('app.skipToContent')}
      </a>

      {/* Desktop persistent sidebar */}
      <aside className="hidden w-60 shrink-0 flex-col border-e border-border bg-surface p-4 lg:flex">
        <div className="mb-6 flex items-center gap-2.5">
          <div className="flex size-10 items-center justify-center rounded-xl bg-primary/15 text-primary">
            <Store className="size-5" aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <p className="truncate text-sm font-bold text-foreground">
              {activeShop?.name ?? t('app.name')}
            </p>
            {activeShop ? (
              <p className="truncate text-xs text-muted-foreground" dir="ltr">
                /{activeShop.slug}
              </p>
            ) : null}
          </div>
        </div>

        <nav aria-label="Merchant navigation" className="flex flex-1 flex-col gap-1">
          {NAV_ITEMS.map(({ to, end, key, icon: Icon }) => (
            <NavLink
              key={to}
              to={to}
              end={end}
              className={({ isActive }) =>
                cn(
                  'flex min-h-11 items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-primary/15 text-primary'
                    : 'text-muted-foreground hover:bg-surface-elevated hover:text-foreground',
                )
              }
            >
              <Icon className="size-4 shrink-0" aria-hidden="true" />
              <span>{t(key)}</span>
            </NavLink>
          ))}
        </nav>

        {activeShop ? (
          <Button asChild variant="outline" size="sm" className="mt-4 w-full justify-center">
            <Link to={`/s/${activeShop.slug}`}>
              <ExternalLink className="size-4" aria-hidden="true" />
              {t('merchant.previewStorefront')}
            </Link>
          </Button>
        ) : null}
      </aside>

      {/* Main area */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-20 flex items-center justify-between gap-2 border-b border-border bg-surface/95 px-4 py-2.5 backdrop-blur-sm">
          <div className="flex min-w-0 items-center gap-2 lg:hidden">
            <div className="flex size-9 shrink-0 items-center justify-center rounded-lg bg-primary/15 text-primary">
              <Store className="size-4" aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="truncate text-sm font-semibold text-foreground">
                {activeShop?.name ?? t('app.name')}
              </p>
              {activeShop ? (
                <p className="truncate text-xs text-muted-foreground" dir="ltr">
                  /{activeShop.slug}
                </p>
              ) : null}
            </div>
          </div>

          <div className="hidden lg:block">
            <Button asChild variant="ghost" size="sm">
              <Link to="/auth">{t('merchant.switchShop')}</Link>
            </Button>
          </div>

          <ToolbarControls />
        </header>

        <main id="main-content" className="mx-auto w-full max-w-5xl flex-1 px-4 pt-4 pb-24 lg:pb-8">
          <Outlet />
        </main>
      </div>

      {/* Mobile bottom tab bar (5 destinations) */}
      <nav
        aria-label="Merchant mobile navigation"
        className="safe-bottom fixed inset-x-0 bottom-0 z-30 border-t border-border bg-surface/95 backdrop-blur-sm lg:hidden"
      >
        <ul className="grid grid-cols-5">
          {NAV_ITEMS.map(({ to, end, key, icon: Icon }) => (
            <li key={to}>
              <NavLink
                to={to}
                end={end}
                className={({ isActive }) =>
                  cn(
                    'flex min-h-14 flex-col items-center justify-center gap-1 px-1 py-1.5 text-[11px] font-medium transition-colors',
                    isActive ? 'text-primary' : 'text-muted-foreground hover:text-foreground',
                  )
                }
              >
                <Icon className="size-5" aria-hidden="true" />
                <span className="truncate">{t(key)}</span>
              </NavLink>
            </li>
          ))}
        </ul>
      </nav>
    </div>
  );
}
