import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArrowUpDown,
  FolderOpen,
  ImagePlus,
  Package,
  Pencil,
  Plus,
  Search,
  Star,
  Trash2,
  Upload,
} from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { uploadProductImage } from '@/shared/api/uploads';
import { cn } from '@/shared/lib/cn';
import {
  formatMoney,
  formatNumber,
  getCurrencyUnit,
  toLatinDigits,
  toNumber,
} from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { triggerHaptic } from '@/shared/lib/telegram';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { ConfirmDialog } from '@/shared/ui/confirm-dialog';
import { ModalDialog } from '@/shared/ui/dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Pagination } from '@/shared/ui/pagination';
import { Select } from '@/shared/ui/select';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { Switch } from '@/shared/ui/switch';
import { Textarea } from '@/shared/ui/textarea';
import type { components } from '@/types/api';
import { slugifyCategoryName } from './merchant-state';

type CategoryDto = components['schemas']['CategoryDto'];
type ProductDto = components['schemas']['ProductDto'];

const ALLOWED_IMAGE_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif']);
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

export function MerchantProductsPage() {
  const { t, i18n } = useTranslation();
  const { session } = useAuth();
  const queryClient = useQueryClient();
  const lang = normalizeLanguage(i18n.language);
  const activeShop = session?.activeTenant;
  const tenantKey = activeShop?.id ?? 'none';
  const currency = activeShop?.currency ?? 'IRT';
  const defaultProductImage = activeShop?.defaultProductImageUrl ?? null;

  const [tab, setTab] = useState<'products' | 'categories'>('products');
  const [searchTerm, setSearchTerm] = useState('');
  const [filterCategoryId, setFilterCategoryId] = useState<string | undefined>(undefined);
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [page, setPage] = useState(1);

  const [productModal, setProductModal] = useState<{ open: boolean; editing: ProductDto | null }>({
    open: false,
    editing: null,
  });
  const [categoryModal, setCategoryModal] = useState<{ open: boolean; editing: CategoryDto | null }>(
    { open: false, editing: null },
  );
  const [stockProduct, setStockProduct] = useState<ProductDto | null>(null);
  const [imagesProduct, setImagesProduct] = useState<ProductDto | null>(null);
  const [pendingDelete, setPendingDelete] = useState<
    | { kind: 'product'; id: string; name: string }
    | { kind: 'category'; id: string; name: string }
    | null
  >(null);
  const [bannerError, setBannerError] = useState<string | null>(null);

  const categoriesQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'categories'],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/categories', {
        params: { query: { page: 1, pageSize: 100 } },
      });
      if (error || !data) {
        throw new Error('Failed to load categories');
      }
      return data.items;
    },
  });

  const productsQuery = useQuery({
    queryKey: [
      'merchant',
      tenantKey,
      'products',
      page,
      filterCategoryId ?? 'all',
      activeFilter,
      searchTerm.trim(),
    ],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/products', {
        params: {
          query: {
            page,
            pageSize: 20,
            categoryId: filterCategoryId,
            q: searchTerm.trim() || undefined,
            isActive: activeFilter === 'all' ? undefined : activeFilter === 'active',
          },
        },
      });
      if (error || !data) {
        throw new Error('Failed to load products');
      }
      return data;
    },
  });

  const deleteProductMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await apiClient.DELETE('/api/v1/merchant/products/{id}', {
        params: { path: { id } },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      setBannerError(null);
      setPendingDelete(null);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'kpi-products'] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setPendingDelete(null);
      const code = extractProblemCode(err);
      setBannerError(
        code === 'product_in_use'
          ? t('merchant.catalog.deleteProductInUse')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const deleteCategoryMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await apiClient.DELETE('/api/v1/merchant/categories/{id}', {
        params: { path: { id } },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      setBannerError(null);
      setPendingDelete(null);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'categories'] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setPendingDelete(null);
      const code = extractProblemCode(err);
      setBannerError(
        code === 'category_in_use'
          ? t('merchant.catalog.deleteCategoryInUse')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const categories = categoriesQuery.data ?? [];
  const categoryNameMap = new Map(
    categories.map((c) => [c.id, lang === 'en' && c.nameEn ? c.nameEn : c.name]),
  );

  const openNewProductModal = () => {
    setBannerError(null);
    if (categories.length === 0) {
      setBannerError(t('merchant.catalog.needCategoryFirst'));
      setTab('categories');
      return;
    }
    setProductModal({ open: true, editing: null });
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('merchant.catalog.title')}
        </h1>

        {tab === 'products' ? (
          <Button type="button" size="sm" onClick={openNewProductModal}>
            <Plus className="size-4" aria-hidden="true" />
            <span>{t('merchant.catalog.newProduct')}</span>
          </Button>
        ) : (
          <Button
            type="button"
            size="sm"
            onClick={() => {
              setBannerError(null);
              setCategoryModal({ open: true, editing: null });
            }}
          >
            <Plus className="size-4" aria-hidden="true" />
            <span>{t('merchant.catalog.newCategory')}</span>
          </Button>
        )}
      </div>

      {/* Primary tabs */}
      <div className="grid grid-cols-2 rounded-xl border border-border bg-surface p-1">
        <button
          type="button"
          onClick={() => {
            triggerHaptic('selection');
            setTab('products');
            setBannerError(null);
          }}
          className={cn(
            'min-h-11 cursor-pointer rounded-lg px-3 text-sm font-medium transition-colors',
            tab === 'products'
              ? 'bg-primary text-primary-foreground'
              : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {t('merchant.catalog.tabProducts')}
        </button>
        <button
          type="button"
          onClick={() => {
            triggerHaptic('selection');
            setTab('categories');
            setBannerError(null);
          }}
          className={cn(
            'min-h-11 cursor-pointer rounded-lg px-3 text-sm font-medium transition-colors',
            tab === 'categories'
              ? 'bg-primary text-primary-foreground'
              : 'text-muted-foreground hover:text-foreground',
          )}
        >
          {t('merchant.catalog.tabCategories')} ({formatNumber(categories.length, lang)})
        </button>
      </div>

      {bannerError ? (
        <div
          role="alert"
          className="rounded-lg border border-destructive/40 bg-destructive/10 px-3.5 py-2.5 text-sm text-destructive"
        >
          {bannerError}
        </div>
      ) : null}

      {tab === 'products' ? (
        <div className="space-y-3">
          {/* Search & balanced status filter */}
          <div className="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_auto]">
            <div className="relative">
              <Search
                className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
                aria-hidden="true"
              />
              <Input
                type="search"
                value={searchTerm}
                onChange={(e) => {
                  setSearchTerm(e.target.value);
                  setPage(1);
                }}
                placeholder={t('merchant.catalog.searchProducts')}
                aria-label={t('common.search')}
                className="ps-9"
              />
            </div>

            <div className="grid grid-cols-3 gap-1 rounded-lg border border-border bg-surface p-1">
              {(['all', 'active', 'inactive'] as const).map((mode) => (
                <button
                  key={mode}
                  type="button"
                  onClick={() => {
                    triggerHaptic('selection');
                    setActiveFilter(mode);
                    setPage(1);
                  }}
                  className={cn(
                    'min-h-11 cursor-pointer rounded-md px-2.5 text-xs font-medium transition-colors',
                    activeFilter === mode
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  {mode === 'all'
                    ? t('merchant.catalog.statusFilterAll')
                    : t(`common.${mode}`)}
                </button>
              ))}
            </div>
          </div>

          {/* Horizontal scrollable category pills */}
          {categories.length > 0 ? (
            <div className="no-scrollbar flex flex-nowrap gap-2 overflow-x-auto pb-1 lg:flex-wrap">
              <button
                type="button"
                onClick={() => {
                  triggerHaptic('selection');
                  setFilterCategoryId(undefined);
                  setPage(1);
                }}
                className={cn(
                  'min-h-11 shrink-0 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
                  !filterCategoryId
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
                )}
              >
                {t('storefront.allCategories')}
              </button>
              {categories.map((cat) => {
                const active = filterCategoryId === cat.id;
                return (
                  <button
                    key={cat.id}
                    type="button"
                    onClick={() => {
                      triggerHaptic('selection');
                      setFilterCategoryId(cat.id);
                      setPage(1);
                    }}
                    className={cn(
                      'min-h-11 shrink-0 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
                      active
                        ? 'border-primary bg-primary text-primary-foreground'
                        : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
                    )}
                  >
                    {lang === 'en' && cat.nameEn ? cat.nameEn : cat.name}
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
            <EmptyState
              icon={<Package className="size-6" aria-hidden="true" />}
              description={t('merchant.catalog.emptyProducts')}
              action={
                <Button type="button" size="sm" onClick={openNewProductModal}>
                  <Plus className="size-4" aria-hidden="true" />
                  <span>{t('merchant.catalog.newProduct')}</span>
                </Button>
              }
            />
          ) : (
            <>
              <div className="space-y-2.5">
                {productsQuery.data.items.map((product) => {
                  const stock = toNumber(product.stock);
                  const thumb = product.primaryImageUrl ?? defaultProductImage;
                  return (
                    <Card key={product.id} className="space-y-3">
                      <div className="flex items-start gap-3">
                        <div className="flex size-14 shrink-0 items-center justify-center overflow-hidden rounded-lg border border-border bg-surface-elevated">
                          {thumb ? (
                            <img
                              src={thumb}
                              alt={product.name}
                              loading="lazy"
                              className="size-full object-cover"
                            />
                          ) : (
                            <Package className="size-5 text-muted-foreground" aria-hidden="true" />
                          )}
                        </div>

                        <div className="min-w-0 flex-1 space-y-1">
                          <div className="flex items-start justify-between gap-2">
                            <div className="min-w-0">
                              <h2 className="truncate text-sm font-semibold text-foreground">
                                {product.name}
                              </h2>
                              {product.nameEn ? (
                                <p className="truncate text-xs text-muted-foreground" dir="ltr">
                                  {product.nameEn}
                                </p>
                              ) : null}
                            </div>

                            <div className="flex shrink-0 flex-wrap justify-end gap-1">
                              <Badge variant={stock > 0 ? 'success' : 'destructive'}>
                                <span className="tabular-nums">
                                  {t('merchant.catalog.stockLabel')}: {formatNumber(stock, lang)}
                                </span>
                              </Badge>
                              <Badge variant={product.isActive ? 'default' : 'secondary'}>
                                {product.isActive ? t('common.active') : t('common.inactive')}
                              </Badge>
                            </div>
                          </div>

                          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                            <span>{categoryNameMap.get(product.categoryId) ?? '—'}</span>
                            <span>·</span>
                            <span className="tabular-nums font-semibold text-primary">
                              {formatMoney(product.price, currency, lang)}
                            </span>
                          </div>
                        </div>
                      </div>

                      {/* Balanced single-row 4-button action bar on mobile */}
                      <div className="grid grid-cols-4 gap-1.5 border-t border-border pt-2.5 sm:flex sm:flex-wrap sm:gap-2">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setStockProduct(product)}
                          className="px-2 sm:px-3"
                        >
                          <ArrowUpDown className="size-3.5 shrink-0" aria-hidden="true" />
                          <span className="truncate">{t('merchant.catalog.stockAdjustButton')}</span>
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setImagesProduct(product)}
                          className="px-2 sm:px-3"
                        >
                          <ImagePlus className="size-3.5 shrink-0" aria-hidden="true" />
                          <span className="truncate">{t('merchant.catalog.imagesButton')}</span>
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setProductModal({ open: true, editing: product })}
                          className="px-2 sm:px-3"
                        >
                          <Pencil className="size-3.5 shrink-0" aria-hidden="true" />
                          <span className="truncate">{t('common.edit')}</span>
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={deleteProductMutation.isPending}
                          onClick={() =>
                            setPendingDelete({ kind: 'product', id: product.id, name: product.name })
                          }
                          className="px-2 text-destructive hover:bg-destructive/10 sm:px-3"
                        >
                          <Trash2 className="size-3.5 shrink-0" aria-hidden="true" />
                          <span className="truncate">{t('common.delete')}</span>
                        </Button>
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
      ) : (
        <div className="space-y-2.5">
          {categoriesQuery.isLoading ? (
            <LoadingState rows={3} />
          ) : categoriesQuery.isError ? (
            <ErrorState onRetry={() => void categoriesQuery.refetch()} />
          ) : categories.length === 0 ? (
            <EmptyState
              icon={<FolderOpen className="size-6" aria-hidden="true" />}
              description={t('merchant.catalog.emptyCategories')}
              action={
                <Button
                  type="button"
                  size="sm"
                  onClick={() => setCategoryModal({ open: true, editing: null })}
                >
                  <Plus className="size-4" aria-hidden="true" />
                  <span>{t('merchant.catalog.newCategory')}</span>
                </Button>
              }
            />
          ) : (
            categories.map((cat) => (
              <Card key={cat.id} className="flex flex-wrap items-center justify-between gap-3">
                <div className="min-w-0 space-y-0.5">
                  <div className="flex items-center gap-2">
                    <FolderOpen className="size-4 shrink-0 text-primary" aria-hidden="true" />
                    <h2 className="text-sm font-semibold text-foreground">{cat.name}</h2>
                    {cat.nameEn ? (
                      <span className="text-xs text-muted-foreground" dir="ltr">
                        ({cat.nameEn})
                      </span>
                    ) : null}
                  </div>
                  <p className="text-xs text-muted-foreground" dir="ltr">
                    {cat.slug}
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <Badge variant={cat.isActive ? 'default' : 'secondary'}>
                    {cat.isActive ? t('common.active') : t('common.inactive')}
                  </Badge>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() => setCategoryModal({ open: true, editing: cat })}
                  >
                    <Pencil className="size-3.5" aria-hidden="true" />
                    <span>{t('common.edit')}</span>
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    disabled={deleteCategoryMutation.isPending}
                    onClick={() =>
                      setPendingDelete({ kind: 'category', id: cat.id, name: cat.name })
                    }
                    className="text-destructive hover:bg-destructive/10"
                  >
                    <Trash2 className="size-3.5" aria-hidden="true" />
                    <span>{t('common.delete')}</span>
                  </Button>
                </div>
              </Card>
            ))
          )}
        </div>
      )}

      {/* Styled ConfirmDialog replacing native window.confirm */}
      <ConfirmDialog
        open={pendingDelete !== null}
        title={
          pendingDelete?.kind === 'product'
            ? t('merchant.catalog.deleteProductTitle')
            : t('merchant.catalog.deleteCategoryTitle')
        }
        description={
          pendingDelete
            ? pendingDelete.kind === 'product'
              ? t('merchant.catalog.confirmDeleteProduct', { name: pendingDelete.name })
              : t('merchant.catalog.confirmDeleteCategory', { name: pendingDelete.name })
            : ''
        }
        confirmLabel={t('common.delete')}
        busy={deleteProductMutation.isPending || deleteCategoryMutation.isPending}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => {
          if (!pendingDelete) {
            return;
          }
          if (pendingDelete.kind === 'product') {
            deleteProductMutation.mutate(pendingDelete.id);
          } else {
            deleteCategoryMutation.mutate(pendingDelete.id);
          }
        }}
      />

      {categoryModal.open ? (
        <CategoryFormModal
          tenantKey={tenantKey}
          editing={categoryModal.editing}
          onClose={() => setCategoryModal({ open: false, editing: null })}
        />
      ) : null}

      {productModal.open ? (
        <ProductFormModal
          tenantKey={tenantKey}
          currency={currency}
          categories={categories}
          editing={productModal.editing}
          onClose={() => setProductModal({ open: false, editing: null })}
        />
      ) : null}

      {stockProduct ? (
        <StockAdjustModal
          tenantKey={tenantKey}
          product={stockProduct}
          onClose={() => setStockProduct(null)}
        />
      ) : null}

      {imagesProduct ? (
        <ProductImagesModal
          tenantKey={tenantKey}
          product={imagesProduct}
          onClose={() => setImagesProduct(null)}
        />
      ) : null}
    </div>
  );
}

const categoryFormSchema = z.object({
  name: z.string().trim().min(2).max(120),
  nameEn: z.string().trim().max(120),
  slug: z
    .string()
    .trim()
    .toLowerCase()
    .regex(/^[a-z0-9-]{1,40}$/),
  sortOrder: z
    .string()
    .trim()
    .refine((v) => v === '' || (/^\d+$/.test(toLatinDigits(v)) && Number(toLatinDigits(v)) >= 0)),
  isActive: z.boolean(),
});

type CategoryFormValues = z.infer<typeof categoryFormSchema>;

function CategoryFormModal({
  tenantKey,
  editing,
  onClose,
}: {
  tenantKey: string;
  editing: CategoryDto | null;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    control,
    setValue,
    getValues,
    handleSubmit,
    formState: { errors },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categoryFormSchema),
    defaultValues: {
      name: editing?.name ?? '',
      nameEn: editing?.nameEn ?? '',
      slug: editing?.slug ?? '',
      sortOrder: String(editing ? toNumber(editing.sortOrder) : 0),
      isActive: editing ? editing.isActive : true,
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: CategoryFormValues) => {
      const parsedOrder = Math.max(0, Number.parseInt(toLatinDigits(values.sortOrder), 10) || 0);
      if (editing) {
        const { error } = await apiClient.PATCH('/api/v1/merchant/categories/{id}', {
          params: { path: { id: editing.id } },
          body: {
            name: values.name,
            nameEn: values.nameEn || null,
            slug: values.slug,
            sortOrder: parsedOrder,
            isActive: values.isActive,
          },
        });
        if (error) {
          throw error;
        }
      } else {
        const { error } = await apiClient.POST('/api/v1/merchant/categories', {
          body: {
            name: values.name,
            nameEn: values.nameEn || null,
            slug: values.slug,
            sortOrder: parsedOrder,
            isActive: values.isActive,
          },
        });
        if (error) {
          throw error;
        }
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'categories'] });
      onClose();
    },
    onError: (err) => {
      triggerHaptic('error');
      const code = extractProblemCode(err);
      setServerError(
        code === 'slug_taken'
          ? t('merchant.catalog.slugTaken')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const nameEnField = register('nameEn');

  return (
    <ModalDialog
      open
      title={editing ? t('merchant.catalog.editCategory') : t('merchant.catalog.newCategory')}
      onClose={onClose}
    >
      <form
        onSubmit={(e) => void handleSubmit((vals) => mutation.mutate(vals))(e)}
        className="space-y-3.5"
        noValidate
      >
        <div className="space-y-1.5">
          <Label htmlFor="cat-name-fa">{t('merchant.catalog.nameFaLabel')}</Label>
          <Input id="cat-name-fa" aria-invalid={Boolean(errors.name)} {...register('name')} />
          {errors.name ? (
            <p className="text-xs text-destructive">{t('merchant.catalog.validationName')}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="cat-name-en">{t('merchant.catalog.nameEnLabel')}</Label>
          <Input
            id="cat-name-en"
            dir="ltr"
            {...nameEnField}
            onChange={(e) => {
              void nameEnField.onChange(e);
              if (!editing && !getValues('slug')) {
                setValue('slug', slugifyCategoryName(e.target.value), { shouldValidate: true });
              }
            }}
          />
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label htmlFor="cat-slug">{t('merchant.catalog.slugLabel')}</Label>
            <Input
              id="cat-slug"
              dir="ltr"
              aria-invalid={Boolean(errors.slug)}
              {...register('slug')}
            />
            {errors.slug ? (
              <p className="text-xs text-destructive">{t('merchant.catalog.validationSlug')}</p>
            ) : null}
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="cat-order">{t('merchant.catalog.sortOrderLabel')}</Label>
            <Input id="cat-order" dir="ltr" inputMode="numeric" {...register('sortOrder')} />
          </div>
        </div>

        <div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-1.5">
          <Label htmlFor="cat-active-switch" className="cursor-pointer">
            {t('merchant.catalog.visibilityLabel')}
          </Label>
          <Controller
            control={control}
            name="isActive"
            render={({ field }) => (
              <Switch
                id="cat-active-switch"
                checked={field.value}
                onCheckedChange={field.onChange}
                ariaLabel={t('merchant.catalog.visibilityLabel')}
              />
            )}
          />
        </div>

        {serverError ? (
          <p
            role="alert"
            className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            {serverError}
          </p>
        ) : null}

        <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            className="w-full sm:w-auto"
          >
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={mutation.isPending} className="w-full sm:w-auto">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </ModalDialog>
  );
}

const productFormSchema = z.object({
  categoryId: z.string().min(1),
  name: z.string().trim().min(2).max(200),
  nameEn: z.string().trim().max(200),
  price: z
    .string()
    .trim()
    .refine((v) => {
      const normalized = toLatinDigits(v);
      return normalized.length > 0 && !Number.isNaN(Number(normalized)) && Number(normalized) >= 0;
    }),
  stock: z
    .string()
    .trim()
    .refine((v) => {
      const normalized = toLatinDigits(v);
      return /^\d+$/.test(normalized) && Number(normalized) >= 0;
    }),
  description: z.string().max(4000),
  isActive: z.boolean(),
});

type ProductFormValues = z.infer<typeof productFormSchema>;

function ProductFormModal({
  tenantKey,
  currency,
  categories,
  editing,
  onClose,
}: {
  tenantKey: string;
  currency: string;
  categories: CategoryDto[];
  editing: ProductDto | null;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const lang = normalizeLanguage(i18n.language);
  const currencyUnit = getCurrencyUnit(currency, lang);
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<ProductFormValues>({
    resolver: zodResolver(productFormSchema),
    defaultValues: {
      categoryId: editing?.categoryId ?? categories[0]?.id ?? '',
      name: editing?.name ?? '',
      nameEn: editing?.nameEn ?? '',
      price: editing ? String(toNumber(editing.price)) : '',
      stock: editing ? String(toNumber(editing.stock)) : '0',
      description: editing?.description ?? '',
      isActive: editing ? editing.isActive : true,
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: ProductFormValues) => {
      const parsedPrice = Number(toLatinDigits(values.price));
      const parsedStock = Math.max(0, Number.parseInt(toLatinDigits(values.stock), 10) || 0);

      if (editing) {
        const { error } = await apiClient.PATCH('/api/v1/merchant/products/{id}', {
          params: { path: { id: editing.id } },
          body: {
            categoryId: values.categoryId,
            name: values.name,
            nameEn: values.nameEn || null,
            description: values.description.trim() || null,
            price: parsedPrice,
            isActive: values.isActive,
          },
        });
        if (error) {
          throw error;
        }
      } else {
        const { error } = await apiClient.POST('/api/v1/merchant/products', {
          body: {
            categoryId: values.categoryId,
            name: values.name,
            nameEn: values.nameEn || null,
            description: values.description.trim() || null,
            price: parsedPrice,
            stock: parsedStock,
            isActive: values.isActive,
          },
        });
        if (error) {
          throw error;
        }
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'kpi-products'] });
      onClose();
    },
    onError: (err) => {
      triggerHaptic('error');
      setServerError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  return (
    <ModalDialog
      open
      title={editing ? t('merchant.catalog.editProduct') : t('merchant.catalog.newProduct')}
      onClose={onClose}
    >
      <form
        onSubmit={(e) => void handleSubmit((vals) => mutation.mutate(vals))(e)}
        className="space-y-3.5"
        noValidate
      >
        <div className="space-y-1.5">
          <Label htmlFor="prod-category">{t('merchant.catalog.categoryLabel')}</Label>
          <Select id="prod-category" {...register('categoryId')}>
            {categories.map((cat) => (
              <option key={cat.id} value={cat.id}>
                {lang === 'en' && cat.nameEn ? cat.nameEn : cat.name}
              </option>
            ))}
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="prod-name-fa">{t('merchant.catalog.nameFaLabel')}</Label>
          <Input id="prod-name-fa" aria-invalid={Boolean(errors.name)} {...register('name')} />
          {errors.name ? (
            <p className="text-xs text-destructive">{t('merchant.catalog.validationName')}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="prod-name-en">{t('merchant.catalog.nameEnLabel')}</Label>
          <Input id="prod-name-en" dir="ltr" {...register('nameEn')} />
        </div>

        <div className={cn('grid gap-3', editing ? 'grid-cols-1' : 'grid-cols-2')}>
          <div className="space-y-1.5">
            <Label htmlFor="prod-price">
              {t('merchant.catalog.priceWithCurrency', { currency: currencyUnit })}
            </Label>
            <Input
              id="prod-price"
              dir="ltr"
              inputMode="decimal"
              aria-invalid={Boolean(errors.price)}
              {...register('price')}
            />
            {errors.price ? (
              <p className="text-xs text-destructive">{t('merchant.catalog.validationPrice')}</p>
            ) : null}
          </div>

          {!editing ? (
            <div className="space-y-1.5">
              <Label htmlFor="prod-stock">{t('merchant.catalog.stockLabel')}</Label>
              <Input
                id="prod-stock"
                dir="ltr"
                inputMode="numeric"
                aria-invalid={Boolean(errors.stock)}
                {...register('stock')}
              />
              {errors.stock ? (
                <p className="text-xs text-destructive">{t('merchant.catalog.validationStock')}</p>
              ) : null}
            </div>
          ) : null}
        </div>

        {editing ? (
          <p className="text-xs text-muted-foreground">{t('merchant.catalog.editStockHint')}</p>
        ) : null}

        <div className="space-y-1.5">
          <Label htmlFor="prod-desc">{t('merchant.catalog.descriptionLabel')}</Label>
          <Textarea id="prod-desc" {...register('description')} />
        </div>

        <div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-1.5">
          <Label htmlFor="prod-active-switch" className="cursor-pointer">
            {t('merchant.catalog.visibilityLabel')}
          </Label>
          <Controller
            control={control}
            name="isActive"
            render={({ field }) => (
              <Switch
                id="prod-active-switch"
                checked={field.value}
                onCheckedChange={field.onChange}
                ariaLabel={t('merchant.catalog.visibilityLabel')}
              />
            )}
          />
        </div>

        {serverError ? (
          <p
            role="alert"
            className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            {serverError}
          </p>
        ) : null}

        <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            className="w-full sm:w-auto"
          >
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={mutation.isPending} className="w-full sm:w-auto">
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </ModalDialog>
  );
}

const stockAdjustSchema = z.object({
  delta: z
    .string()
    .trim()
    .refine((v) => {
      const normalized = toLatinDigits(v);
      const parsed = Number.parseInt(normalized, 10);
      return /^-?\d+$/.test(normalized) && !Number.isNaN(parsed) && parsed !== 0;
    }),
  reason: z.string().trim().max(500),
});

type StockAdjustValues = z.infer<typeof stockAdjustSchema>;

function StockAdjustModal({
  tenantKey,
  product,
  onClose,
}: {
  tenantKey: string;
  product: ProductDto;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const lang = normalizeLanguage(i18n.language);
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    setValue,
    handleSubmit,
    formState: { errors },
  } = useForm<StockAdjustValues>({
    resolver: zodResolver(stockAdjustSchema),
    defaultValues: {
      delta: '1',
      reason: '',
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: StockAdjustValues) => {
      const parsedDelta = Number.parseInt(toLatinDigits(values.delta), 10);
      const { data, error } = await apiClient.POST('/api/v1/merchant/products/{id}/stock-adjust', {
        params: { path: { id: product.id } },
        body: {
          delta: parsedDelta,
          reason: values.reason || null,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to adjust stock');
      }
      return data;
    },
    onSuccess: () => {
      triggerHaptic('success');
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
      onClose();
    },
    onError: (err) => {
      triggerHaptic('error');
      const code = extractProblemCode(err);
      setServerError(
        code === 'insufficient_stock'
          ? t('merchant.catalog.insufficientStock')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  return (
    <ModalDialog
      open
      title={t('merchant.catalog.stockAdjustTitle')}
      description={`${product.name} — ${t('merchant.catalog.currentStock', { count: formatNumber(toNumber(product.stock), lang) })}`}
      onClose={onClose}
    >
      <form
        onSubmit={(e) => void handleSubmit((vals) => mutation.mutate(vals))(e)}
        className="space-y-3.5"
        noValidate
      >
        <div className="flex flex-wrap gap-2">
          {[-5, -1, 1, 5, 10].map((preset) => (
            <Button
              key={preset}
              type="button"
              variant="outline"
              size="sm"
              onClick={() => {
                triggerHaptic('selection');
                setValue('delta', String(preset), { shouldValidate: true });
              }}
              className="tabular-nums"
              dir="ltr"
            >
              {preset > 0 ? `+${preset}` : preset}
            </Button>
          ))}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="stock-delta">{t('merchant.catalog.deltaLabel')}</Label>
          <Input
            id="stock-delta"
            dir="ltr"
            inputMode="numeric"
            aria-invalid={Boolean(errors.delta)}
            {...register('delta')}
          />
          {errors.delta ? (
            <p className="text-xs text-destructive">{t('merchant.catalog.validationDelta')}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="stock-reason">{t('merchant.catalog.deltaReasonLabel')}</Label>
          <Input
            id="stock-reason"
            placeholder={t('merchant.catalog.deltaReasonPlaceholder')}
            {...register('reason')}
          />
        </div>

        {serverError ? (
          <p
            role="alert"
            className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            {serverError}
          </p>
        ) : null}

        <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
          <Button
            type="button"
            variant="outline"
            onClick={onClose}
            className="w-full sm:w-auto"
          >
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={mutation.isPending} className="w-full sm:w-auto">
            {mutation.isPending ? t('common.saving') : t('merchant.catalog.applyStock')}
          </Button>
        </div>
      </form>
    </ModalDialog>
  );
}

function ProductImagesModal({
  tenantKey,
  product,
  onClose,
}: {
  tenantKey: string;
  product: ProductDto;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [altText, setAltText] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const imagesQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'product-images', product.id],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/products/{productId}/images', {
        params: { path: { productId: product.id } },
      });
      if (error || !data) {
        throw new Error('Failed to load images');
      }
      return data;
    },
  });

  const invalidateImagesAndProducts = () => {
    void queryClient.invalidateQueries({
      queryKey: ['merchant', tenantKey, 'product-images', product.id],
    });
    void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
  };

  const uploadMutation = useMutation({
    mutationFn: async () => {
      if (!selectedFile) {
        throw new Error('No file selected');
      }
      const result = await uploadProductImage(product.id, selectedFile, altText);
      if (result.error || !result.data) {
        throw result.error ?? new Error('Upload failed');
      }
      return result.data;
    },
    onSuccess: () => {
      triggerHaptic('success');
      setSelectedFile(null);
      setAltText('');
      setErrorMsg(null);
      invalidateImagesAndProducts();
    },
    onError: (err) => {
      triggerHaptic('error');
      setErrorMsg(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const setPrimaryMutation = useMutation({
    mutationFn: async (imageId: string) => {
      const { error } = await apiClient.POST(
        '/api/v1/merchant/products/{productId}/images/{imageId}/primary',
        { params: { path: { productId: product.id, imageId } } },
      );
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      invalidateImagesAndProducts();
    },
  });

  const deleteImageMutation = useMutation({
    mutationFn: async (imageId: string) => {
      const { error } = await apiClient.DELETE(
        '/api/v1/merchant/products/{productId}/images/{imageId}',
        { params: { path: { productId: product.id, imageId } } },
      );
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      invalidateImagesAndProducts();
    },
  });

  const handleFileChange = (file: File | null) => {
    setErrorMsg(null);
    if (!file) {
      setSelectedFile(null);
      return;
    }
    if (!ALLOWED_IMAGE_TYPES.has(file.type)) {
      setErrorMsg(t('merchant.catalog.invalidFileType'));
      setSelectedFile(null);
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setErrorMsg(t('merchant.catalog.fileTooLarge'));
      setSelectedFile(null);
      return;
    }
    setSelectedFile(file);
  };

  const handleUpload = (event: FormEvent) => {
    event.preventDefault();
    if (!selectedFile) {
      return;
    }
    uploadMutation.mutate();
  };

  return (
    <ModalDialog
      open
      title={t('merchant.catalog.imagesTitle')}
      description={product.name}
      onClose={onClose}
    >
      <div className="space-y-4">
        <form
          onSubmit={handleUpload}
          className="space-y-3 rounded-lg border border-border bg-background p-3"
        >
          <div className="space-y-1.5">
            <Label htmlFor="image-file">{t('merchant.catalog.uploadImageLabel')}</Label>
            <input
              id="image-file"
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              onChange={(e) => handleFileChange(e.target.files?.[0] ?? null)}
              className="block w-full cursor-pointer text-xs text-muted-foreground file:me-3 file:min-h-11 file:cursor-pointer file:rounded-lg file:border-0 file:bg-secondary file:px-3 file:text-xs file:font-medium file:text-secondary-foreground"
            />
          </div>

          <div className="space-y-1.5">
            <Label htmlFor="image-alt">{t('merchant.catalog.altTextLabel')}</Label>
            <Input
              id="image-alt"
              maxLength={300}
              value={altText}
              onChange={(e) => setAltText(e.target.value)}
            />
          </div>

          {errorMsg ? (
            <p role="alert" className="text-xs text-destructive">
              {errorMsg}
            </p>
          ) : null}

          <Button
            type="submit"
            size="sm"
            disabled={!selectedFile || uploadMutation.isPending}
            className="w-full sm:w-auto"
          >
            <Upload className="size-4" aria-hidden="true" />
            <span>
              {uploadMutation.isPending
                ? t('merchant.catalog.uploading')
                : t('merchant.catalog.uploadCta')}
            </span>
          </Button>
        </form>

        {imagesQuery.isLoading ? (
          <LoadingState rows={2} />
        ) : !imagesQuery.data || imagesQuery.data.length === 0 ? (
          <EmptyState embedded description={t('merchant.catalog.emptyImages')} />
        ) : (
          <div className="grid grid-cols-2 gap-3">
            {imagesQuery.data.map((img) => (
              <Card key={img.id} className="overflow-hidden p-0">
                <div className="aspect-square bg-surface-elevated">
                  <img
                    src={img.url}
                    alt={img.altText ?? product.name}
                    loading="lazy"
                    className="size-full object-cover"
                  />
                </div>
                <div className="flex items-center justify-between gap-1 p-2">
                  {img.isPrimary ? (
                    <Badge variant="success">
                      <Star className="size-3" aria-hidden="true" />
                      <span>{t('merchant.catalog.primaryBadge')}</span>
                    </Badge>
                  ) : (
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      disabled={setPrimaryMutation.isPending}
                      onClick={() => setPrimaryMutation.mutate(img.id)}
                      className="px-2 text-xs"
                    >
                      <Star className="size-3.5" aria-hidden="true" />
                      <span>{t('merchant.catalog.setPrimary')}</span>
                    </Button>
                  )}

                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    aria-label={t('common.delete')}
                    disabled={deleteImageMutation.isPending}
                    onClick={() => deleteImageMutation.mutate(img.id)}
                    className="text-destructive hover:bg-destructive/10"
                  >
                    <Trash2 className="size-4" aria-hidden="true" />
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </ModalDialog>
  );
}
