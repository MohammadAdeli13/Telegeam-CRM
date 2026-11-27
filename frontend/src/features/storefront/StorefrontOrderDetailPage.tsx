import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, ArrowLeft, ArrowRight, CheckCircle2, XCircle } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useOutletContext, useParams, useSearchParams } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemMessage } from '@/shared/api/client';
import { formatDate, formatMoney, toNumber } from '@/shared/lib/format';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { OrderStatusBadge } from '@/shared/ui/status-badge';
import type { components } from '@/types/api';

type StorefrontShop = components['schemas']['StorefrontShopDto'];

export function StorefrontOrderDetailPage() {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const { shopSlug = '', orderId = '' } = useParams();
  const [searchParams] = useSearchParams();
  const shop = useOutletContext<StorefrontShop>();
  const { status: authStatus } = useAuth();

  const [showCancelBox, setShowCancelBox] = useState(false);
  const [cancelReason, setCancelReason] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const lang = normalizeLanguage(i18n.language);
  const isRtl = getDirection(lang) === 'rtl';
  const BackIcon = isRtl ? ArrowRight : ArrowLeft;
  const isAuthenticated = authStatus === 'authenticated';
  const justPlaced = searchParams.get('placed') === '1';

  const orderQuery = useQuery({
    queryKey: ['storefront-order-detail', shopSlug, orderId],
    enabled: isAuthenticated && Boolean(shopSlug && orderId),
    queryFn: async () => {
      const { data, error, response } = await apiClient.GET(
        '/api/v1/storefront/{shopSlug}/orders/{id}',
        { params: { path: { shopSlug, id: orderId } } },
      );
      if (response.status === 404) {
        return null;
      }
      if (error || !data) {
        throw new Error('Failed to load order');
      }
      return data;
    },
  });

  const cancelMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await apiClient.POST(
        '/api/v1/storefront/{shopSlug}/orders/{id}/cancel',
        {
          params: { path: { shopSlug, id: orderId } },
          body: { reason: cancelReason.trim() || null },
        },
      );
      if (error || !data) {
        throw error ?? new Error('Cancel failed');
      }
      return data;
    },
    onSuccess: () => {
      setShowCancelBox(false);
      setCancelReason('');
      setErrorMsg(null);
      void queryClient.invalidateQueries({
        queryKey: ['storefront-order-detail', shopSlug, orderId],
      });
      void queryClient.invalidateQueries({ queryKey: ['storefront-orders', shopSlug] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-products', shopSlug] });
    },
    onError: (err) => {
      setErrorMsg(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  if (!isAuthenticated) {
    return (
      <EmptyState
        title={t('storefront.myOrdersTitle')}
        description={t('storefront.myOrdersLoginRequired')}
        action={
          <Button asChild>
            <Link to="/auth">{t('storefront.goToSignIn')}</Link>
          </Button>
        }
      />
    );
  }

  if (orderQuery.isLoading) {
    return <LoadingState rows={3} />;
  }

  if (orderQuery.isError) {
    return <ErrorState onRetry={() => void orderQuery.refetch()} />;
  }

  const order = orderQuery.data;
  if (!order) {
    return (
      <div className="space-y-4">
        <ErrorState
          title={t('storefront.orderNotFoundTitle')}
          message={t('storefront.orderNotFoundDesc')}
        />
        <div className="flex justify-center">
          <Button asChild variant="outline">
            <Link to={`/s/${shopSlug}/orders`}>{t('storefront.backToOrders')}</Link>
          </Button>
        </div>
      </div>
    );
  }

  const canCancel = order.status === 'Pending';

  return (
    <div className="space-y-4">
      <Button asChild variant="ghost" size="sm" className="px-2">
        <Link to={`/s/${shopSlug}/orders`}>
          <BackIcon className="size-4" aria-hidden="true" />
          <span>{t('storefront.backToOrders')}</span>
        </Link>
      </Button>

      {justPlaced ? (
        <div
          role="status"
          className="flex items-center gap-2 rounded-xl border border-success/40 bg-success/10 px-4 py-3 text-sm font-medium text-success"
        >
          <CheckCircle2 className="size-5 shrink-0" aria-hidden="true" />
          <span>{t('storefront.orderPlacedBanner')}</span>
        </div>
      ) : null}

      <Card className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h1 className="tabular-nums text-base font-bold text-foreground" dir="ltr">
              {t('orders.orderNumber', { id: order.id.slice(0, 8) })}
            </h1>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {formatDate(order.placedAt, lang)}
            </p>
          </div>
          <OrderStatusBadge status={order.status} />
        </div>

        <div className="flex items-center justify-between border-t border-border pt-3 text-sm">
          <span className="text-muted-foreground">{t('orders.total')}</span>
          <span className="tabular-nums text-lg font-bold text-primary">
            {formatMoney(order.totalAmount, shop.currency, lang)}
          </span>
        </div>

        {order.note ? (
          <div className="border-t border-border pt-3">
            <p className="text-xs font-medium text-muted-foreground">{t('orders.note')}</p>
            <p className="mt-1 text-sm text-foreground">{order.note}</p>
          </div>
        ) : null}
      </Card>

      {/* Order items */}
      <Card className="space-y-2.5">
        <h2 className="text-sm font-semibold text-foreground">{t('orders.itemsTitle')}</h2>
        <div className="divide-y divide-border">
          {order.items.map((item) => (
            <div key={item.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
              <div className="min-w-0">
                <p className="truncate font-medium text-foreground">{item.productNameSnapshot}</p>
                <p className="tabular-nums text-xs text-muted-foreground">
                  {t('orders.quantity', { count: toNumber(item.quantity) })} ×{' '}
                  {formatMoney(item.unitPrice, shop.currency, lang)}
                </p>
              </div>
              <span className="tabular-nums shrink-0 font-semibold text-foreground">
                {formatMoney(item.lineTotal, shop.currency, lang)}
              </span>
            </div>
          ))}
        </div>
      </Card>

      {/* Customer cancel action (Pending only) */}
      {canCancel ? (
        <Card className="space-y-3 border-destructive/30">
          {showCancelBox ? (
            <div className="space-y-3">
              <div className="flex items-start gap-2 text-xs text-destructive">
                <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                <div>
                  <p className="font-semibold">{t('storefront.cancelOrderConfirmTitle')}</p>
                  <p className="mt-0.5 text-muted-foreground">
                    {t('storefront.cancelOrderConfirmDesc')}
                  </p>
                </div>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="cancel-reason">{t('storefront.cancelReasonLabel')}</Label>
                <Input
                  id="cancel-reason"
                  maxLength={500}
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  placeholder={t('storefront.cancelReasonPlaceholder')}
                />
              </div>

              {errorMsg ? (
                <p role="alert" className="text-xs text-destructive">
                  {errorMsg}
                </p>
              ) : null}

              <div className="flex gap-2">
                <Button
                  type="button"
                  variant="destructive"
                  size="sm"
                  disabled={cancelMutation.isPending}
                  onClick={() => cancelMutation.mutate()}
                >
                  {cancelMutation.isPending
                    ? t('storefront.cancellingOrder')
                    : t('common.confirm')}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setShowCancelBox(false)}
                >
                  {t('common.cancel')}
                </Button>
              </div>
            </div>
          ) : (
            <Button
              type="button"
              variant="destructive"
              className="w-full"
              onClick={() => setShowCancelBox(true)}
            >
              <XCircle className="size-4" aria-hidden="true" />
              <span>{t('storefront.cancelOrderButton')}</span>
            </Button>
          )}
        </Card>
      ) : null}

      {/* Timeline */}
      <Card className="space-y-2.5">
        <h2 className="text-sm font-semibold text-foreground">{t('orders.timelineTitle')}</h2>
        <ol className="space-y-2">
          {order.statusHistory.map((entry) => (
            <li
              key={entry.id}
              className="rounded-lg border border-border bg-background px-3 py-2 text-xs"
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <OrderStatusBadge status={entry.toStatus} />
                <span className="tabular-nums text-muted-foreground">
                  {formatDate(entry.occurredAt, lang)}
                </span>
              </div>
              {entry.reason ? <p className="mt-1 text-foreground">{entry.reason}</p> : null}
            </li>
          ))}
        </ol>
      </Card>
    </div>
  );
}
