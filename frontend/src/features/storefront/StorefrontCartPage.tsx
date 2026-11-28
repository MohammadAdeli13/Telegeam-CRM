import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  Banknote,
  CheckCircle2,
  CreditCard,
  MapPin,
  Minus,
  Package,
  Pencil,
  Plus,
  RefreshCw,
  ShoppingBag,
  ShoppingCart,
  Star,
  Trash2,
  Truck,
} from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useOutletContext, useParams } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import {
  computeEffectiveShippingCost,
  formatMoney,
  formatNumber,
  isValidOptionalPhone,
  normalizePhoneToE164,
  toNumber,
} from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { triggerHaptic } from '@/shared/lib/telegram';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { ConfirmDialog } from '@/shared/ui/confirm-dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { EmptyState } from '@/shared/ui/state-views';
import { Textarea } from '@/shared/ui/textarea';
import type { components } from '@/types/api';
import { useStorefrontCart } from './cart-context';
import { CustomerAddressModal } from './CustomerAddressModal';

type StorefrontShop = components['schemas']['StorefrontShopDto'];
type CustomerAddressDto = components['schemas']['CustomerAddressDto'];

export function StorefrontCartPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { shopSlug = '' } = useParams();
  const shop = useOutletContext<StorefrontShop>();
  const { status: authStatus } = useAuth();
  const { items, subtotal, idempotencyKey, setQuantity, clearCart } = useStorefrontCart();

  const [phone, setPhone] = useState('');
  const [note, setNote] = useState('');
  const [selectedAddressId, setSelectedAddressId] = useState<string | null>(null);
  const [selectedShippingId, setSelectedShippingId] = useState<string | null>(null);
  const [selectedPaymentMethod, setSelectedPaymentMethod] = useState<
    'CardToCard' | 'PayOnDelivery' | null
  >(() => {
    if (shop.enableCardToCard) {
      return 'CardToCard';
    }
    if (shop.enablePayOnDelivery) {
      return 'PayOnDelivery';
    }
    return null;
  });

  const [addressModal, setAddressModal] = useState<{
    open: boolean;
    editing: CustomerAddressDto | null;
  }>({ open: false, editing: null });
  const [deletingAddress, setDeletingAddress] = useState<CustomerAddressDto | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const lang = normalizeLanguage(i18n.language);
  const isAuthenticated = authStatus === 'authenticated';
  const cartSignature = items.map((i) => `${i.productId}:${i.quantity}`).join('|');

  const shippingQuery = useQuery({
    queryKey: ['storefront-shipping-methods', shopSlug],
    enabled: Boolean(shopSlug),
    queryFn: async () => {
      const { data, error } = await apiClient.GET(
        '/api/v1/storefront/{shopSlug}/shipping-methods',
        { params: { path: { shopSlug } } },
      );
      if (error || !data) {
        throw new Error('Failed to load shipping methods');
      }
      return data;
    },
  });

  const addressesQuery = useQuery({
    queryKey: ['storefront-addresses', shopSlug],
    enabled: isAuthenticated && Boolean(shopSlug),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/storefront/{shopSlug}/addresses', {
        params: { path: { shopSlug } },
      });
      if (error || !data) {
        throw new Error('Failed to load addresses');
      }
      return data;
    },
  });

  const shippingMethods = shippingQuery.data ?? [];
  const addresses = addressesQuery.data ?? [];

  // Auto-select default address when loaded.
  useEffect(() => {
    if (addresses.length === 0) {
      setSelectedAddressId(null);
      return;
    }
    if (selectedAddressId && addresses.some((a) => a.id === selectedAddressId)) {
      return;
    }
    const def = addresses.find((a) => a.isDefault) ?? addresses[0];
    if (def) {
      setSelectedAddressId(def.id);
    }
  }, [addresses, selectedAddressId]);

  // Auto-select first shipping method when shop defines shipping options.
  useEffect(() => {
    if (shippingMethods.length === 0) {
      setSelectedShippingId(null);
      return;
    }
    if (selectedShippingId && shippingMethods.some((m) => m.id === selectedShippingId)) {
      return;
    }
    const first = shippingMethods[0];
    if (first) {
      setSelectedShippingId(first.id);
    }
  }, [shippingMethods, selectedShippingId]);

  // Keep payment method valid if shop config updates.
  useEffect(() => {
    if (selectedPaymentMethod === 'CardToCard' && !shop.enableCardToCard) {
      setSelectedPaymentMethod(shop.enablePayOnDelivery ? 'PayOnDelivery' : null);
    } else if (selectedPaymentMethod === 'PayOnDelivery' && !shop.enablePayOnDelivery) {
      setSelectedPaymentMethod(shop.enableCardToCard ? 'CardToCard' : null);
    } else if (!selectedPaymentMethod) {
      if (shop.enableCardToCard) {
        setSelectedPaymentMethod('CardToCard');
      } else if (shop.enablePayOnDelivery) {
        setSelectedPaymentMethod('PayOnDelivery');
      }
    }
  }, [selectedPaymentMethod, shop.enableCardToCard, shop.enablePayOnDelivery]);

  const validateQuery = useQuery({
    queryKey: [
      'storefront-cart-validate',
      shopSlug,
      cartSignature,
      selectedShippingId ?? 'none',
    ],
    enabled: isAuthenticated && items.length > 0 && Boolean(shopSlug),
    queryFn: async () => {
      const { data, error } = await apiClient.POST('/api/v1/storefront/{shopSlug}/cart/validate', {
        params: { path: { shopSlug } },
        body: {
          items: items.map((item) => ({
            productId: item.productId,
            quantity: item.quantity,
          })),
          shippingMethodId: selectedShippingId,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Cart validation failed');
      }
      return data;
    },
  });

  const setDefaultAddressMutation = useMutation({
    mutationFn: async (addressId: string) => {
      const { error } = await apiClient.POST(
        '/api/v1/storefront/{shopSlug}/addresses/{id}/default',
        { params: { path: { shopSlug, id: addressId } } },
      );
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('selection');
      void queryClient.invalidateQueries({ queryKey: ['storefront-addresses', shopSlug] });
    },
  });

  const deleteAddressMutation = useMutation({
    mutationFn: async (addressId: string) => {
      const { error } = await apiClient.DELETE('/api/v1/storefront/{shopSlug}/addresses/{id}', {
        params: { path: { shopSlug, id: addressId } },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      setDeletingAddress(null);
      void queryClient.invalidateQueries({ queryKey: ['storefront-addresses', shopSlug] });
    },
  });

  const validation = validateQuery.data ?? null;

  const placeOrderMutation = useMutation({
    mutationFn: async () => {
      const normalizedPhone = normalizePhoneToE164(phone);
      const { data, error } = await apiClient.POST('/api/v1/storefront/{shopSlug}/orders', {
        params: {
          path: { shopSlug },
          header: { 'Idempotency-Key': idempotencyKey },
        },
        body: {
          items: items.map((item) => ({
            productId: item.productId,
            quantity: item.quantity,
          })),
          note: note.trim() || null,
          phone: normalizedPhone || null,
          addressId: selectedAddressId,
          shippingMethodId: selectedShippingId,
          paymentMethod: selectedPaymentMethod,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Order placement failed');
      }
      return data;
    },
    onSuccess: (order) => {
      triggerHaptic('success');
      clearCart();
      void queryClient.invalidateQueries({ queryKey: ['storefront-orders', shopSlug] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-products', shopSlug] });
      void navigate(`/s/${shopSlug}/orders/${order.id}?placed=1`, { replace: true });
    },
    onError: (err) => {
      triggerHaptic('error');
      const code = extractProblemCode(err);
      if (code === 'insufficient_stock' || code === 'product_not_available') {
        void validateQuery.refetch();
      }
      setFormError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const handleCheckout = (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    if (!isValidOptionalPhone(phone)) {
      triggerHaptic('error');
      setFormError(t('storefront.phoneInvalid'));
      return;
    }

    placeOrderMutation.mutate();
  };

  if (items.length === 0) {
    return (
      <div className="mx-auto max-w-lg py-4">
        <EmptyState
          icon={<ShoppingCart className="size-6" aria-hidden="true" />}
          title={t('storefront.cartTitle')}
          description={t('storefront.cartEmpty')}
          action={
            <Button asChild>
              <Link to={`/s/${shopSlug}`}>{t('storefront.continueShopping')}</Link>
            </Button>
          }
        />
      </div>
    );
  }

  const validationByProduct = new Map(
    validation?.items.map((line) => [line.productId, line]) ?? [],
  );

  const selectedShipping = shippingMethods.find((m) => m.id === selectedShippingId) ?? null;
  const effectiveSubtotal = validation ? toNumber(validation.subtotalAmount) : subtotal;
  const effectiveShipping = validation
    ? toNumber(validation.shippingAmount)
    : selectedShipping
      ? computeEffectiveShippingCost(selectedShipping, effectiveSubtotal)
      : 0;
  const isPayAtDest = validation
    ? validation.isShippingPayAtDestination
    : Boolean(selectedShipping?.isPayAtDestination);
  const effectiveTotal = validation
    ? toNumber(validation.totalAmount)
    : effectiveSubtotal + effectiveShipping;

  const showPaymentSection = shop.enableCardToCard || shop.enablePayOnDelivery;

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('storefront.cartTitle')}
        </h1>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => {
            triggerHaptic('warning');
            clearCart();
          }}
          className="text-destructive hover:bg-destructive/10"
        >
          <Trash2 className="size-4" aria-hidden="true" />
          <span>{t('storefront.clearCart')}</span>
        </Button>
      </div>

      {/* Cart items */}
      <div className="space-y-2.5">
        {items.map((item) => {
          const title = lang === 'en' && item.nameEn ? item.nameEn : item.name;
          const serverLine = validationByProduct.get(item.productId);
          const availableStock = serverLine ? toNumber(serverLine.availableStock) : item.stock;
          const unitPrice = serverLine ? toNumber(serverLine.unitPrice) : item.unitPrice;
          const hasIssue = Boolean(serverLine && !serverLine.isAvailable);
          const thumbUrl = item.imageUrl ?? shop.defaultProductImageUrl ?? null;

          return (
            <Card
              key={item.productId}
              className={hasIssue ? 'space-y-3 border-destructive/50' : 'space-y-3'}
            >
              <div className="flex items-start gap-3">
                <Link
                  to={`/s/${shopSlug}/products/${item.productId}`}
                  className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-surface-elevated"
                >
                  {thumbUrl ? (
                    <img
                      src={thumbUrl}
                      alt={title}
                      loading="lazy"
                      className="size-full object-cover"
                    />
                  ) : (
                    <Package className="size-6 text-muted-foreground" aria-hidden="true" />
                  )}
                </Link>

                <div className="min-w-0 flex-1 space-y-1">
                  <Link
                    to={`/s/${shopSlug}/products/${item.productId}`}
                    className="line-clamp-2 text-sm font-semibold text-foreground hover:text-primary"
                  >
                    {title}
                  </Link>
                  <p className="tabular-nums text-xs text-muted-foreground">
                    <span>{formatMoney(unitPrice, shop.currency, lang)}</span>
                    <span className="mx-1">×</span>
                    <span>{formatNumber(item.quantity, lang)}</span>
                  </p>
                  <p className="tabular-nums text-sm font-bold text-primary">
                    {formatMoney(unitPrice * item.quantity, shop.currency, lang)}
                  </p>
                </div>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border pt-2.5">
                <div className="flex items-center gap-1 rounded-lg border border-border bg-background p-0.5">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={
                      item.quantity === 1
                        ? t('storefront.removeItem')
                        : t('storefront.decreaseQty')
                    }
                    onClick={() => {
                      triggerHaptic('light');
                      setQuantity(item.productId, item.quantity - 1);
                    }}
                    className={item.quantity === 1 ? 'text-destructive' : undefined}
                  >
                    {item.quantity === 1 ? (
                      <Trash2 className="size-4" aria-hidden="true" />
                    ) : (
                      <Minus className="size-4" aria-hidden="true" />
                    )}
                  </Button>
                  <span className="tabular-nums min-w-8 text-center text-sm font-bold text-foreground">
                    {formatNumber(item.quantity, lang)}
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t('storefront.increaseQty')}
                    disabled={availableStock > 0 && item.quantity >= availableStock}
                    onClick={() => {
                      triggerHaptic('light');
                      setQuantity(item.productId, item.quantity + 1);
                    }}
                  >
                    <Plus className="size-4" aria-hidden="true" />
                  </Button>
                </div>

                {hasIssue && serverLine ? (
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="flex items-center gap-1 text-xs text-destructive">
                      <AlertTriangle className="size-3.5 shrink-0" aria-hidden="true" />
                      <span>
                        {serverLine.issue === 'insufficient_stock'
                          ? t('storefront.issueOutOfStock', {
                              available: formatNumber(availableStock, lang),
                            })
                          : t('storefront.issueUnavailable')}
                      </span>
                    </span>
                    {serverLine.issue === 'insufficient_stock' && availableStock > 0 ? (
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setQuantity(item.productId, availableStock)}
                      >
                        {t('storefront.adjustToAvailable', {
                          available: formatNumber(availableStock, lang),
                        })}
                      </Button>
                    ) : null}
                  </div>
                ) : null}
              </div>
            </Card>
          );
        })}
      </div>

      {/* Delivery Addresses (authenticated) */}
      {isAuthenticated ? (
        <Card className="space-y-3">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="flex items-center gap-2">
              <MapPin className="size-4 text-primary" aria-hidden="true" />
              <h2 className="text-sm font-bold text-foreground">
                {t('storefront.addresses.sectionTitle')}
              </h2>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setAddressModal({ open: true, editing: null })}
            >
              <Plus className="size-4" aria-hidden="true" />
              <span>{t('storefront.addresses.addNew')}</span>
            </Button>
          </div>

          {addresses.length === 0 ? (
            <p className="text-xs text-muted-foreground">{t('storefront.addresses.empty')}</p>
          ) : (
            <div className="space-y-2">
              {addresses.map((addr) => {
                const selected = selectedAddressId === addr.id;
                return (
                  <div
                    key={addr.id}
                    className={cn(
                      'rounded-xl border p-3 transition-colors',
                      selected
                        ? 'border-primary bg-primary/5'
                        : 'border-border bg-background hover:bg-surface-elevated',
                    )}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <button
                        type="button"
                        onClick={() => {
                          triggerHaptic('selection');
                          setSelectedAddressId(addr.id);
                        }}
                        className="flex min-w-0 flex-1 cursor-pointer items-start gap-2.5 text-start"
                      >
                        <span
                          className={cn(
                            'mt-1 flex size-4 shrink-0 items-center justify-center rounded-full border-2',
                            selected ? 'border-primary' : 'border-muted-foreground/50',
                          )}
                          aria-hidden="true"
                        >
                          {selected ? (
                            <span className="size-2 rounded-full bg-primary" />
                          ) : null}
                        </span>
                        <div className="min-w-0 space-y-0.5">
                          <div className="flex flex-wrap items-center gap-1.5">
                            <span className="text-sm font-semibold text-foreground">
                              {addr.title}
                            </span>
                            {addr.isDefault ? (
                              <Badge variant="success">{t('storefront.addresses.defaultBadge')}</Badge>
                            ) : null}
                          </div>
                          <p className="text-xs text-foreground">
                            {addr.recipientName} ·{' '}
                            <span className="tabular-nums" dir="ltr">
                              {addr.phone}
                            </span>
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {addr.province}، {addr.city}، {addr.addressLine}
                            {addr.plaque
                              ? `، ${t('storefront.addresses.plaqueShort', { value: addr.plaque })}`
                              : ''}
                            {addr.unit
                              ? `، ${t('storefront.addresses.unitShort', { value: addr.unit })}`
                              : ''}
                          </p>
                          {addr.postalCode ? (
                            <p className="tabular-nums text-[11px] text-muted-foreground">
                              {t('orders.postalCodeLabel')}: <span dir="ltr">{addr.postalCode}</span>
                            </p>
                          ) : null}
                        </div>
                      </button>

                      <div className="flex shrink-0 items-center gap-1">
                        {!addr.isDefault ? (
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={t('storefront.addresses.setDefault')}
                            onClick={() => setDefaultAddressMutation.mutate(addr.id)}
                          >
                            <Star className="size-4 text-muted-foreground" aria-hidden="true" />
                          </Button>
                        ) : null}
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          aria-label={t('common.edit')}
                          onClick={() => setAddressModal({ open: true, editing: addr })}
                        >
                          <Pencil className="size-4" aria-hidden="true" />
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          aria-label={t('common.delete')}
                          onClick={() => setDeletingAddress(addr)}
                          className="text-destructive hover:bg-destructive/10"
                        >
                          <Trash2 className="size-4" aria-hidden="true" />
                        </Button>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </Card>
      ) : null}

      {/* Shipping Methods */}
      {shippingMethods.length > 0 ? (
        <Card className="space-y-3">
          <div className="flex items-center gap-2">
            <Truck className="size-4 text-primary" aria-hidden="true" />
            <h2 className="text-sm font-bold text-foreground">
              {t('storefront.checkout.shippingSectionTitle')}
            </h2>
          </div>

          <div className="space-y-2">
            {shippingMethods.map((method) => {
              const selected = selectedShippingId === method.id;
              const cost = computeEffectiveShippingCost(method, effectiveSubtotal);
              const unlockedFree =
                !method.isPayAtDestination &&
                method.freeAboveAmount !== null &&
                method.freeAboveAmount !== undefined &&
                effectiveSubtotal >= toNumber(method.freeAboveAmount);

              return (
                <button
                  key={method.id}
                  type="button"
                  onClick={() => {
                    triggerHaptic('selection');
                    setSelectedShippingId(method.id);
                  }}
                  className={cn(
                    'flex w-full cursor-pointer items-start justify-between gap-3 rounded-xl border p-3 text-start transition-colors',
                    selected
                      ? 'border-primary bg-primary/5'
                      : 'border-border bg-background hover:bg-surface-elevated',
                  )}
                >
                  <div className="flex min-w-0 items-start gap-2.5">
                    <span
                      className={cn(
                        'mt-1 flex size-4 shrink-0 items-center justify-center rounded-full border-2',
                        selected ? 'border-primary' : 'border-muted-foreground/50',
                      )}
                      aria-hidden="true"
                    >
                      {selected ? <span className="size-2 rounded-full bg-primary" /> : null}
                    </span>
                    <div className="min-w-0 space-y-0.5">
                      <p className="text-sm font-semibold text-foreground">{method.name}</p>
                      {method.estimatedDaysText ? (
                        <p className="text-xs text-muted-foreground">{method.estimatedDaysText}</p>
                      ) : null}
                      {method.description ? (
                        <p className="text-xs text-muted-foreground">{method.description}</p>
                      ) : null}
                      {unlockedFree ? (
                        <p className="text-xs font-medium text-success">
                          {t('storefront.checkout.freeUnlocked')}
                        </p>
                      ) : !method.isPayAtDestination && method.freeAboveAmount ? (
                        <p className="text-[11px] text-muted-foreground">
                          {t('storefront.checkout.freeAboveHint', {
                            amount: formatMoney(method.freeAboveAmount, shop.currency, lang),
                          })}
                        </p>
                      ) : null}
                    </div>
                  </div>

                  <span className="tabular-nums shrink-0 text-xs font-bold text-primary">
                    {method.isPayAtDestination
                      ? t('orders.payAtDestination')
                      : cost === 0
                        ? t('orders.shippingFree')
                        : formatMoney(cost, shop.currency, lang)}
                  </span>
                </button>
              );
            })}
          </div>
        </Card>
      ) : null}

      {/* Payment Methods */}
      {isAuthenticated && showPaymentSection ? (
        <Card className="space-y-3">
          <div className="flex items-center gap-2">
            <CreditCard className="size-4 text-primary" aria-hidden="true" />
            <h2 className="text-sm font-bold text-foreground">
              {t('storefront.checkout.paymentSectionTitle')}
            </h2>
          </div>

          <div className="space-y-2">
            {shop.enableCardToCard ? (
              <button
                type="button"
                onClick={() => {
                  triggerHaptic('selection');
                  setSelectedPaymentMethod('CardToCard');
                }}
                className={cn(
                  'flex w-full cursor-pointer items-start gap-2.5 rounded-xl border p-3 text-start transition-colors',
                  selectedPaymentMethod === 'CardToCard'
                    ? 'border-primary bg-primary/5'
                    : 'border-border bg-background hover:bg-surface-elevated',
                )}
              >
                <CreditCard className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-foreground">
                    {t('orders.paymentMethods.CardToCard')}
                  </p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {t('storefront.checkout.cardToCardDesc')}
                  </p>
                </div>
              </button>
            ) : null}

            {shop.enablePayOnDelivery ? (
              <button
                type="button"
                onClick={() => {
                  triggerHaptic('selection');
                  setSelectedPaymentMethod('PayOnDelivery');
                }}
                className={cn(
                  'flex w-full cursor-pointer items-start gap-2.5 rounded-xl border p-3 text-start transition-colors',
                  selectedPaymentMethod === 'PayOnDelivery'
                    ? 'border-primary bg-primary/5'
                    : 'border-border bg-background hover:bg-surface-elevated',
                )}
              >
                <Banknote className="mt-0.5 size-4 shrink-0 text-primary" aria-hidden="true" />
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-foreground">
                    {t('orders.paymentMethods.PayOnDelivery')}
                  </p>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {t('storefront.checkout.payOnDeliveryDesc')}
                  </p>
                </div>
              </button>
            ) : null}
          </div>
        </Card>
      ) : null}

      {/* Totals & Auto-Validation */}
      <Card className="space-y-3">
        <div className="space-y-2 text-sm">
          {selectedShipping ? (
            <>
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">{t('orders.subtotal')}</span>
                <span className="tabular-nums font-medium text-foreground">
                  {formatMoney(effectiveSubtotal, shop.currency, lang)}
                </span>
              </div>
              <div className="flex items-center justify-between text-xs">
                <span className="text-muted-foreground">{t('orders.shippingCost')}</span>
                <span className="tabular-nums font-medium text-foreground">
                  {isPayAtDest
                    ? t('orders.payAtDestination')
                    : effectiveShipping === 0
                      ? t('orders.shippingFree')
                      : formatMoney(effectiveShipping, shop.currency, lang)}
                </span>
              </div>
            </>
          ) : null}

          <div className="flex items-center justify-between border-t border-border pt-2">
            <span className="text-muted-foreground">
              {validation ? t('storefront.verifiedTotal') : t('storefront.subtotal')}
            </span>
            <span className="tabular-nums text-lg font-bold text-primary">
              {formatMoney(effectiveTotal, shop.currency, lang)}
            </span>
          </div>
        </div>

        {isAuthenticated ? (
          <div className="space-y-2">
            {validation ? (
              <p
                role="status"
                className={
                  validation.isValid
                    ? 'flex items-center gap-1.5 text-xs text-success'
                    : 'flex items-center gap-1.5 text-xs text-destructive'
                }
              >
                {validation.isValid ? (
                  <CheckCircle2 className="size-4 shrink-0" aria-hidden="true" />
                ) : (
                  <AlertTriangle className="size-4 shrink-0" aria-hidden="true" />
                )}
                <span>
                  {validation.isValid ? t('storefront.cartValid') : t('storefront.cartHasIssues')}
                </span>
              </p>
            ) : null}

            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={validateQuery.isFetching}
              onClick={() => void validateQuery.refetch()}
              className="w-full"
            >
              <RefreshCw
                className={validateQuery.isFetching ? 'size-4 animate-spin' : 'size-4'}
                aria-hidden="true"
              />
              <span>
                {validateQuery.isFetching
                  ? t('storefront.validatingCart')
                  : t('storefront.validateCart')}
              </span>
            </Button>
          </div>
        ) : null}
      </Card>

      {/* Checkout form or Sign-in prompt */}
      {isAuthenticated ? (
        <Card>
          <CardHeader>
            <CardTitle>{t('storefront.checkoutTitle')}</CardTitle>
          </CardHeader>

          <form onSubmit={handleCheckout} className="space-y-3.5" noValidate>
            {!selectedAddressId ? (
              <div className="space-y-1.5">
                <Label htmlFor="checkout-phone">{t('storefront.phoneLabel')}</Label>
                <Input
                  id="checkout-phone"
                  dir="ltr"
                  type="tel"
                  inputMode="tel"
                  value={phone}
                  onChange={(e) => {
                    setPhone(e.target.value);
                    if (formError) {
                      setFormError(null);
                    }
                  }}
                  placeholder={t('storefront.phonePlaceholder')}
                />
              </div>
            ) : null}

            <div className="space-y-1.5">
              <Label htmlFor="checkout-note">{t('storefront.noteLabel')}</Label>
              <Textarea
                id="checkout-note"
                maxLength={2000}
                value={note}
                onChange={(e) => setNote(e.target.value)}
                placeholder={t('storefront.notePlaceholder')}
              />
            </div>

            {formError ? (
              <p
                role="alert"
                className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
              >
                {formError}
              </p>
            ) : null}

            <Button
              type="submit"
              disabled={
                placeOrderMutation.isPending || (validation !== null && !validation.isValid)
              }
              className="w-full"
            >
              <ShoppingBag className="size-4" aria-hidden="true" />
              <span>
                {placeOrderMutation.isPending
                  ? t('storefront.placingOrder')
                  : t('storefront.placeOrderCta')}
              </span>
            </Button>
          </form>
        </Card>
      ) : (
        <Card className="space-y-3 border-warning/40">
          <CardHeader className="mb-0">
            <CardTitle>{t('storefront.loginToCheckoutTitle')}</CardTitle>
            <CardDescription>{t('storefront.loginToCheckoutDesc')}</CardDescription>
          </CardHeader>
          <Button asChild className="w-full">
            <Link to="/auth">{t('storefront.goToSignIn')}</Link>
          </Button>
        </Card>
      )}

      {addressModal.open ? (
        <CustomerAddressModal
          shopSlug={shopSlug}
          editing={addressModal.editing}
          onSaved={(saved) => {
            setSelectedAddressId(saved.id);
            setAddressModal({ open: false, editing: null });
          }}
          onClose={() => setAddressModal({ open: false, editing: null })}
        />
      ) : null}

      <ConfirmDialog
        open={deletingAddress !== null}
        title={t('storefront.addresses.deleteTitle')}
        description={
          deletingAddress
            ? t('storefront.addresses.confirmDelete', { title: deletingAddress.title })
            : ''
        }
        confirmLabel={t('common.delete')}
        busy={deleteAddressMutation.isPending}
        onCancel={() => setDeletingAddress(null)}
        onConfirm={() => {
          if (deletingAddress) {
            deleteAddressMutation.mutate(deletingAddress.id);
          }
        }}
      />
    </div>
  );
}
