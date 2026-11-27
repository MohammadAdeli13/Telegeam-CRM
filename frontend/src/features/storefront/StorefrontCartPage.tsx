import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  AlertTriangle,
  CheckCircle2,
  Minus,
  Package,
  Plus,
  RefreshCw,
  ShoppingBag,
  Trash2,
} from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate, useOutletContext, useParams } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { formatMoney, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { EmptyState } from '@/shared/ui/state-views';
import { Textarea } from '@/shared/ui/textarea';
import type { components } from '@/types/api';
import { useStorefrontCart } from './cart-context';
import { isValidOptionalE164Phone } from './cart-store';

type StorefrontShop = components['schemas']['StorefrontShopDto'];
type CartValidationResponse = components['schemas']['CartValidationResponse'];

export function StorefrontCartPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { shopSlug = '' } = useParams();
  const shop = useOutletContext<StorefrontShop>();
  const { status: authStatus } = useAuth();
  const { items, subtotal, idempotencyKey, setQuantity, removeItem, clearCart } =
    useStorefrontCart();

  const [phone, setPhone] = useState('');
  const [note, setNote] = useState('');
  const [validation, setValidation] = useState<CartValidationResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const lang = normalizeLanguage(i18n.language);
  const isAuthenticated = authStatus === 'authenticated';

  const validateMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await apiClient.POST('/api/v1/storefront/{shopSlug}/cart/validate', {
        params: { path: { shopSlug } },
        body: {
          items: items.map((item) => ({
            productId: item.productId,
            quantity: item.quantity,
          })),
        },
      });
      if (error || !data) {
        throw error ?? new Error('Cart validation failed');
      }
      return data;
    },
    onSuccess: (data) => {
      setValidation(data);
    },
  });

  // Reset stale server validation whenever cart lines change.
  const cartSignature = items.map((i) => `${i.productId}:${i.quantity}`).join('|');
  useEffect(() => {
    setValidation(null);
    setFormError(null);
  }, [cartSignature]);

  const placeOrderMutation = useMutation({
    mutationFn: async () => {
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
          phone: phone.trim() || null,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Order placement failed');
      }
      return data;
    },
    onSuccess: (order) => {
      clearCart();
      void queryClient.invalidateQueries({ queryKey: ['storefront-orders', shopSlug] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-products', shopSlug] });
      void navigate(`/s/${shopSlug}/orders/${order.id}?placed=1`, { replace: true });
    },
    onError: (err) => {
      const code = extractProblemCode(err);
      if (code === 'insufficient_stock' || code === 'product_not_available') {
        validateMutation.mutate();
      }
      setFormError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const handleCheckout = (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    if (!isValidOptionalE164Phone(phone)) {
      setFormError(t('storefront.phoneInvalid'));
      return;
    }

    placeOrderMutation.mutate();
  };

  if (items.length === 0) {
    return (
      <EmptyState
        title={t('storefront.cartTitle')}
        description={t('storefront.cartEmpty')}
        action={
          <Button asChild>
            <Link to={`/s/${shopSlug}`}>{t('storefront.continueShopping')}</Link>
          </Button>
        }
      />
    );
  }

  const validationByProduct = new Map(
    validation?.items.map((line) => [line.productId, line]) ?? [],
  );

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('storefront.cartTitle')}
        </h1>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={clearCart}
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

          return (
            <Card
              key={item.productId}
              className={hasIssue ? 'space-y-2.5 border-destructive/50' : 'space-y-2.5'}
            >
              <div className="flex items-start gap-3">
                <Link
                  to={`/s/${shopSlug}/products/${item.productId}`}
                  className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-surface-elevated"
                >
                  {item.imageUrl ? (
                    <img
                      src={item.imageUrl}
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
                    {formatMoney(unitPrice, shop.currency, lang)} × {item.quantity}
                  </p>
                  <p className="tabular-nums text-sm font-bold text-primary">
                    {formatMoney(unitPrice * item.quantity, shop.currency, lang)}
                  </p>
                </div>

                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={t('storefront.removeItem')}
                  onClick={() => removeItem(item.productId)}
                  className="size-9 shrink-0 text-destructive hover:bg-destructive/10"
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                </Button>
              </div>

              <div className="flex flex-wrap items-center justify-between gap-2 border-t border-border pt-2">
                <div className="flex items-center gap-2 rounded-lg border border-border bg-background p-1">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t('storefront.decreaseQty')}
                    onClick={() => setQuantity(item.productId, item.quantity - 1)}
                    className="size-9"
                  >
                    <Minus className="size-3.5" aria-hidden="true" />
                  </Button>
                  <span className="tabular-nums min-w-7 text-center text-sm font-bold text-foreground">
                    {item.quantity}
                  </span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t('storefront.increaseQty')}
                    disabled={availableStock > 0 && item.quantity >= availableStock}
                    onClick={() => setQuantity(item.productId, item.quantity + 1)}
                    className="size-9"
                  >
                    <Plus className="size-3.5" aria-hidden="true" />
                  </Button>
                </div>

                {hasIssue && serverLine ? (
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="flex items-center gap-1 text-xs text-destructive">
                      <AlertTriangle className="size-3.5 shrink-0" aria-hidden="true" />
                      <span>
                        {serverLine.issue === 'insufficient_stock'
                          ? t('storefront.issueOutOfStock', { available: availableStock })
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
                        {t('storefront.adjustToAvailable', { available: availableStock })}
                      </Button>
                    ) : null}
                  </div>
                ) : null}
              </div>
            </Card>
          );
        })}
      </div>

      {/* Totals & Validation */}
      <Card className="space-y-3">
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            {validation ? t('storefront.verifiedTotal') : t('storefront.subtotal')}
          </span>
          <span className="tabular-nums text-lg font-bold text-primary">
            {formatMoney(validation ? validation.totalAmount : subtotal, shop.currency, lang)}
          </span>
        </div>

        {isAuthenticated ? (
          <div className="space-y-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={validateMutation.isPending}
              onClick={() => validateMutation.mutate()}
              className="w-full"
            >
              <RefreshCw className="size-4" aria-hidden="true" />
              <span>
                {validateMutation.isPending
                  ? t('storefront.validatingCart')
                  : t('storefront.validateCart')}
              </span>
            </Button>

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
            <div className="space-y-1.5">
              <Label htmlFor="checkout-phone">{t('storefront.phoneLabel')}</Label>
              <Input
                id="checkout-phone"
                dir="ltr"
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                placeholder={t('storefront.phonePlaceholder')}
              />
            </div>

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
              disabled={placeOrderMutation.isPending || (validation !== null && !validation.isValid)}
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
    </div>
  );
}
