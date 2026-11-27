import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, ArrowRight, Check, Minus, Package, Plus, ShoppingCart } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useOutletContext, useParams } from 'react-router';
import { apiClient } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import { formatMoney, toNumber } from '@/shared/lib/format';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { ErrorState, LoadingState } from '@/shared/ui/state-views';
import type { components } from '@/types/api';
import { useStorefrontCart } from './cart-context';

type StorefrontShop = components['schemas']['StorefrontShopDto'];

export function StorefrontProductDetailPage() {
  const { t, i18n } = useTranslation();
  const { shopSlug = '', productId = '' } = useParams();
  const shop = useOutletContext<StorefrontShop>();
  const { addItem, getQuantity } = useStorefrontCart();

  const [selectedImageIdx, setSelectedImageIdx] = useState(0);
  const [quantity, setQuantity] = useState(1);
  const [justAdded, setJustAdded] = useState(false);

  const lang = normalizeLanguage(i18n.language);
  const isRtl = getDirection(lang) === 'rtl';
  const BackIcon = isRtl ? ArrowRight : ArrowLeft;

  const productQuery = useQuery({
    queryKey: ['storefront-product', shopSlug, productId],
    enabled: Boolean(shopSlug && productId),
    queryFn: async () => {
      const { data, error, response } = await apiClient.GET(
        '/api/v1/storefront/{shopSlug}/products/{id}',
        { params: { path: { shopSlug, id: productId } } },
      );
      if (response.status === 404) {
        return null;
      }
      if (error || !data) {
        throw new Error('Failed to load product');
      }
      return data;
    },
  });

  if (productQuery.isLoading) {
    return <LoadingState rows={3} />;
  }

  if (productQuery.isError) {
    return <ErrorState onRetry={() => void productQuery.refetch()} />;
  }

  const product = productQuery.data;
  if (!product) {
    return (
      <div className="space-y-4">
        <ErrorState
          title={t('storefront.productNotFoundTitle')}
          message={t('storefront.productNotFoundDesc')}
        />
        <div className="flex justify-center">
          <Button asChild variant="outline">
            <Link to={`/s/${shopSlug}`}>{t('storefront.backToCatalog')}</Link>
          </Button>
        </div>
      </div>
    );
  }

  const title = lang === 'en' && product.nameEn ? product.nameEn : product.name;
  const stock = toNumber(product.stock);
  const price = toNumber(product.price);
  const inCartQty = getQuantity(product.id);
  const remainingAddable = Math.max(0, stock - inCartQty);
  const activeImage = product.images[selectedImageIdx] ?? product.images[0] ?? null;

  const handleAdd = () => {
    if (remainingAddable <= 0) {
      return;
    }
    const primaryImg = product.images.find((i) => i.isPrimary) ?? product.images[0] ?? null;
    addItem(
      {
        productId: product.id,
        name: product.name,
        nameEn: product.nameEn,
        unitPrice: price,
        stock,
        imageUrl: primaryImg?.url ?? null,
      },
      Math.min(quantity, remainingAddable),
    );
    setQuantity(1);
    setJustAdded(true);
    window.setTimeout(() => setJustAdded(false), 1800);
  };

  return (
    <div className="space-y-4">
      <Button asChild variant="ghost" size="sm" className="px-2">
        <Link to={`/s/${shopSlug}`}>
          <BackIcon className="size-4" aria-hidden="true" />
          <span>{t('storefront.backToCatalog')}</span>
        </Link>
      </Button>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        {/* Image gallery */}
        <div className="space-y-2.5">
          <Card className="overflow-hidden p-0">
            <div className="flex aspect-square w-full items-center justify-center bg-surface-elevated">
              {activeImage ? (
                <img
                  src={activeImage.url}
                  alt={activeImage.altText ?? title}
                  className="size-full object-cover"
                />
              ) : (
                <Package className="size-12 text-muted-foreground" aria-hidden="true" />
              )}
            </div>
          </Card>

          {product.images.length > 1 ? (
            <div className="flex flex-wrap gap-2">
              {product.images.map((img, idx) => (
                <button
                  key={img.id}
                  type="button"
                  onClick={() => setSelectedImageIdx(idx)}
                  className={cn(
                    'size-14 cursor-pointer overflow-hidden rounded-lg border-2 bg-surface-elevated transition-colors',
                    idx === selectedImageIdx ? 'border-primary' : 'border-border',
                  )}
                >
                  <img
                    src={img.url}
                    alt={img.altText ?? title}
                    loading="lazy"
                    className="size-full object-cover"
                  />
                </button>
              ))}
            </div>
          ) : null}
        </div>

        {/* Details & Add-to-cart */}
        <Card className="flex flex-col justify-between gap-4">
          <div className="space-y-3">
            <div className="space-y-1">
              <h1 className="text-lg font-bold tracking-tight text-foreground">{title}</h1>
              <p className="tabular-nums text-xl font-bold text-primary">
                {formatMoney(price, shop.currency, lang)}
              </p>
            </div>

            <div className="flex flex-wrap items-center gap-2">
              <Badge variant={stock > 0 ? 'success' : 'destructive'}>
                {stock > 0
                  ? t('storefront.inStock', { count: stock })
                  : t('storefront.outOfStock')}
              </Badge>
              {inCartQty > 0 ? (
                <Badge variant="default">{t('storefront.inCart', { count: inCartQty })}</Badge>
              ) : null}
            </div>

            {product.description ? (
              <div className="space-y-1 border-t border-border pt-3">
                <h2 className="text-xs font-semibold text-muted-foreground">
                  {t('storefront.productDetails')}
                </h2>
                <p className="whitespace-pre-line text-sm text-foreground">{product.description}</p>
              </div>
            ) : null}
          </div>

          <div className="space-y-2.5 border-t border-border pt-3">
            {stock > 0 && remainingAddable > 0 ? (
              <>
                <div className="flex items-center justify-between gap-3">
                  <span className="text-sm font-medium text-foreground">
                    {t('storefront.quantityLabel')}
                  </span>
                  <div className="flex items-center gap-2 rounded-lg border border-border bg-background p-1">
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={t('storefront.decreaseQty')}
                      disabled={quantity <= 1}
                      onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                      className="size-9"
                    >
                      <Minus className="size-4" aria-hidden="true" />
                    </Button>
                    <span className="tabular-nums min-w-8 text-center text-sm font-bold text-foreground">
                      {quantity}
                    </span>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={t('storefront.increaseQty')}
                      disabled={quantity >= remainingAddable}
                      onClick={() => setQuantity((q) => Math.min(remainingAddable, q + 1))}
                      className="size-9"
                    >
                      <Plus className="size-4" aria-hidden="true" />
                    </Button>
                  </div>
                </div>

                <Button type="button" className="w-full" onClick={handleAdd}>
                  {justAdded ? (
                    <>
                      <Check className="size-4" aria-hidden="true" />
                      <span>{t('storefront.addedToCart')}</span>
                    </>
                  ) : (
                    <>
                      <ShoppingCart className="size-4" aria-hidden="true" />
                      <span>{t('storefront.addToCart')}</span>
                    </>
                  )}
                </Button>
              </>
            ) : null}

            {inCartQty > 0 ? (
              <Button asChild variant="outline" className="w-full">
                <Link to={`/s/${shopSlug}/cart`}>{t('storefront.viewCart')}</Link>
              </Button>
            ) : null}
          </div>
        </Card>
      </div>
    </div>
  );
}
