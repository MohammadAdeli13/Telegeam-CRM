import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  Check,
  CheckCircle2,
  Clock,
  Copy,
  CreditCard,
  ExternalLink,
  MapPin,
  ShoppingBag,
  Truck,
  Upload,
  XCircle,
} from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useOutletContext, useParams, useSearchParams } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemMessage } from '@/shared/api/client';
import { uploadOrderReceipt } from '@/shared/api/uploads';
import {
  formatCardNumberDisplay,
  formatDate,
  formatDateTime,
  formatMoney,
  formatNumber,
  formatOrderCode,
  formatRemainingTime,
  toNumber,
} from '@/shared/lib/format';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import { copyTextToClipboard, triggerHaptic } from '@/shared/lib/telegram';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { PaymentStatusBadge } from '@/shared/ui/payment-badge';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { OrderStatusBadge } from '@/shared/ui/status-badge';
import type { components } from '@/types/api';

type StorefrontShop = components['schemas']['StorefrontShopDto'];

const ALLOWED_RECEIPT_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif']);
const MAX_RECEIPT_BYTES = 5 * 1024 * 1024;

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
  const [receiptFile, setReceiptFile] = useState<File | null>(null);
  const [receiptError, setReceiptError] = useState<string | null>(null);
  const [cardCopied, setCardCopied] = useState(false);
  const [trackingCopied, setTrackingCopied] = useState(false);
  const [nowMs, setNowMs] = useState(() => Date.now());

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

  const paymentExpiresAt = orderQuery.data?.paymentExpiresAt ?? null;
  const orderStatus = orderQuery.data?.status ?? null;

  useEffect(() => {
    if (!paymentExpiresAt || orderStatus !== 'Pending') {
      return;
    }
    const timer = window.setInterval(() => {
      setNowMs(Date.now());
    }, 1000);
    return () => window.clearInterval(timer);
  }, [paymentExpiresAt, orderStatus]);

  const receiptMutation = useMutation({
    mutationFn: async () => {
      if (!receiptFile) {
        throw new Error('No file selected');
      }
      const res = await uploadOrderReceipt(shopSlug, orderId, receiptFile);
      if (res.error || !res.data) {
        throw res.error ?? new Error('Upload failed');
      }
      return res.data;
    },
    onSuccess: () => {
      triggerHaptic('success');
      setReceiptFile(null);
      setReceiptError(null);
      void queryClient.invalidateQueries({
        queryKey: ['storefront-order-detail', shopSlug, orderId],
      });
      void queryClient.invalidateQueries({ queryKey: ['storefront-orders', shopSlug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setReceiptError(extractProblemMessage(err, t('app.errorGeneric')));
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
      triggerHaptic('success');
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
      triggerHaptic('error');
      setErrorMsg(extractProblemMessage(err, t('app.errorGeneric')));
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

  if (orderQuery.isLoading) {
    return <LoadingState rows={3} />;
  }

  if (orderQuery.isError) {
    return <ErrorState onRetry={() => void orderQuery.refetch()} />;
  }

  const order = orderQuery.data;
  if (!order) {
    return (
      <div className="mx-auto max-w-lg py-6">
        <ErrorState
          title={t('storefront.orderNotFoundTitle')}
          message={t('storefront.orderNotFoundDesc')}
          action={
            <Button asChild variant="outline">
              <Link to={`/s/${shopSlug}/orders`}>{t('storefront.backToOrders')}</Link>
            </Button>
          }
        />
      </div>
    );
  }

  const orderCode = formatOrderCode(order.id, order.orderNumber, lang);
  const isCardToCard = order.paymentMethod === 'CardToCard';
  const needsReceipt =
    isCardToCard &&
    order.status === 'Pending' &&
    (order.paymentStatus === 'AwaitingReceipt' || order.paymentStatus === 'Rejected');

  const countdown =
    order.paymentExpiresAt && order.status === 'Pending'
      ? formatRemainingTime(order.paymentExpiresAt, nowMs, lang)
      : null;

  const canCancel =
    order.status === 'Pending' ||
    (order.status === 'Confirmed' &&
      !shop.requireOrderApproval &&
      order.paymentStatus !== 'Verified');

  const handleReceiptFileChange = (file: File | null) => {
    setReceiptError(null);
    if (!file) {
      setReceiptFile(null);
      return;
    }
    if (!ALLOWED_RECEIPT_TYPES.has(file.type)) {
      setReceiptError(t('merchant.catalog.invalidFileType'));
      setReceiptFile(null);
      return;
    }
    if (file.size > MAX_RECEIPT_BYTES) {
      setReceiptError(t('merchant.catalog.fileTooLarge'));
      setReceiptFile(null);
      return;
    }
    setReceiptFile(file);
  };

  const handleReceiptSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!receiptFile) {
      return;
    }
    receiptMutation.mutate();
  };

  const handleCopyCard = async () => {
    if (!shop.cardNumber) {
      return;
    }
    const digits = shop.cardNumber.replace(/\D/g, '');
    const ok = await copyTextToClipboard(digits || shop.cardNumber);
    if (ok) {
      triggerHaptic('success');
      setCardCopied(true);
      window.setTimeout(() => setCardCopied(false), 2000);
    }
  };

  const handleCopyTracking = async () => {
    if (!order.trackingCode) {
      return;
    }
    const ok = await copyTextToClipboard(order.trackingCode);
    if (ok) {
      triggerHaptic('success');
      setTrackingCopied(true);
      window.setTimeout(() => setTrackingCopied(false), 2000);
    }
  };

  const shippingFee = toNumber(order.shippingAmount, 0);
  const subtotalFee = toNumber(order.subtotalAmount, 0);

  return (
    <div className="mx-auto max-w-3xl space-y-4">
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

      {/* Summary header */}
      <Card className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <div>
            <h1 className="tabular-nums text-base font-bold text-foreground">
              {t('orders.orderNumber', { id: orderCode })}
            </h1>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {formatDateTime(order.placedAt, lang)}
            </p>
          </div>
          <div className="flex flex-wrap items-center gap-1.5">
            <OrderStatusBadge status={order.status} />
            <PaymentStatusBadge status={order.paymentStatus} />
          </div>
        </div>

        <div className="space-y-1.5 border-t border-border pt-3 text-xs">
          {order.shippingMethodNameSnapshot ? (
            <>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">{t('orders.subtotal')}</span>
                <span className="tabular-nums text-foreground">
                  {formatMoney(subtotalFee > 0 ? subtotalFee : order.totalAmount, shop.currency, lang)}
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">{t('orders.shippingCost')}</span>
                <span className="tabular-nums text-foreground">
                  {order.isShippingPayAtDestination
                    ? t('orders.payAtDestination')
                    : shippingFee === 0
                      ? t('orders.shippingFree')
                      : formatMoney(shippingFee, shop.currency, lang)}
                </span>
              </div>
            </>
          ) : null}
          <div className="flex items-center justify-between pt-1 text-sm">
            <span className="text-muted-foreground">{t('orders.total')}</span>
            <span className="tabular-nums text-lg font-bold text-primary">
              {formatMoney(order.totalAmount, shop.currency, lang)}
            </span>
          </div>
        </div>

        {order.note ? (
          <div className="border-t border-border pt-3">
            <p className="text-xs font-medium text-muted-foreground">{t('orders.note')}</p>
            <p className="mt-1 text-sm text-foreground">{order.note}</p>
          </div>
        ) : null}
      </Card>

      {/* Card-to-Card Payment Details, Countdown & Receipt Upload */}
      {isCardToCard ? (
        <Card className="space-y-3 border-primary/30">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <CreditCard className="size-4 text-primary" aria-hidden="true" />
              <h2 className="text-sm font-bold text-foreground">
                {t('storefront.checkout.cardDetailsTitle')}
              </h2>
            </div>

            {countdown ? (
              <Badge variant={countdown.expired ? 'destructive' : 'warning'}>
                <Clock className="size-3.5" aria-hidden="true" />
                <span className="tabular-nums">
                  {countdown.expired
                    ? t('orders.paymentExpiredBadge')
                    : t('orders.paymentRemaining', { time: countdown.text })}
                </span>
              </Badge>
            ) : null}
          </div>

          {order.paymentStatus === 'Rejected' && order.paymentRejectionReason ? (
            <div
              role="alert"
              className="flex items-start gap-2 rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-xs text-destructive"
            >
              <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
              <div>
                <p className="font-semibold">{t('orders.rejectionReasonTitle')}</p>
                <p className="mt-0.5">{order.paymentRejectionReason}</p>
              </div>
            </div>
          ) : null}

          {needsReceipt && shop.cardNumber ? (
            <div className="space-y-2 rounded-xl border border-border bg-background p-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <p className="text-[11px] text-muted-foreground">
                    {t('storefront.checkout.cardNumberLabel')}
                  </p>
                  <p className="tabular-nums text-base font-bold tracking-wider text-foreground" dir="ltr">
                    {formatCardNumberDisplay(shop.cardNumber)}
                  </p>
                </div>
                <Button type="button" variant="outline" size="sm" onClick={() => void handleCopyCard()}>
                  {cardCopied ? (
                    <Check className="size-4 text-success" aria-hidden="true" />
                  ) : (
                    <Copy className="size-4" aria-hidden="true" />
                  )}
                  <span>
                    {cardCopied
                      ? t('storefront.checkout.cardCopied')
                      : t('storefront.checkout.copyCardNumber')}
                  </span>
                </Button>
              </div>

              <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
                {shop.cardHolderName ? (
                  <span>
                    {t('storefront.checkout.cardHolderLabel')}:{' '}
                    <strong className="text-foreground">{shop.cardHolderName}</strong>
                  </span>
                ) : null}
                {shop.bankName ? (
                  <span>
                    {t('storefront.checkout.bankNameLabel')}:{' '}
                    <strong className="text-foreground">{shop.bankName}</strong>
                  </span>
                ) : null}
              </div>
            </div>
          ) : null}

          {needsReceipt ? (
            <form onSubmit={handleReceiptSubmit} className="space-y-3 border-t border-border pt-3">
              <div>
                <p className="text-sm font-semibold text-foreground">
                  {order.paymentStatus === 'Rejected'
                    ? t('storefront.checkout.reuploadReceiptTitle')
                    : t('storefront.checkout.uploadReceiptTitle')}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {t('storefront.checkout.uploadReceiptDesc')}
                </p>
              </div>

              <input
                type="file"
                accept="image/jpeg,image/png,image/webp,image/gif"
                onChange={(e) => handleReceiptFileChange(e.target.files?.[0] ?? null)}
                className="block w-full cursor-pointer text-xs text-muted-foreground file:me-3 file:min-h-11 file:cursor-pointer file:rounded-lg file:border-0 file:bg-secondary file:px-3 file:text-xs file:font-medium file:text-secondary-foreground"
              />

              {receiptError ? (
                <p role="alert" className="text-xs text-destructive">
                  {receiptError}
                </p>
              ) : null}

              <Button
                type="submit"
                disabled={!receiptFile || receiptMutation.isPending}
                className="w-full"
              >
                <Upload className="size-4" aria-hidden="true" />
                <span>
                  {receiptMutation.isPending
                    ? t('storefront.checkout.uploadingReceipt')
                    : t('storefront.checkout.uploadReceiptCta')}
                </span>
              </Button>
            </form>
          ) : null}

          {order.paymentStatus === 'ReceiptSubmitted' ? (
            <p
              role="status"
              className="rounded-lg border border-primary/30 bg-primary/10 px-3 py-2 text-xs text-primary"
            >
              {t('storefront.checkout.receiptSubmittedBanner')}
            </p>
          ) : null}

          {order.receiptUrl ? (
            <div className="space-y-2 border-t border-border pt-3">
              <div className="overflow-hidden rounded-lg border border-border bg-surface-elevated">
                <img
                  src={order.receiptUrl}
                  alt={t('orders.receiptImageAlt')}
                  className="max-h-60 w-full object-contain"
                />
              </div>
              <Button asChild variant="outline" size="sm" className="w-full">
                <a href={order.receiptUrl} target="_blank" rel="noopener noreferrer">
                  <ExternalLink className="size-4" aria-hidden="true" />
                  <span>{t('orders.viewReceipt')}</span>
                </a>
              </Button>
            </div>
          ) : null}
        </Card>
      ) : null}

      {/* Shipment Tracking Card */}
      {order.carrierName || order.trackingCode ? (
        <Card className="space-y-2.5 border-success/30">
          <div className="flex items-center gap-2">
            <Truck className="size-4 text-success" aria-hidden="true" />
            <h2 className="text-sm font-bold text-foreground">{t('orders.trackingTitle')}</h2>
          </div>

          <div className="space-y-1.5 text-xs">
            {order.carrierName ? (
              <p className="text-foreground">
                <span className="text-muted-foreground">{t('orders.carrierLabel')}: </span>
                <span className="font-semibold">{order.carrierName}</span>
              </p>
            ) : null}
            {order.trackingCode ? (
              <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-2">
                <span className="tabular-nums font-bold text-foreground" dir="ltr">
                  {order.trackingCode}
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => void handleCopyTracking()}
                >
                  {trackingCopied ? (
                    <Check className="size-3.5 text-success" aria-hidden="true" />
                  ) : (
                    <Copy className="size-3.5" aria-hidden="true" />
                  )}
                  <span>
                    {trackingCopied ? t('orders.trackingCopied') : t('orders.copyTrackingCode')}
                  </span>
                </Button>
              </div>
            ) : null}
            {order.shippedAt ? (
              <p className="text-muted-foreground">{formatDateTime(order.shippedAt, lang)}</p>
            ) : null}
          </div>
        </Card>
      ) : null}

      {/* Delivery & Shipping Info */}
      {order.shippingMethodNameSnapshot || order.shippingAddressSnapshot ? (
        <Card className="space-y-2">
          <div className="flex items-center gap-2">
            <MapPin className="size-4 text-primary" aria-hidden="true" />
            <h2 className="text-sm font-bold text-foreground">{t('orders.shippingInfoTitle')}</h2>
          </div>

          <div className="space-y-1 text-xs">
            {order.shippingMethodNameSnapshot ? (
              <p className="text-foreground">
                <span className="text-muted-foreground">{t('orders.shippingMethodLabel')}: </span>
                <span className="font-medium">{order.shippingMethodNameSnapshot}</span>
              </p>
            ) : null}
            {order.recipientName || order.recipientPhone ? (
              <p className="text-foreground">
                <span className="text-muted-foreground">{t('orders.recipientLabel')}: </span>
                <span>{order.recipientName}</span>
                {order.recipientPhone ? (
                  <span className="tabular-nums ms-2" dir="ltr">
                    {order.recipientPhone}
                  </span>
                ) : null}
              </p>
            ) : null}
            {order.shippingAddressSnapshot ? (
              <p className="text-foreground">
                <span className="text-muted-foreground">{t('orders.addressLabel')}: </span>
                <span>{order.shippingAddressSnapshot}</span>
              </p>
            ) : null}
            {order.shippingPostalCode ? (
              <p className="tabular-nums text-muted-foreground">
                {t('orders.postalCodeLabel')}: <span dir="ltr">{order.shippingPostalCode}</span>
              </p>
            ) : null}
          </div>
        </Card>
      ) : null}

      {/* Order items */}
      <Card className="space-y-2.5">
        <h2 className="text-sm font-semibold text-foreground">{t('orders.itemsTitle')}</h2>
        <div className="divide-y divide-border">
          {order.items.map((item) => (
            <div key={item.id} className="flex items-center justify-between gap-3 py-2.5 text-sm">
              <div className="min-w-0">
                <p className="truncate font-medium text-foreground">{item.productNameSnapshot}</p>
                <p className="tabular-nums text-xs text-muted-foreground">
                  {t('orders.quantity', { count: formatNumber(toNumber(item.quantity), lang) })} ×{' '}
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

      {/* Customer cancel action */}
      {canCancel ? (
        <Card className="space-y-3 border-destructive/30">
          {showCancelBox ? (
            <div className="space-y-3">
              <div className="flex items-start gap-2 text-xs text-destructive">
                <AlertTriangle className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
                <div>
                  <p className="font-semibold">{t('storefront.cancelOrderConfirmTitle')}</p>
                  <p className="mt-0.5 text-muted-foreground">
                    {order.status === 'Confirmed'
                      ? t('storefront.checkout.cancelOrderConfirmAutoDesc')
                      : t('storefront.cancelOrderConfirmDesc')}
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
              onClick={() => {
                triggerHaptic('warning');
                setShowCancelBox(true);
              }}
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
          {order.statusHistory.map((entry) => {
            const noteText =
              entry.fromStatus === null
                ? t('orders.orderPlacedEvent')
                : entry.reason === 'auto_confirmed'
                  ? t('orders.autoConfirmedEvent')
                  : entry.reason === 'payment_verified'
                    ? t('orders.paymentVerifiedEvent')
                    : entry.reason === 'payment_expired'
                      ? t('orders.paymentExpiredEvent')
                      : entry.reason && entry.reason !== 'Order placed.'
                        ? entry.reason
                        : null;

            return (
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
                {noteText ? <p className="mt-1 text-foreground">{noteText}</p> : null}
              </li>
            );
          })}
        </ol>
      </Card>
    </div>
  );
}
