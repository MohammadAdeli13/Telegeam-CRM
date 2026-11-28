import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, Check, Copy, Eye, RefreshCw, ShoppingBag } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import { formatDate, formatMoney, formatNumber, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { copyTextToClipboard, triggerHaptic } from '@/shared/lib/telegram';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { ModalDialog } from '@/shared/ui/dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Pagination } from '@/shared/ui/pagination';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { OrderStatusBadge, type OrderStatusValue } from '@/shared/ui/status-badge';
import { getNextOrderStatuses, ORDER_STATUS_LIST } from './merchant-state';

export function MerchantOrdersPage() {
  const { t, i18n } = useTranslation();
  const { session } = useAuth();
  const lang = normalizeLanguage(i18n.language);
  const activeShop = session?.activeTenant;
  const tenantKey = activeShop?.id ?? 'none';
  const currency = activeShop?.currency ?? 'IRT';

  const [statusFilter, setStatusFilter] = useState<OrderStatusValue | undefined>(undefined);
  const [page, setPage] = useState(1);
  const [selectedOrderId, setSelectedOrderId] = useState<string | null>(null);
  const [copiedLink, setCopiedLink] = useState(false);

  const ordersQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'orders', page, statusFilter ?? 'all'],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/orders', {
        params: {
          query: {
            page,
            pageSize: 20,
            status: statusFilter,
          },
        },
      });
      if (error || !data) {
        throw new Error('Failed to load orders');
      }
      return data;
    },
  });

  const handleCopyStorefrontLink = async () => {
    if (!activeShop) {
      return;
    }
    const ok = await copyTextToClipboard(`${window.location.origin}/s/${activeShop.slug}`);
    if (ok) {
      triggerHaptic('success');
      setCopiedLink(true);
      window.setTimeout(() => setCopiedLink(false), 2000);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-tight text-foreground">{t('orders.title')}</h1>
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

      {/* Horizontal scrollable single-row status filter pills on mobile */}
      <div
        className="no-scrollbar flex flex-nowrap gap-2 overflow-x-auto pb-1 lg:flex-wrap"
        role="tablist"
        aria-label={t('common.status')}
      >
        <button
          type="button"
          role="tab"
          aria-selected={!statusFilter}
          onClick={() => {
            triggerHaptic('selection');
            setStatusFilter(undefined);
            setPage(1);
          }}
          className={cn(
            'min-h-11 shrink-0 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
            !statusFilter
              ? 'border-primary bg-primary text-primary-foreground'
              : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
          )}
        >
          {t('common.all')}
        </button>
        {ORDER_STATUS_LIST.map((st) => {
          const active = statusFilter === st;
          return (
            <button
              key={st}
              type="button"
              role="tab"
              aria-selected={active}
              onClick={() => {
                triggerHaptic('selection');
                setStatusFilter(st);
                setPage(1);
              }}
              className={cn(
                'min-h-11 shrink-0 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
                active
                  ? 'border-primary bg-primary text-primary-foreground'
                  : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
              )}
            >
              {t(`orders.statuses.${st}`)}
            </button>
          );
        })}
      </div>

      {ordersQuery.isLoading ? (
        <LoadingState rows={3} />
      ) : ordersQuery.isError ? (
        <ErrorState onRetry={() => void ordersQuery.refetch()} />
      ) : !ordersQuery.data || ordersQuery.data.items.length === 0 ? (
        <EmptyState
          icon={<ShoppingBag className="size-6" aria-hidden="true" />}
          title={statusFilter ? t('app.emptyTitle') : t('orders.emptyAll')}
          description={statusFilter ? t('orders.empty') : t('orders.emptyAllHint')}
          action={
            !statusFilter && activeShop ? (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => void handleCopyStorefrontLink()}
              >
                {copiedLink ? (
                  <Check className="size-4 text-success" aria-hidden="true" />
                ) : (
                  <Copy className="size-4" aria-hidden="true" />
                )}
                <span>{copiedLink ? t('merchant.linkCopied') : t('merchant.copyShopLink')}</span>
              </Button>
            ) : null
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
                    {order.customerDisplayName ?? t('orders.guestCustomer')} ·{' '}
                    {formatDate(order.placedAt, lang)}
                  </p>
                  {order.note ? (
                    <p className="line-clamp-1 text-xs text-muted-foreground">{order.note}</p>
                  ) : null}
                </div>

                <div className="flex items-center gap-3">
                  <span className="tabular-nums text-sm font-bold text-primary">
                    {formatMoney(order.totalAmount, currency, lang)}
                  </span>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setSelectedOrderId(order.id)}
                  >
                    <Eye className="size-4" aria-hidden="true" />
                    <span>{t('orders.viewDetails')}</span>
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

      {selectedOrderId ? (
        <OrderDetailModal
          tenantKey={tenantKey}
          currency={currency}
          orderId={selectedOrderId}
          onClose={() => setSelectedOrderId(null)}
        />
      ) : null}
    </div>
  );
}

function OrderDetailModal({
  tenantKey,
  currency,
  orderId,
  onClose,
}: {
  tenantKey: string;
  currency: string;
  orderId: string;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const lang = normalizeLanguage(i18n.language);

  const [reason, setReason] = useState('');
  const [confirmCancel, setConfirmCancel] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const detailQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'order-detail', orderId],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/orders/{id}', {
        params: { path: { id: orderId } },
      });
      if (error || !data) {
        throw new Error('Failed to load order detail');
      }
      return data;
    },
  });

  const transitionMutation = useMutation({
    mutationFn: async (targetStatus: OrderStatusValue) => {
      const current = detailQuery.data;
      const { data, error } = await apiClient.PATCH('/api/v1/merchant/orders/{id}/status', {
        params: { path: { id: orderId } },
        body: {
          status: targetStatus,
          reason: reason.trim() || null,
          rowVersion: current ? toNumber(current.rowVersion) : null,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Status update failed');
      }
      return data;
    },
    onSuccess: () => {
      triggerHaptic('success');
      setReason('');
      setConfirmCancel(false);
      setErrorMsg(null);
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'order-detail', orderId],
      });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'orders'] });
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'kpi-pending-orders'],
      });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'recent-orders'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
    },
    onError: (err) => {
      triggerHaptic('error');
      const code = extractProblemCode(err);
      setErrorMsg(
        code === 'concurrency_conflict'
          ? t('orders.concurrencyConflict')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const order = detailQuery.data;
  const nextStatuses = order ? getNextOrderStatuses(order.status) : [];

  return (
    <ModalDialog
      open
      title={t('orders.orderNumber', { id: orderId.slice(0, 8).toUpperCase() })}
      onClose={onClose}
    >
      {detailQuery.isLoading ? (
        <LoadingState rows={2} />
      ) : detailQuery.isError || !order ? (
        <ErrorState onRetry={() => void detailQuery.refetch()} />
      ) : (
        <div className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border bg-background p-3">
            <div>
              <p className="text-xs text-muted-foreground">{t('orders.customer')}</p>
              <p className="text-sm font-semibold text-foreground">
                {order.customerDisplayName ?? t('orders.guestCustomer')}
              </p>
              <p className="mt-0.5 text-xs text-muted-foreground">
                {formatDate(order.placedAt, lang)}
              </p>
            </div>
            <div className="flex flex-col items-end gap-1.5">
              <OrderStatusBadge status={order.status} />
              <span className="tabular-nums text-base font-bold text-primary">
                {formatMoney(order.totalAmount, currency, lang)}
              </span>
            </div>
          </div>

          {order.note ? (
            <div className="rounded-lg border border-border bg-background p-3">
              <p className="text-xs font-medium text-muted-foreground">{t('orders.note')}</p>
              <p className="mt-1 text-sm text-foreground">{order.note}</p>
            </div>
          ) : null}

          {/* Items */}
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-foreground">{t('orders.itemsTitle')}</h3>
            <div className="divide-y divide-border rounded-lg border border-border bg-background">
              {order.items.map((item) => (
                <div key={item.id} className="flex items-center justify-between gap-3 p-3 text-sm">
                  <div className="min-w-0">
                    <p className="truncate font-medium text-foreground">
                      {item.productNameSnapshot}
                    </p>
                    <p className="tabular-nums text-xs text-muted-foreground">
                      {t('orders.quantity', { count: formatNumber(toNumber(item.quantity), lang) })}{' '}
                      × {formatMoney(item.unitPrice, currency, lang)}
                    </p>
                  </div>
                  <span className="tabular-nums shrink-0 font-semibold text-foreground">
                    {formatMoney(item.lineTotal, currency, lang)}
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* Transition controls */}
          <div className="space-y-3 rounded-lg border border-border bg-background p-3">
            <h3 className="text-sm font-semibold text-foreground">
              {t('orders.transitionTitle')}
            </h3>
            {nextStatuses.length === 0 ? (
              <p className="text-xs text-muted-foreground">{t('orders.terminalHint')}</p>
            ) : (
              <>
                <div className="space-y-1.5">
                  <Label htmlFor="order-reason">{t('orders.reasonLabel')}</Label>
                  <Input
                    id="order-reason"
                    maxLength={500}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder={t('orders.reasonPlaceholder')}
                  />
                </div>

                {confirmCancel ? (
                  <div className="space-y-2.5 rounded-lg border border-destructive/40 bg-destructive/10 p-3">
                    <div className="flex items-start gap-2 text-xs text-destructive">
                      <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                      <p>{t('orders.cancelConfirmWarning')}</p>
                    </div>
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="destructive"
                        size="sm"
                        disabled={transitionMutation.isPending}
                        onClick={() => transitionMutation.mutate('Cancelled')}
                      >
                        {t('common.confirm')}
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setConfirmCancel(false)}
                      >
                        {t('common.cancel')}
                      </Button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {nextStatuses.map((target) => {
                      const isCancel = target === 'Cancelled';
                      return (
                        <Button
                          key={target}
                          type="button"
                          size="sm"
                          variant={isCancel ? 'destructive' : 'default'}
                          disabled={transitionMutation.isPending}
                          onClick={() => {
                            if (isCancel) {
                              triggerHaptic('warning');
                              setConfirmCancel(true);
                            } else {
                              transitionMutation.mutate(target);
                            }
                          }}
                        >
                          {t('orders.moveTo', { status: t(`orders.statuses.${target}`) })}
                        </Button>
                      );
                    })}
                  </div>
                )}
              </>
            )}

            {errorMsg ? (
              <p role="alert" className="text-xs text-destructive">
                {errorMsg}
              </p>
            ) : null}
          </div>

          {/* Status History Timeline */}
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-foreground">{t('orders.timelineTitle')}</h3>
            <ol className="space-y-2">
              {order.statusHistory.map((entry) => {
                const roleLabel = entry.actorRole
                  ? t(`roles.${entry.actorRole}`, { defaultValue: entry.actorRole })
                  : null;
                const noteText =
                  entry.reason && entry.reason !== 'Order placed.'
                    ? entry.reason
                    : entry.fromStatus === null
                      ? t('orders.orderPlacedEvent')
                      : null;

                return (
                  <li
                    key={entry.id}
                    className="rounded-lg border border-border bg-background px-3 py-2 text-xs"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <div className="flex items-center gap-1.5">
                        <OrderStatusBadge status={entry.toStatus} />
                        {roleLabel ? (
                          <span className="text-muted-foreground">({roleLabel})</span>
                        ) : null}
                      </div>
                      <span className="tabular-nums text-muted-foreground">
                        {formatDate(entry.occurredAt, lang)}
                      </span>
                    </div>
                    {noteText ? <p className="mt-1 text-foreground">{noteText}</p> : null}
                  </li>
                );
              })}
            </ol>
          </div>
        </div>
      )}
    </ModalDialog>
  );
}
