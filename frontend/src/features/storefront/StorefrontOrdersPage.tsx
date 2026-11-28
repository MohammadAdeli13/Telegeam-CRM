import { useQuery } from '@tanstack/react-query';
import { Eye, RefreshCw, ShoppingBag } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useOutletContext, useParams } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient } from '@/shared/api/client';
import { formatDate, formatMoney, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { Pagination } from '@/shared/ui/pagination';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { OrderStatusBadge } from '@/shared/ui/status-badge';
import type { components } from '@/types/api';

type StorefrontShop = components['schemas']['StorefrontShopDto'];

export function StorefrontOrdersPage() {
  const { t, i18n } = useTranslation();
  const { shopSlug = '' } = useParams();
  const shop = useOutletContext<StorefrontShop>();
  const { status: authStatus } = useAuth();
  const [page, setPage] = useState(1);

  const lang = normalizeLanguage(i18n.language);
  const isAuthenticated = authStatus === 'authenticated';

  const ordersQuery = useQuery({
    queryKey: ['storefront-orders', shopSlug, page],
    enabled: isAuthenticated && Boolean(shopSlug),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/storefront/{shopSlug}/orders', {
        params: { path: { shopSlug }, query: { page, pageSize: 20 } },
      });
      if (error || !data) {
        throw new Error('Failed to load orders');
      }
      return data;
    },
  });

  if (!isAuthenticated) {
    return (
      <div className="mx-auto max-w-lg py-4">
        <EmptyState
          icon={<ShoppingBag className="size-6" aria-hidden="true" />}
          title={t('storefront.myOrdersTitle')}
          description={t('storefront.myOrdersLoginRequired')}
          action={
            <Button asChild>
              <Link to="/auth">{t('storefront.goToSignIn')}</Link>
            </Button>
          }
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('storefront.myOrdersTitle')}
        </h1>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => void ordersQuery.refetch()}
        >
          <RefreshCw className="size-4" aria-hidden="true" />
          <span>{t('common.refresh')}</span>
        </Button>
      </div>

      {ordersQuery.isLoading ? (
        <LoadingState rows={3} />
      ) : ordersQuery.isError ? (
        <ErrorState onRetry={() => void ordersQuery.refetch()} />
      ) : !ordersQuery.data || ordersQuery.data.items.length === 0 ? (
        <EmptyState
          icon={<ShoppingBag className="size-6" aria-hidden="true" />}
          title=""
          description={t('storefront.myOrdersEmpty')}
          action={
            <Button asChild>
              <Link to={`/s/${shopSlug}`}>{t('storefront.continueShopping')}</Link>
            </Button>
          }
        />
      ) : (
        <>
          <div className="space-y-2.5">
            {ordersQuery.data.items.map((order) => (
              <Card key={order.id} className="flex flex-wrap items-center justify-between gap-3">
                <div className="min-w-0 space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="tabular-nums text-sm font-bold text-foreground" dir="ltr">
                      #{order.id.slice(0, 8).toUpperCase()}
                    </span>
                    <OrderStatusBadge status={order.status} />
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {formatDate(order.placedAt, lang)}
                  </p>
                </div>

                <div className="flex items-center gap-3">
                  <span className="tabular-nums text-sm font-bold text-primary">
                    {formatMoney(order.totalAmount, shop.currency, lang)}
                  </span>
                  <Button asChild variant="outline" size="sm">
                    <Link to={`/s/${shopSlug}/orders/${order.id}`}>
                      <Eye className="size-4" aria-hidden="true" />
                      <span>{t('orders.viewDetails')}</span>
                    </Link>
                  </Button>
                </div>
              </Card>
            ))}
          </div>

          <Pagination
            page={toNumber(ordersQuery.data.page, 1)}
            pageSize={toNumber(ordersQuery.data.pageSize, 20)}
            total={toNumber(ordersQuery.data.total, 0)}
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  );
}
