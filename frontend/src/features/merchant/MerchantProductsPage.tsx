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
import { useTranslation } from 'react-i18next';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { uploadProductImage } from '@/shared/api/uploads';
import { cn } from '@/shared/lib/cn';
import { formatMoney, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { ModalDialog } from '@/shared/ui/dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Pagination } from '@/shared/ui/pagination';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
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
  const tenantKey = session?.activeTenant?.id ?? 'none';

  const [tab, setTab] = useState<'products' | 'categories'>('products');
  const [searchTerm, setSearchTerm] = useState('');
  const [filterCategoryId, setFilterCategoryId] = useState<string | undefined>(undefined);
  const [activeFilter, setActiveFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [page, setPage] = useState(1);

  // Modals
  const [productModal, setProductModal] = useState<{ open: boolean; editing: ProductDto | null }>({
    open: false,
    editing: null,
  });
  const [categoryModal, setCategoryModal] = useState<{ open: boolean; editing: CategoryDto | null }>(
    { open: false, editing: null },
  );
  const [stockProduct, setStockProduct] = useState<ProductDto | null>(null);
  const [imagesProduct, setImagesProduct] = useState<ProductDto | null>(null);
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
      setBannerError(null);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'kpi-products'] });
    },
    onError: (err) => {
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
      setBannerError(null);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'categories'] });
    },
    onError: (err) => {
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

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('merchant.catalog.title')}
        </h1>

        <div className="flex flex-wrap items-center gap-2">
          <div className="inline-flex rounded-lg border border-border bg-surface p-1">
            <button
              type="button"
              onClick={() => {
                setTab('products');
                setBannerError(null);
              }}
              className={cn(
                'min-h-9 cursor-pointer rounded-md px-3 text-xs font-medium transition-colors',
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
                setTab('categories');
                setBannerError(null);
              }}
              className={cn(
                'min-h-9 cursor-pointer rounded-md px-3 text-xs font-medium transition-colors',
                tab === 'categories'
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:text-foreground',
              )}
            >
              {t('merchant.catalog.tabCategories')} ({categories.length})
            </button>
          </div>

          {tab === 'products' ? (
            <Button
              type="button"
              size="sm"
              onClick={() => {
                setBannerError(null);
                if (categories.length === 0) {
                  setBannerError(t('merchant.catalog.needCategoryFirst'));
                  setTab('categories');
                  return;
                }
                setProductModal({ open: true, editing: null });
              }}
            >
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
          {/* Search & active filter */}
          <div className="flex flex-col gap-2 sm:flex-row">
            <div className="relative flex-1">
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

            <div className="flex items-center gap-1 rounded-lg border border-border bg-surface p-1">
              {(['all', 'active', 'inactive'] as const).map((mode) => (
                <button
                  key={mode}
                  type="button"
                  onClick={() => {
                    setActiveFilter(mode);
                    setPage(1);
                  }}
                  className={cn(
                    'min-h-9 cursor-pointer rounded-md px-3 text-xs font-medium transition-colors',
                    activeFilter === mode
                      ? 'bg-primary text-primary-foreground'
                      : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  {t(`common.${mode}`)}
                </button>
              ))}
            </div>
          </div>

          {/* Category filter pills */}
          {categories.length > 0 ? (
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={() => {
                  setFilterCategoryId(undefined);
                  setPage(1);
                }}
                className={cn(
                  'min-h-9 cursor-pointer rounded-full border px-3 text-xs font-medium transition-colors',
                  !filterCategoryId
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
                )}
              >
                {t('common.all')}
              </button>
              {categories.map((cat) => {
                const active = filterCategoryId === cat.id;
                return (
                  <button
                    key={cat.id}
                    type="button"
                    onClick={() => {
                      setFilterCategoryId(cat.id);
                      setPage(1);
                    }}
                    className={cn(
                      'min-h-9 cursor-pointer rounded-full border px-3 text-xs font-medium transition-colors',
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
            <EmptyState description={t('merchant.catalog.emptyProducts')} />
          ) : (
            <>
              <div className="space-y-2.5">
                {productsQuery.data.items.map((product) => {
                  const stock = toNumber(product.stock);
                  return (
                    <Card key={product.id} className="space-y-3">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0 space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <Package className="size-4 shrink-0 text-primary" aria-hidden="true" />
                            <h2 className="text-sm font-semibold text-foreground">{product.name}</h2>
                            {product.nameEn ? (
                              <span className="text-xs text-muted-foreground" dir="ltr">
                                ({product.nameEn})
                              </span>
                            ) : null}
                          </div>
                          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                            <span>{categoryNameMap.get(product.categoryId) ?? '—'}</span>
                            <span>·</span>
                            <span className="tabular-nums font-semibold text-primary">
                              {formatMoney(product.price, 'IRR', lang)}
                            </span>
                          </div>
                        </div>

                        <div className="flex shrink-0 flex-wrap items-center gap-1.5">
                          <Badge variant={stock > 0 ? 'success' : 'destructive'}>
                            <span className="tabular-nums">
                              {t('merchant.catalog.stockLabel')}: {stock}
                            </span>
                          </Badge>
                          <Badge variant={product.isActive ? 'default' : 'secondary'}>
                            {product.isActive ? t('common.active') : t('common.inactive')}
                          </Badge>
                        </div>
                      </div>

                      <div className="flex flex-wrap items-center gap-2 border-t border-border pt-2.5">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setStockProduct(product)}
                        >
                          <ArrowUpDown className="size-3.5" aria-hidden="true" />
                          <span>{t('merchant.catalog.stockAdjustButton')}</span>
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setImagesProduct(product)}
                        >
                          <ImagePlus className="size-3.5" aria-hidden="true" />
                          <span>{t('merchant.catalog.imagesButton')}</span>
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => setProductModal({ open: true, editing: product })}
                        >
                          <Pencil className="size-3.5" aria-hidden="true" />
                          <span>{t('common.edit')}</span>
                        </Button>
                        <Button
                          type="button"
                          variant="ghost"
                          size="sm"
                          disabled={deleteProductMutation.isPending}
                          onClick={() => {
                            if (window.confirm(t('merchant.catalog.confirmDeleteProduct', { name: product.name }))) {
                              deleteProductMutation.mutate(product.id);
                            }
                          }}
                          className="text-destructive hover:bg-destructive/10"
                        >
                          <Trash2 className="size-3.5" aria-hidden="true" />
                          <span>{t('common.delete')}</span>
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
            <EmptyState description={t('merchant.catalog.emptyCategories')} />
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
                    /{cat.slug} · #{toNumber(cat.sortOrder)}
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
                    onClick={() => {
                      if (window.confirm(t('merchant.catalog.confirmDeleteCategory', { name: cat.name }))) {
                        deleteCategoryMutation.mutate(cat.id);
                      }
                    }}
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

      {/* Category Create/Edit Modal */}
      {categoryModal.open ? (
        <CategoryFormModal
          tenantKey={tenantKey}
          editing={categoryModal.editing}
          onClose={() => setCategoryModal({ open: false, editing: null })}
        />
      ) : null}

      {/* Product Create/Edit Modal */}
      {productModal.open ? (
        <ProductFormModal
          tenantKey={tenantKey}
          categories={categories}
          editing={productModal.editing}
          onClose={() => setProductModal({ open: false, editing: null })}
        />
      ) : null}

      {/* Stock Adjust Modal */}
      {stockProduct ? (
        <StockAdjustModal
          tenantKey={tenantKey}
          product={stockProduct}
          onClose={() => setStockProduct(null)}
        />
      ) : null}

      {/* Product Images Modal */}
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
  const [name, setName] = useState(editing?.name ?? '');
  const [nameEn, setNameEn] = useState(editing?.nameEn ?? '');
  const [slug, setSlug] = useState(editing?.slug ?? '');
  const [sortOrder, setSortOrder] = useState(String(editing ? toNumber(editing.sortOrder) : 0));
  const [isActive, setIsActive] = useState(editing ? editing.isActive : true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () => {
      const parsedOrder = Math.max(0, Number.parseInt(sortOrder, 10) || 0);
      if (editing) {
        const { error } = await apiClient.PATCH('/api/v1/merchant/categories/{id}', {
          params: { path: { id: editing.id } },
          body: {
            name: name.trim(),
            nameEn: nameEn.trim() || null,
            slug: slug.trim().toLowerCase(),
            sortOrder: parsedOrder,
            isActive,
          },
        });
        if (error) {
          throw error;
        }
      } else {
        const { error } = await apiClient.POST('/api/v1/merchant/categories', {
          body: {
            name: name.trim(),
            nameEn: nameEn.trim() || null,
            slug: slug.trim().toLowerCase(),
            sortOrder: parsedOrder,
            isActive,
          },
        });
        if (error) {
          throw error;
        }
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'categories'] });
      onClose();
    },
    onError: (err) => {
      const code = extractProblemCode(err);
      setErrorMsg(
        code === 'slug_taken'
          ? t('merchant.catalog.slugTaken')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setErrorMsg(null);
    if (!name.trim() || !/^[a-z0-9-]+$/.test(slug.trim().toLowerCase())) {
      setErrorMsg(t('onboarding.validationSlug'));
      return;
    }
    mutation.mutate();
  };

  return (
    <ModalDialog
      open
      title={editing ? t('merchant.catalog.editCategory') : t('merchant.catalog.newCategory')}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit} className="space-y-3.5">
        <div className="space-y-1.5">
          <Label htmlFor="cat-name-fa">{t('merchant.catalog.nameFaLabel')}</Label>
          <Input
            id="cat-name-fa"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="cat-name-en">{t('merchant.catalog.nameEnLabel')}</Label>
          <Input
            id="cat-name-en"
            dir="ltr"
            value={nameEn}
            onChange={(e) => {
              const next = e.target.value;
              setNameEn(next);
              if (!editing && !slug) {
                setSlug(slugifyCategoryName(next));
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
              required
              value={slug}
              onChange={(e) => setSlug(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label htmlFor="cat-order">{t('merchant.catalog.sortOrderLabel')}</Label>
            <Input
              id="cat-order"
              dir="ltr"
              type="number"
              min={0}
              value={sortOrder}
              onChange={(e) => setSortOrder(e.target.value)}
            />
          </div>
        </div>

        <div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-2">
          <span className="text-sm font-medium text-foreground">
            {t('merchant.catalog.visibilityLabel')}
          </span>
          <Button
            type="button"
            size="sm"
            variant={isActive ? 'default' : 'secondary'}
            onClick={() => setIsActive((v) => !v)}
          >
            {isActive ? t('common.active') : t('common.inactive')}
          </Button>
        </div>

        {errorMsg ? (
          <p role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
            {errorMsg}
          </p>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </ModalDialog>
  );
}

function ProductFormModal({
  tenantKey,
  categories,
  editing,
  onClose,
}: {
  tenantKey: string;
  categories: CategoryDto[];
  editing: ProductDto | null;
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const lang = normalizeLanguage(i18n.language);

  const [categoryId, setCategoryId] = useState(editing?.categoryId ?? categories[0]?.id ?? '');
  const [name, setName] = useState(editing?.name ?? '');
  const [nameEn, setNameEn] = useState(editing?.nameEn ?? '');
  const [price, setPrice] = useState(editing ? String(toNumber(editing.price)) : '');
  const [stock, setStock] = useState(editing ? String(toNumber(editing.stock)) : '0');
  const [description, setDescription] = useState(editing?.description ?? '');
  const [isActive, setIsActive] = useState(editing ? editing.isActive : true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async () => {
      const parsedPrice = Number(price);
      const parsedStock = Math.max(0, Number.parseInt(stock, 10) || 0);
      if (editing) {
        const { error } = await apiClient.PATCH('/api/v1/merchant/products/{id}', {
          params: { path: { id: editing.id } },
          body: {
            categoryId,
            name: name.trim(),
            nameEn: nameEn.trim() || null,
            description: description.trim() || null,
            price: parsedPrice,
            isActive,
          },
        });
        if (error) {
          throw error;
        }
      } else {
        const { error } = await apiClient.POST('/api/v1/merchant/products', {
          body: {
            categoryId,
            name: name.trim(),
            nameEn: nameEn.trim() || null,
            description: description.trim() || null,
            price: parsedPrice,
            stock: parsedStock,
            isActive,
          },
        });
        if (error) {
          throw error;
        }
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'kpi-products'] });
      onClose();
    },
    onError: (err) => {
      setErrorMsg(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setErrorMsg(null);
    if (!name.trim() || !categoryId || Number.isNaN(Number(price)) || Number(price) < 0) {
      setErrorMsg(t('app.errorGeneric'));
      return;
    }
    mutation.mutate();
  };

  return (
    <ModalDialog
      open
      title={editing ? t('merchant.catalog.editProduct') : t('merchant.catalog.newProduct')}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit} className="space-y-3.5">
        <div className="space-y-1.5">
          <Label htmlFor="prod-category">{t('merchant.catalog.categoryLabel')}</Label>
          <select
            id="prod-category"
            value={categoryId}
            onChange={(e) => setCategoryId(e.target.value)}
            className="flex min-h-11 w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground"
          >
            {categories.map((cat) => (
              <option key={cat.id} value={cat.id}>
                {lang === 'en' && cat.nameEn ? cat.nameEn : cat.name}
              </option>
            ))}
          </select>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="prod-name-fa">{t('merchant.catalog.nameFaLabel')}</Label>
          <Input
            id="prod-name-fa"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="prod-name-en">{t('merchant.catalog.nameEnLabel')}</Label>
          <Input
            id="prod-name-en"
            dir="ltr"
            value={nameEn}
            onChange={(e) => setNameEn(e.target.value)}
          />
        </div>

        <div className={cn('grid gap-3', editing ? 'grid-cols-1' : 'grid-cols-2')}>
          <div className="space-y-1.5">
            <Label htmlFor="prod-price">{t('merchant.catalog.priceLabel')}</Label>
            <Input
              id="prod-price"
              dir="ltr"
              type="number"
              min={0}
              step="0.01"
              required
              value={price}
              onChange={(e) => setPrice(e.target.value)}
            />
          </div>

          {!editing ? (
            <div className="space-y-1.5">
              <Label htmlFor="prod-stock">{t('merchant.catalog.stockLabel')}</Label>
              <Input
                id="prod-stock"
                dir="ltr"
                type="number"
                min={0}
                required
                value={stock}
                onChange={(e) => setStock(e.target.value)}
              />
            </div>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="prod-desc">{t('merchant.catalog.descriptionLabel')}</Label>
          <Textarea
            id="prod-desc"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-2">
          <span className="text-sm font-medium text-foreground">
            {t('merchant.catalog.visibilityLabel')}
          </span>
          <Button
            type="button"
            size="sm"
            variant={isActive ? 'default' : 'secondary'}
            onClick={() => setIsActive((v) => !v)}
          >
            {isActive ? t('common.active') : t('common.inactive')}
          </Button>
        </div>

        {errorMsg ? (
          <p role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
            {errorMsg}
          </p>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? t('common.saving') : t('common.save')}
          </Button>
        </div>
      </form>
    </ModalDialog>
  );
}

function StockAdjustModal({
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
  const [delta, setDelta] = useState('1');
  const [reason, setReason] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const mutation = useMutation({
    mutationFn: async (parsedDelta: number) => {
      const { data, error } = await apiClient.POST('/api/v1/merchant/products/{id}/stock-adjust', {
        params: { path: { id: product.id } },
        body: {
          delta: parsedDelta,
          reason: reason.trim() || null,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to adjust stock');
      }
      return data;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'products'] });
      onClose();
    },
    onError: (err) => {
      const code = extractProblemCode(err);
      setErrorMsg(
        code === 'insufficient_stock'
          ? t('merchant.catalog.insufficientStock')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setErrorMsg(null);
    const parsed = Number.parseInt(delta, 10);
    if (Number.isNaN(parsed) || parsed === 0) {
      setErrorMsg(t('merchant.catalog.deltaLabel'));
      return;
    }
    mutation.mutate(parsed);
  };

  return (
    <ModalDialog
      open
      title={t('merchant.catalog.stockAdjustTitle')}
      description={`${product.name} — ${t('merchant.catalog.currentStock', { count: toNumber(product.stock) })}`}
      onClose={onClose}
    >
      <form onSubmit={handleSubmit} className="space-y-3.5">
        <div className="flex flex-wrap gap-2">
          {[-5, -1, 1, 5, 10].map((preset) => (
            <Button
              key={preset}
              type="button"
              variant="outline"
              size="sm"
              onClick={() => setDelta(String(preset))}
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
            type="number"
            required
            value={delta}
            onChange={(e) => setDelta(e.target.value)}
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="stock-reason">{t('merchant.catalog.deltaReasonLabel')}</Label>
          <Input
            id="stock-reason"
            value={reason}
            onChange={(e) => setReason(e.target.value)}
            placeholder={t('merchant.catalog.deltaReasonPlaceholder')}
          />
        </div>

        {errorMsg ? (
          <p role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive">
            {errorMsg}
          </p>
        ) : null}

        <div className="flex justify-end gap-2 pt-2">
          <Button type="button" variant="outline" onClick={onClose}>
            {t('common.cancel')}
          </Button>
          <Button type="submit" disabled={mutation.isPending}>
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
      setSelectedFile(null);
      setAltText('');
      setErrorMsg(null);
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'product-images', product.id],
      });
    },
    onError: (err) => {
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
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'product-images', product.id],
      });
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
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'product-images', product.id],
      });
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
        <form onSubmit={handleUpload} className="space-y-3 rounded-lg border border-border bg-background p-3">
          <div className="space-y-1.5">
            <Label htmlFor="image-file">{t('merchant.catalog.uploadImageLabel')}</Label>
            <input
              id="image-file"
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif"
              onChange={(e) => handleFileChange(e.target.files?.[0] ?? null)}
              className="block w-full cursor-pointer text-xs text-muted-foreground file:me-3 file:min-h-9 file:cursor-pointer file:rounded-md file:border-0 file:bg-secondary file:px-3 file:text-xs file:font-medium file:text-secondary-foreground"
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

          <Button type="submit" size="sm" disabled={!selectedFile || uploadMutation.isPending}>
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
          <EmptyState description={t('merchant.catalog.emptyImages')} />
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
                <div className="space-y-2 p-2.5">
                  <div className="flex items-center justify-between gap-1">
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
                        className="min-h-9 px-2 text-xs"
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
                      className="size-9 text-destructive hover:bg-destructive/10"
                    >
                      <Trash2 className="size-4" aria-hidden="true" />
                    </Button>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </ModalDialog>
  );
}
