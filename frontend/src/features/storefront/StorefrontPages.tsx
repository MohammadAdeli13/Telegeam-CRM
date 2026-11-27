import { useQuery } from '@tanstack/react-query';
import { Package, Search } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useOutletContext, useParams } from 'react-router';
import { apiClient } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import { formatMoney, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { Badge } from '@/shared/ui/badge';
import { Card } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import type { components } from '@/types/api';

type StorefrontShop = components['schemas']['StorefrontShopDto'];

export function StorefrontCatalogPage() {
  const { t, i18n } = useTranslation();
  const { shopSlug = '' } = useParams();
  const shop = useOutletContext<StorefrontShop>();
  const [selectedCategoryId, setSelectedCategoryId] = useState<string | undefined>(undefined);
  const [searchTerm, setSearchTerm] = useState('');

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
    queryKey: ['storefront-products', shopSlug, selectedCategoryId ?? 'all', searchTerm.trim()],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/storefront/{shopSlug}/products', {
        params: {
          path: { shopSlug },
          query: {
            categoryId: selectedCategoryId,
            q: searchTerm.trim() || undefined,
            page: 1,
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
            onChange={(e) => setSearchTerm(e.target.value)}
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
            onClick={() => setSelectedCategoryId(undefined)}
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
                onClick={() => setSelectedCategoryId(category.id)}
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
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {productsQuery.data.items.map((product) => {
            const title = lang === 'en' && product.nameEn ? product.nameEn : product.name;
            const stock = toNumber(product.stock);
            return (
              <Card key={product.id} className="flex flex-col overflow-hidden p-0">
                <div className="flex aspect-square w-full items-center justify-center bg-surface-elevated">
                  {product.primaryImageUrl ? (
                    <img
                      src={product.primaryImageUrl}
                      alt={title}
                      loading="lazy"
                      className="size-full object-cover"
                    />
                  ) : (
                    <Package className="size-8 text-muted-foreground" aria-hidden="true" />
                  )}
                </div>
                <div className="flex flex-1 flex-col justify-between gap-2 p-3">
                  <h2 className="line-clamp-2 text-sm font-semibold text-foreground">{title}</h2>
                  <div className="space-y-1.5">
                    <p className="tabular-nums text-sm font-bold text-primary">
                      {formatMoney(product.price, shop.currency, lang)}
                    </p>
                    <Badge variant={stock > 0 ? 'success' : 'destructive'}>
                      {stock > 0
                        ? t('storefront.inStock', { count: stock })
                        : t('storefront.outOfStock')}
                    </Badge>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}

export function StorefrontCartPlaceholder() {
  const { t } = useTranslation();
  return <EmptyState title={t('storefront.cartTitle')} description={t('storefront.cartEmpty')} />;
}

export function StorefrontOrdersPlaceholder() {
  const { t } = useTranslation();
  return (
    <EmptyState
      title={t('storefront.myOrdersTitle')}
      description={t('storefront.myOrdersEmpty')}
    />
  );
}
