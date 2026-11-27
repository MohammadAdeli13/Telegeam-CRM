import { useQuery } from '@tanstack/react-query';
import { Minus, Package, Plus, Search, ShoppingCart } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useOutletContext, useParams } from 'react-router';
import { apiClient } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import { formatMoney, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Pagination } from '@/shared/ui/pagination';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import type { components } from '@/types/api';
import { useStorefrontCart } from './cart-context';

type StorefrontShop = components['schemas']['StorefrontShopDto'];

export function StorefrontCatalogPage() {
  const { t, i18n } = useTranslation();
  const { shopSlug = '' } = useParams();
  const shop = useOutletContext<StorefrontShop>();
  const { addItem, setQuantity, getQuantity } = useStorefrontCart();

  const [selectedCategoryId, setSelectedCategoryId] = useState<string | undefined>(undefined);
  const [searchTerm, setSearchTerm] = useState('');
  const [page, setPage] = useState(1);

  const lang = normalizeLanguage(i18n.language);

  const categoriesQuery = useQuery({
    queryKey: ['storefront-categories', shopSlug],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/storefront/{shopSlug}/categories', {
        params: { path: { shopSlug } },
      });
      if (error || !data) {
        throw new Error('Failed to load categories');
      }
      return data;
    },
  });

  const productsQuery = useQuery({
    queryKey: [
      'storefront-products',
      shopSlug,
      page,
      selectedCategoryId ?? 'all',
      searchTerm.trim(),
    ],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/storefront/{shopSlug}/products', {
        params: {
          path: { shopSlug },
          query: {
            categoryId: selectedCategoryId,
            q: searchTerm.trim() || undefined,
            page,
            pageSize: 20,
          },
        },
      });
      if (error || !data) {
        throw new Error('Failed to load products');
      }
      return data;
    },
  });

  return (
    <div className="space-y-4">
      <div className="space-y-1.5">
        <Label htmlFor="storefront-search" className="sr-only">
          {t('storefront.searchLabel')}
        </Label>
        <div className="relative">
          <Search
            className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden="true"
          />
          <Input
            id="storefront-search"
            type="search"
            value={searchTerm}
            onChange={(e) => {
              setSearchTerm(e.target.value);
              setPage(1);
            }}
            placeholder={t('storefront.searchPlaceholder')}
            className="ps-9"
          />
        </div>
      </div>

      {categoriesQuery.data && categoriesQuery.data.length > 0 ? (
        <div className="flex flex-wrap gap-2" role="tablist" aria-label={t('storefront.allCategories')}>
          <button
            type="button"
            role="tab"
            aria-selected={!selectedCategoryId}
            onClick={() => {
              setSelectedCategoryId(undefined);
              setPage(1);
            }}
            className={cn(
              'min-h-11 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
              !selectedCategoryId
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
            )}
          >
            {t('storefront.allCategories')}
          </button>
          {categoriesQuery.data.map((category) => {
            const active = selectedCategoryId === category.id;
            const label = lang === 'en' && category.nameEn ? category.nameEn : category.name;
            return (
              <button
                key={category.id}
                type="button"
                role="tab"
                aria-selected={active}
                onClick={() => {
                  setSelectedCategoryId(category.id);
                  setPage(1);
                }}
                className={cn(
                  'min-h-11 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
                  active
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
                )}
              >
                {label}
              </button>
            );
          })}
        </div>
      ) : null}

      {productsQuery.isLoading ? (
        <LoadingState rows={3} />
      ) : productsQuery.isError ? (
        <ErrorState onRetry={() => void productsQuery.refetch()} />
      ) : !productsQuery.data || productsQuery.data.items.length === 0 ? (
        <EmptyState description={t('storefront.emptyProducts')} />
      ) : (
        <>
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {productsQuery.data.items.map((product) => {
              const title = lang === 'en' && product.nameEn ? product.nameEn : product.name;
              const stock = toNumber(product.stock);
              const price = toNumber(product.price);
              const inCartQty = getQuantity(product.id);

              return (
                <Card key={product.id} className="flex flex-col overflow-hidden p-0">
                  <Link
                    to={`/s/${shopSlug}/products/${product.id}`}
                    className="block aspect-square w-full bg-surface-elevated"
                  >
                    {product.primaryImageUrl ? (
                      <img
                        src={product.primaryImageUrl}
                        alt={title}
                        loading="lazy"
                        className="size-full object-cover transition-transform duration-200 hover:scale-[1.02]"
                      />
                    ) : (
                      <div className="flex size-full items-center justify-center">
                        <Package className="size-8 text-muted-foreground" aria-hidden="true" />
                      </div>
                    )}
                  </Link>

                  <div className="flex flex-1 flex-col justify-between gap-2.5 p-3">
                    <div className="space-y-1.5">
                      <Link
                        to={`/s/${shopSlug}/products/${product.id}`}
                        className="line-clamp-2 text-sm font-semibold text-foreground hover:text-primary"
                      >
                        {title}
                      </Link>
                      <p className="tabular-nums text-sm font-bold text-primary">
                        {formatMoney(price, shop.currency, lang)}
                      </p>
                      <Badge variant={stock > 0 ? 'success' : 'destructive'}>
                        {stock > 0
                          ? t('storefront.inStock', { count: stock })
                          : t('storefront.outOfStock')}
                      </Badge>
                    </div>

                    {stock > 0 ? (
                      inCartQty > 0 ? (
                        <div className="flex items-center justify-between gap-1 rounded-lg border border-primary/40 bg-primary/10 p-1">
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={t('storefront.decreaseQty')}
                            onClick={() => setQuantity(product.id, inCartQty - 1)}
                            className="size-9"
                          >
                            <Minus className="size-3.5" aria-hidden="true" />
                          </Button>
                          <span className="tabular-nums text-xs font-bold text-foreground">
                            {inCartQty}
                          </span>
                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={t('storefront.increaseQty')}
                            disabled={inCartQty >= stock}
                            onClick={() => setQuantity(product.id, inCartQty + 1)}
                            className="size-9"
                          >
                            <Plus className="size-3.5" aria-hidden="true" />
                          </Button>
                        </div>
                      ) : (
                        <Button
                          type="button"
                          size="sm"
                          className="w-full"
                          onClick={() =>
                            addItem({
                              productId: product.id,
                              name: product.name,
                              nameEn: product.nameEn,
                              unitPrice: price,
                              stock,
                              imageUrl: product.primaryImageUrl,
                            })
                          }
                        >
                          <ShoppingCart className="size-3.5" aria-hidden="true" />
                          <span>{t('storefront.addToCart')}</span>
                        </Button>
                      )
                    ) : null}
                  </div>
                </Card>
              );
            })}
          </div>

          <Pagination
            page={toNumber(productsQuery.data.page, 1)}
            pageSize={toNumber(productsQuery.data.pageSize, 20)}
            total={toNumber(productsQuery.data.total, 0)}
            onPageChange={setPage}
          />
        </>
      )}
    </div>
  );
}
