import { useQuery } from '@tanstack/react-query';
import {
  Check,
  ChevronLeft,
  ChevronRight,
  Clock,
  Copy,
  ExternalLink,
  Package,
  Send,
  ShoppingBag,
  Users,
} from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient } from '@/shared/api/client';
import { formatDate, formatMoney, formatNumber, toNumber } from '@/shared/lib/format';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import {
  copyTextToClipboard,
  openTelegramUrl,
  triggerHaptic,
} from '@/shared/lib/telegram';
import { Button } from '@/shared/ui/button';
import { Card, CardHeader, CardTitle } from '@/shared/ui/card';
import { EmptyState, LoadingState } from '@/shared/ui/state-views';
import { OrderStatusBadge } from '@/shared/ui/status-badge';

export function MerchantDashboardPage() {
  const { t, i18n } = useTranslation();
  const { session } = useAuth();
  const [copyState, setCopyState] = useState<'idle' | 'copied' | 'failed'>('idle');

  const lang = normalizeLanguage(i18n.language);
  const ChevronIcon = getDirection(lang) === 'rtl' ? ChevronLeft : ChevronRight;
  const activeShop = session?.activeTenant;
  const tenantKey = activeShop?.id ?? 'none';
  const currency = activeShop?.currency ?? 'IRT';

  const productsSummary = useQuery({
    queryKey: ['merchant', tenantKey, 'kpi-products'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/products', {
        params: { query: { page: 1, pageSize: 1 } },
      });
      if (error || !data) {
        throw new Error('Failed to load products count');
      }
      return data;
    },
  });

  const pendingOrdersSummary = useQuery({
    queryKey: ['merchant', tenantKey, 'kpi-pending-orders'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/orders', {
        params: { query: { page: 1, pageSize: 1, status: 'Pending' } },
      });
      if (error || !data) {
        throw new Error('Failed to load pending orders');
      }
      return data;
    },
  });

  const recentOrdersQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'recent-orders'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/orders', {
        params: { query: { page: 1, pageSize: 5 } },
      });
      if (error || !data) {
        throw new Error('Failed to load recent orders');
      }
      return data;
    },
  });

  const customersSummary = useQuery({
    queryKey: ['merchant', tenantKey, 'kpi-customers'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/customers', {
        params: { query: { page: 1, pageSize: 1 } },
      });
      if (error || !data) {
        throw new Error('Failed to load customers count');
      }
      return data;
    },
  });

  const storefrontUrl = activeShop ? `${window.location.origin}/s/${activeShop.slug}` : '';

  const handleCopyLink = async () => {
    if (!storefrontUrl) {
      return;
    }
    const ok = await copyTextToClipboard(storefrontUrl);
    if (ok) {
      triggerHaptic('success');
      setCopyState('copied');
      window.setTimeout(() => setCopyState('idle'), 2200);
    } else {
      triggerHaptic('error');
      setCopyState('failed');
      window.setTimeout(() => setCopyState('idle'), 3500);
    }
  };

  const handleShareTelegram = () => {
    if (!storefrontUrl || !activeShop) {
      return;
    }
    triggerHaptic('light');
    const url = `https://t.me/share/url?url=${encodeURIComponent(storefrontUrl)}&text=${encodeURIComponent(activeShop.name)}`;
    openTelegramUrl(url);
  };

  const kpiCards = [
    {
      label: t('merchant.dashboard.pendingOrders'),
      value: pendingOrdersSummary.data
        ? formatNumber(toNumber(pendingOrdersSummary.data.total), lang)
        : '—',
      icon: Clock,
      to: '/merchant/orders',
      accent: 'text-warning',
    },
    {
      label: t('merchant.dashboard.totalOrders'),
      value: recentOrdersQuery.data
        ? formatNumber(toNumber(recentOrdersQuery.data.total), lang)
        : '—',
      icon: ShoppingBag,
      to: '/merchant/orders',
      accent: 'text-primary',
    },
    {
      label: t('merchant.dashboard.totalProducts'),
      value: productsSummary.data
        ? formatNumber(toNumber(productsSummary.data.total), lang)
        : '—',
      icon: Package,
      to: '/merchant/products',
      accent: 'text-primary',
    },
    {
      label: t('merchant.dashboard.totalCustomers'),
      value: customersSummary.data
        ? formatNumber(toNumber(customersSummary.data.total), lang)
        : '—',
      icon: Users,
      to: '/merchant/customers',
      accent: 'text-primary',
    },
  ] as const;

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-foreground">
            {t('merchant.dashboard.title')}
          </h1>
          {activeShop ? (
            <p className="mt-0.5 text-xs text-muted-foreground">{activeShop.name}</p>
          ) : null}
        </div>

        {activeShop ? (
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => void handleCopyLink()}>
              {copyState === 'copied' ? (
                <Check className="size-4 text-success" aria-hidden="true" />
              ) : (
                <Copy className="size-4" aria-hidden="true" />
              )}
              <span>
                {copyState === 'copied' ? t('merchant.linkCopied') : t('merchant.copyShopLink')}
              </span>
            </Button>

            <Button type="button" variant="outline" size="sm" onClick={handleShareTelegram}>
              <Send className="size-4" aria-hidden="true" />
              <span>{t('merchant.shareInTelegram')}</span>
            </Button>

            <Button asChild size="sm" className="lg:hidden">
              <Link to={`/s/${activeShop.slug}`}>
                <ExternalLink className="size-4" aria-hidden="true" />
                <span>{t('merchant.previewStorefront')}</span>
              </Link>
            </Button>
          </div>
        ) : null}
      </div>

      {copyState === 'failed' ? (
        <p
          role="alert"
          className="rounded-lg border border-warning/40 bg-warning/10 px-3 py-2 text-xs text-warning"
        >
          {t('merchant.copyFailed')}
        </p>
      ) : null}

      {/* Live KPI grid */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {kpiCards.map(({ label, value, icon: Icon, to, accent }) => (
          <Link key={label} to={to} className="group block">
            <Card className="transition-colors hover:border-primary/40 hover:bg-surface-elevated">
              <div className="flex items-center justify-between gap-2">
                <p className="text-xs font-medium text-muted-foreground">{label}</p>
                <Icon className={`size-4 ${accent}`} aria-hidden="true" />
              </div>
              <div className="mt-2 flex items-end justify-between gap-2">
                <p className="tabular-nums text-2xl font-bold text-foreground">{value}</p>
                <ChevronIcon
                  className="size-4 text-muted-foreground opacity-60 transition-opacity group-hover:opacity-100"
                  aria-hidden="true"
                />
              </div>
            </Card>
          </Link>
        ))}
      </div>

      {/* Recent Orders */}
      <Card className="space-y-3">
        <CardHeader className="mb-0 flex-row items-center justify-between gap-2">
          <CardTitle>{t('merchant.dashboard.recentOrders')}</CardTitle>
          <Button asChild variant="ghost" size="sm">
            <Link to="/merchant/orders">{t('merchant.dashboard.viewAllOrders')}</Link>
          </Button>
        </CardHeader>

        {recentOrdersQuery.isLoading ? (
          <LoadingState rows={2} />
        ) : !recentOrdersQuery.data || recentOrdersQuery.data.items.length === 0 ? (
          <EmptyState
            embedded
            icon={<ShoppingBag className="size-6" aria-hidden="true" />}
            title={t('merchant.dashboard.noRecentOrders')}
            description={t('merchant.dashboard.noRecentOrdersHint')}
            action={
              activeShop ? (
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => void handleCopyLink()}
                >
                  <Copy className="size-4" aria-hidden="true" />
                  <span>{t('merchant.copyShopLink')}</span>
                </Button>
              ) : null
            }
          />
        ) : (
          <div className="divide-y divide-border">
            {recentOrdersQuery.data.items.map((order) => (
              <Link
                key={order.id}
                to="/merchant/orders"
                className="flex min-h-14 items-center justify-between gap-3 py-2.5 transition-colors hover:opacity-85"
              >
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="tabular-nums text-sm font-semibold text-foreground" dir="ltr">
                      #{order.id.slice(0, 8).toUpperCase()}
                    </span>
                    <OrderStatusBadge status={order.status} />
                  </div>
                  <p className="mt-0.5 truncate text-xs text-muted-foreground">
                    {order.customerDisplayName ?? t('orders.guestCustomer')} ·{' '}
                    {formatDate(order.placedAt, lang)}
                  </p>
                </div>
                <span className="tabular-nums shrink-0 text-sm font-bold text-primary">
                  {formatMoney(order.totalAmount, currency, lang)}
                </span>
              </Link>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}
