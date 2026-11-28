import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Check,
  ChevronDown,
  ChevronUp,
  CreditCard,
  Image as ImageIcon,
  LogOut,
  Package,
  Pencil,
  Plus,
  Save,
  Store,
  Trash2,
  Truck,
  Upload,
} from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemMessage } from '@/shared/api/client';
import { uploadDefaultProductImage, uploadShopLogo } from '@/shared/api/uploads';
import { formatMoney, toLatinDigits, toNumber } from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { triggerHaptic } from '@/shared/lib/telegram';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { ConfirmDialog } from '@/shared/ui/confirm-dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { ErrorState, LoadingState } from '@/shared/ui/state-views';
import { Switch } from '@/shared/ui/switch';
import { Textarea } from '@/shared/ui/textarea';
import type { components } from '@/types/api';
import { parseSettingDisplayValue, serializeSettingValue } from './merchant-state';
import { ShippingMethodModal } from './ShippingMethodModal';

type ShippingMethodDto = components['schemas']['ShippingMethodDto'];

interface CustomSettingRow {
  id: string;
  key: string;
  value: string;
}

const KNOWN_KEYS = new Set(['support_telegram', 'store_bio', 'shipping_note']);
const ALLOWED_IMAGE_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp', 'image/gif']);
const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

export function MerchantSettingsPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { session, telegram, updateActiveTenant, signOut } = useAuth();
  const lang = normalizeLanguage(i18n.language);
  const activeShop = session?.activeTenant;
  const tenantKey = activeShop?.id ?? 'none';

  // Section 1: Basic Shop Profile
  const [shopName, setShopName] = useState(activeShop?.name ?? '');
  const [defaultLanguage, setDefaultLanguage] = useState(activeShop?.defaultLanguage ?? 'fa');
  const [currency, setCurrency] = useState(activeShop?.currency ?? 'IRT');
  const [profileDirty, setProfileDirty] = useState(false);
  const [profileError, setProfileError] = useState<string | null>(null);
  const [profileSaved, setProfileSaved] = useState(false);

  // Section 2: Branding
  const [brandingError, setBrandingError] = useState<string | null>(null);

  // Section 3: Checkout, Payment & Order Approval
  const [requireOrderApproval, setRequireOrderApproval] = useState(false);
  const [enablePayOnDelivery, setEnablePayOnDelivery] = useState(true);
  const [enableCardToCard, setEnableCardToCard] = useState(false);
  const [cardNumber, setCardNumber] = useState('');
  const [cardHolderName, setCardHolderName] = useState('');
  const [bankName, setBankName] = useState('');
  const [paymentExpiryMinutes, setPaymentExpiryMinutes] = useState('60');
  const [checkoutDirty, setCheckoutDirty] = useState(false);
  const [checkoutError, setCheckoutError] = useState<string | null>(null);
  const [checkoutSaved, setCheckoutSaved] = useState(false);

  // Section 4: Shipping Methods modals
  const [shippingModal, setShippingModal] = useState<{
    open: boolean;
    editing: ShippingMethodDto | null;
  }>({ open: false, editing: null });
  const [deletingShipping, setDeletingShipping] = useState<ShippingMethodDto | null>(null);

  // Section 5: Friendly Shop Details + Collapsed Custom Fields
  const [supportTelegram, setSupportTelegram] = useState('');
  const [storeBio, setStoreBio] = useState('');
  const [shippingNote, setShippingNote] = useState('');
  const [customRows, setCustomRows] = useState<CustomSettingRow[]>([]);
  const [advancedOpen, setAdvancedOpen] = useState(false);
  const [detailsDirty, setDetailsDirty] = useState(false);
  const [detailsError, setDetailsError] = useState<string | null>(null);
  const [detailsSaved, setDetailsSaved] = useState(false);

  const shopQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'shop-profile'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/shop');
      if (error || !data) {
        throw new Error('Failed to load shop profile');
      }
      return data;
    },
  });

  const shippingQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'shipping-methods'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/shipping-methods');
      if (error || !data) {
        throw new Error('Failed to load shipping methods');
      }
      return data;
    },
  });

  const settingsQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'settings'],
    enabled: Boolean(activeShop),
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/settings');
      if (error || !data) {
        throw new Error('Failed to load settings');
      }
      return data;
    },
  });

  useEffect(() => {
    if (!shopQuery.data) {
      return;
    }
    setShopName(shopQuery.data.name);
    setDefaultLanguage(shopQuery.data.defaultLanguage);
    setCurrency(shopQuery.data.currency);
    setProfileDirty(false);
    setProfileError(null);

    setRequireOrderApproval(shopQuery.data.requireOrderApproval);
    setEnablePayOnDelivery(shopQuery.data.enablePayOnDelivery);
    setEnableCardToCard(shopQuery.data.enableCardToCard);
    setCardNumber(shopQuery.data.cardNumber ?? '');
    setCardHolderName(shopQuery.data.cardHolderName ?? '');
    setBankName(shopQuery.data.bankName ?? '');
    setPaymentExpiryMinutes(String(toNumber(shopQuery.data.paymentExpiryMinutes, 60)));
    setCheckoutDirty(false);
    setCheckoutError(null);
  }, [tenantKey, shopQuery.data]);

  useEffect(() => {
    if (!settingsQuery.data) {
      return;
    }
    const map = new Map(settingsQuery.data.map((item) => [item.key, item.value]));
    setSupportTelegram(
      map.has('support_telegram') ? parseSettingDisplayValue(map.get('support_telegram') ?? '') : '',
    );
    setStoreBio(map.has('store_bio') ? parseSettingDisplayValue(map.get('store_bio') ?? '') : '');
    setShippingNote(
      map.has('shipping_note') ? parseSettingDisplayValue(map.get('shipping_note') ?? '') : '',
    );

    const extras: CustomSettingRow[] = settingsQuery.data
      .filter((item) => !KNOWN_KEYS.has(item.key))
      .map((item, idx) => ({
        id: `${tenantKey}-custom-${idx}-${item.key}`,
        key: item.key,
        value: parseSettingDisplayValue(item.value),
      }));

    setCustomRows(extras);
    setDetailsDirty(false);
    setDetailsError(null);
  }, [tenantKey, settingsQuery.data]);

  const updateProfileMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await apiClient.PATCH('/api/v1/merchant/shop', {
        body: {
          name: shopName.trim(),
          defaultLanguage,
          currency,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to update shop profile');
      }
      return data;
    },
    onSuccess: (updated) => {
      triggerHaptic('success');
      updateActiveTenant({
        name: updated.name,
        defaultLanguage: updated.defaultLanguage,
        currency: updated.currency,
        logoUrl: updated.logoUrl,
        defaultProductImageUrl: updated.defaultProductImageUrl,
      });
      setProfileDirty(false);
      setProfileError(null);
      setProfileSaved(true);
      window.setTimeout(() => setProfileSaved(false), 2500);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shop-profile'] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-shop', updated.slug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setProfileError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const updateCheckoutSettingsMutation = useMutation({
    mutationFn: async () => {
      const parsedExpiry = Math.min(
        10080,
        Math.max(5, Number.parseInt(toLatinDigits(paymentExpiryMinutes), 10) || 60),
      );
      const { data, error } = await apiClient.PATCH('/api/v1/merchant/shop', {
        body: {
          requireOrderApproval,
          enablePayOnDelivery,
          enableCardToCard,
          cardNumber: toLatinDigits(cardNumber).trim(),
          cardHolderName: cardHolderName.trim(),
          bankName: bankName.trim(),
          paymentExpiryMinutes: parsedExpiry,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to update checkout settings');
      }
      return data;
    },
    onSuccess: (updated) => {
      triggerHaptic('success');
      setCheckoutDirty(false);
      setCheckoutError(null);
      setCheckoutSaved(true);
      window.setTimeout(() => setCheckoutSaved(false), 2500);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shop-profile'] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-shop', updated.slug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setCheckoutError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const deleteShippingMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await apiClient.DELETE('/api/v1/merchant/shipping-methods/{id}', {
        params: { path: { id } },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      setDeletingShipping(null);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shipping-methods'] });
    },
  });

  const uploadLogoMutation = useMutation({
    mutationFn: async (file: File) => {
      const result = await uploadShopLogo(file);
      if (result.error || !result.data) {
        throw result.error ?? new Error('Logo upload failed');
      }
      return result.data;
    },
    onSuccess: (updated) => {
      triggerHaptic('success');
      setBrandingError(null);
      updateActiveTenant({ logoUrl: updated.logoUrl });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shop-profile'] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-shop', updated.slug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setBrandingError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const deleteLogoMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await apiClient.DELETE('/api/v1/merchant/shop/logo');
      if (error || !data) {
        throw error ?? new Error('Logo delete failed');
      }
      return data;
    },
    onSuccess: (updated) => {
      triggerHaptic('success');
      setBrandingError(null);
      updateActiveTenant({ logoUrl: updated.logoUrl });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shop-profile'] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-shop', updated.slug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setBrandingError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const uploadDefaultImgMutation = useMutation({
    mutationFn: async (file: File) => {
      const result = await uploadDefaultProductImage(file);
      if (result.error || !result.data) {
        throw result.error ?? new Error('Default image upload failed');
      }
      return result.data;
    },
    onSuccess: (updated) => {
      triggerHaptic('success');
      setBrandingError(null);
      updateActiveTenant({ defaultProductImageUrl: updated.defaultProductImageUrl });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shop-profile'] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-shop', updated.slug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setBrandingError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const deleteDefaultImgMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await apiClient.DELETE('/api/v1/merchant/shop/default-product-image');
      if (error || !data) {
        throw error ?? new Error('Default image delete failed');
      }
      return data;
    },
    onSuccess: (updated) => {
      triggerHaptic('success');
      setBrandingError(null);
      updateActiveTenant({ defaultProductImageUrl: updated.defaultProductImageUrl });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shop-profile'] });
      void queryClient.invalidateQueries({ queryKey: ['storefront-shop', updated.slug] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setBrandingError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const saveSettingsMutation = useMutation({
    mutationFn: async (payload: { key: string; value: string }[]) => {
      const { data, error } = await apiClient.PUT('/api/v1/merchant/settings', {
        body: { settings: payload },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to save settings');
      }
      return data;
    },
    onSuccess: () => {
      triggerHaptic('success');
      setDetailsDirty(false);
      setDetailsError(null);
      setDetailsSaved(true);
      window.setTimeout(() => setDetailsSaved(false), 2500);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'settings'] });
    },
    onError: (err) => {
      triggerHaptic('error');
      setDetailsError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const handleProfileSubmit = (event: FormEvent) => {
    event.preventDefault();
    setProfileError(null);
    if (shopName.trim().length < 2) {
      setProfileError(t('onboarding.validationName'));
      return;
    }
    updateProfileMutation.mutate();
  };

  const handleCheckoutSettingsSubmit = (event: FormEvent) => {
    event.preventDefault();
    setCheckoutError(null);
    const digitsOnly = toLatinDigits(cardNumber).replace(/\D/g, '');
    if (enableCardToCard && digitsOnly.length < 12) {
      setCheckoutError(t('merchant.checkoutSettings.cardNumberRequired'));
      return;
    }
    updateCheckoutSettingsMutation.mutate();
  };

  const handleBrandingFile = (kind: 'logo' | 'default', file: File | null) => {
    setBrandingError(null);
    if (!file) {
      return;
    }
    if (!ALLOWED_IMAGE_TYPES.has(file.type)) {
      setBrandingError(t('merchant.catalog.invalidFileType'));
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setBrandingError(t('merchant.catalog.fileTooLarge'));
      return;
    }
    if (kind === 'logo') {
      uploadLogoMutation.mutate(file);
    } else {
      uploadDefaultImgMutation.mutate(file);
    }
  };

  const handleDetailsSubmit = (event: FormEvent) => {
    event.preventDefault();
    setDetailsError(null);
    setDetailsSaved(false);

    const seen = new Set<string>();
    const payload: { key: string; value: string }[] = [];

    if (supportTelegram.trim()) {
      seen.add('support_telegram');
      payload.push({
        key: 'support_telegram',
        value: serializeSettingValue(supportTelegram),
      });
    }
    if (storeBio.trim()) {
      seen.add('store_bio');
      payload.push({
        key: 'store_bio',
        value: serializeSettingValue(storeBio),
      });
    }
    if (shippingNote.trim()) {
      seen.add('shipping_note');
      payload.push({
        key: 'shipping_note',
        value: serializeSettingValue(shippingNote),
      });
    }

    for (const row of customRows) {
      const k = row.key.trim();
      const v = row.value.trim();
      if (!k) {
        setDetailsError(t('merchant.settings.emptyKey'));
        return;
      }
      if (seen.has(k)) {
        setDetailsError(t('merchant.settings.duplicateKey'));
        return;
      }
      seen.add(k);
      payload.push({
        key: k,
        value: serializeSettingValue(v, { preserveStructuredJson: true }),
      });
    }

    saveSettingsMutation.mutate(payload);
  };

  const shopProfile = shopQuery.data;
  const logoUrl = shopProfile?.logoUrl ?? activeShop?.logoUrl ?? null;
  const defaultProductImageUrl =
    shopProfile?.defaultProductImageUrl ?? activeShop?.defaultProductImageUrl ?? null;
  const shippingMethods = shippingQuery.data ?? [];

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('merchant.settings.title')}
        </h1>
        <p className="mt-0.5 text-xs text-muted-foreground">{t('merchant.settings.subtitle')}</p>
      </div>

      {/* Section 1: Basic Shop Profile */}
      <Card>
        <CardHeader>
          <CardTitle>{t('merchant.settings.profileTitle')}</CardTitle>
          <CardDescription>{t('merchant.settings.profileDesc')}</CardDescription>
        </CardHeader>

        {shopQuery.isLoading ? (
          <LoadingState rows={2} />
        ) : shopQuery.isError ? (
          <ErrorState onRetry={() => void shopQuery.refetch()} />
        ) : (
          <form onSubmit={handleProfileSubmit} className="space-y-4">
            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="settings-shop-name">{t('merchant.settings.shopNameLabel')}</Label>
                <Input
                  id="settings-shop-name"
                  value={shopName}
                  maxLength={120}
                  onChange={(e) => {
                    setShopName(e.target.value);
                    setProfileDirty(true);
                  }}
                />
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="settings-shop-slug">
                  {t('merchant.settings.shopSlugReadonly')}
                </Label>
                <Input
                  id="settings-shop-slug"
                  dir="ltr"
                  value={shopProfile?.slug ?? activeShop?.slug ?? ''}
                  disabled
                />
              </div>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="settings-shop-lang">{t('onboarding.languageLabel')}</Label>
                <Select
                  id="settings-shop-lang"
                  value={defaultLanguage}
                  onChange={(e) => {
                    setDefaultLanguage(e.target.value);
                    setProfileDirty(true);
                  }}
                >
                  <option value="fa">{t('app.persian')}</option>
                  <option value="en">{t('app.english')}</option>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="settings-shop-currency">{t('onboarding.currencyLabel')}</Label>
                <Select
                  id="settings-shop-currency"
                  value={currency}
                  onChange={(e) => {
                    setCurrency(e.target.value);
                    setProfileDirty(true);
                  }}
                >
                  <option value="IRT">{t('currencies.IRT')}</option>
                  <option value="IRR">{t('currencies.IRR')}</option>
                  <option value="USD">{t('currencies.USD')}</option>
                  <option value="EUR">{t('currencies.EUR')}</option>
                </Select>
              </div>
            </div>

            {profileError ? (
              <p
                role="alert"
                className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
              >
                {profileError}
              </p>
            ) : null}

            {profileSaved ? (
              <p
                role="status"
                className="flex items-center gap-1.5 rounded-lg border border-success/40 bg-success/10 px-3 py-2 text-xs text-success"
              >
                <Check className="size-4" aria-hidden="true" />
                <span>{t('merchant.settings.profileSaved')}</span>
              </p>
            ) : null}

            <div className="flex justify-end">
              <Button
                type="submit"
                disabled={!profileDirty || updateProfileMutation.isPending}
                className="w-full sm:w-auto"
              >
                <Save className="size-4" aria-hidden="true" />
                <span>
                  {updateProfileMutation.isPending
                    ? t('common.saving')
                    : t('merchant.settings.saveProfile')}
                </span>
              </Button>
            </div>
          </form>
        )}
      </Card>

      {/* Section 2: Branding (Logo & Default Product Image) */}
      <Card className="space-y-4">
        <CardHeader className="mb-0">
          <CardTitle>{t('merchant.settings.brandingTitle')}</CardTitle>
          <CardDescription>{t('merchant.settings.brandingDesc')}</CardDescription>
        </CardHeader>

        {brandingError ? (
          <p
            role="alert"
            className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
          >
            {brandingError}
          </p>
        ) : null}

        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {/* Shop Logo */}
          <div className="space-y-3 rounded-xl border border-border bg-background p-3.5">
            <div className="flex items-center gap-3">
              <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-border bg-surface-elevated">
                {logoUrl ? (
                  <img src={logoUrl} alt={shopName} className="size-full object-cover" />
                ) : (
                  <ImageIcon className="size-6 text-muted-foreground" aria-hidden="true" />
                )}
              </div>
              <div className="min-w-0">
                <p className="text-sm font-semibold text-foreground">
                  {t('merchant.settings.logoLabel')}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {t('merchant.settings.logoHint')}
                </p>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              <label className="inline-flex min-h-11 flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-xs font-medium text-foreground transition-colors hover:bg-surface-elevated">
                <Upload className="size-4" aria-hidden="true" />
                <span>
                  {uploadLogoMutation.isPending
                    ? t('merchant.catalog.uploading')
                    : t('merchant.settings.uploadLogo')}
                </span>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  disabled={uploadLogoMutation.isPending}
                  onChange={(e) => {
                    handleBrandingFile('logo', e.target.files?.[0] ?? null);
                    e.target.value = '';
                  }}
                  className="sr-only"
                />
              </label>

              {logoUrl ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={deleteLogoMutation.isPending}
                  onClick={() => deleteLogoMutation.mutate()}
                  className="text-destructive hover:bg-destructive/10"
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                  <span>{t('merchant.settings.removeLogo')}</span>
                </Button>
              ) : null}
            </div>
          </div>

          {/* Default Product Image */}
          <div className="space-y-3 rounded-xl border border-border bg-background p-3.5">
            <div className="flex items-center gap-3">
              <div className="flex size-16 shrink-0 items-center justify-center overflow-hidden rounded-xl border border-border bg-surface-elevated">
                {defaultProductImageUrl ? (
                  <img
                    src={defaultProductImageUrl}
                    alt={t('merchant.settings.defaultProductImageLabel')}
                    className="size-full object-cover"
                  />
                ) : (
                  <Package className="size-6 text-muted-foreground" aria-hidden="true" />
                )}
              </div>
              <div className="min-w-0">
                <p className="text-sm font-semibold text-foreground">
                  {t('merchant.settings.defaultProductImageLabel')}
                </p>
                <p className="mt-0.5 text-xs text-muted-foreground">
                  {t('merchant.settings.defaultProductImageHint')}
                </p>
              </div>
            </div>

            <div className="flex flex-wrap gap-2">
              <label className="inline-flex min-h-11 flex-1 cursor-pointer items-center justify-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-xs font-medium text-foreground transition-colors hover:bg-surface-elevated">
                <Upload className="size-4" aria-hidden="true" />
                <span>
                  {uploadDefaultImgMutation.isPending
                    ? t('merchant.catalog.uploading')
                    : t('merchant.settings.uploadDefaultProductImage')}
                </span>
                <input
                  type="file"
                  accept="image/jpeg,image/png,image/webp,image/gif"
                  disabled={uploadDefaultImgMutation.isPending}
                  onChange={(e) => {
                    handleBrandingFile('default', e.target.files?.[0] ?? null);
                    e.target.value = '';
                  }}
                  className="sr-only"
                />
              </label>

              {defaultProductImageUrl ? (
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  disabled={deleteDefaultImgMutation.isPending}
                  onClick={() => deleteDefaultImgMutation.mutate()}
                  className="text-destructive hover:bg-destructive/10"
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                  <span>{t('merchant.settings.removeDefaultProductImage')}</span>
                </Button>
              ) : null}
            </div>
          </div>
        </div>
      </Card>

      {/* Section 3: Checkout, Payment & Order Approval */}
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <CreditCard className="size-4 text-primary" aria-hidden="true" />
            <CardTitle>{t('merchant.checkoutSettings.sectionTitle')}</CardTitle>
          </div>
          <CardDescription>{t('merchant.checkoutSettings.sectionDesc')}</CardDescription>
        </CardHeader>

        {shopQuery.isLoading ? (
          <LoadingState rows={2} />
        ) : (
          <form onSubmit={handleCheckoutSettingsSubmit} className="space-y-4">
            <div className="space-y-1.5 rounded-xl border border-border bg-background p-3">
              <div className="flex items-center justify-between gap-2">
                <Label htmlFor="chk-require-approval" className="cursor-pointer">
                  {t('merchant.checkoutSettings.requireApprovalLabel')}
                </Label>
                <Switch
                  id="chk-require-approval"
                  checked={requireOrderApproval}
                  onCheckedChange={(next) => {
                    setRequireOrderApproval(next);
                    setCheckoutDirty(true);
                  }}
                  ariaLabel={t('merchant.checkoutSettings.requireApprovalLabel')}
                />
              </div>
              <p className="text-xs text-muted-foreground">
                {t('merchant.checkoutSettings.requireApprovalHint')}
              </p>
            </div>

            <div className="flex items-center justify-between gap-2 rounded-xl border border-border bg-background px-3 py-2">
              <Label htmlFor="chk-pay-on-delivery" className="cursor-pointer">
                {t('merchant.checkoutSettings.enablePayOnDeliveryLabel')}
              </Label>
              <Switch
                id="chk-pay-on-delivery"
                checked={enablePayOnDelivery}
                onCheckedChange={(next) => {
                  setEnablePayOnDelivery(next);
                  setCheckoutDirty(true);
                }}
                ariaLabel={t('merchant.checkoutSettings.enablePayOnDeliveryLabel')}
              />
            </div>

            <div className="space-y-3 rounded-xl border border-border bg-background p-3">
              <div className="flex items-center justify-between gap-2">
                <Label htmlFor="chk-card-to-card" className="cursor-pointer">
                  {t('merchant.checkoutSettings.enableCardToCardLabel')}
                </Label>
                <Switch
                  id="chk-card-to-card"
                  checked={enableCardToCard}
                  onCheckedChange={(next) => {
                    setEnableCardToCard(next);
                    setCheckoutDirty(true);
                  }}
                  ariaLabel={t('merchant.checkoutSettings.enableCardToCardLabel')}
                />
              </div>

              {enableCardToCard ? (
                <div className="space-y-3 border-t border-border pt-3">
                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div className="space-y-1.5">
                      <Label htmlFor="chk-card-number">
                        {t('merchant.checkoutSettings.cardNumberLabel')}
                      </Label>
                      <Input
                        id="chk-card-number"
                        dir="ltr"
                        inputMode="numeric"
                        maxLength={25}
                        value={cardNumber}
                        placeholder={t('merchant.checkoutSettings.cardNumberPlaceholder')}
                        onChange={(e) => {
                          setCardNumber(e.target.value);
                          setCheckoutDirty(true);
                        }}
                      />
                    </div>

                    <div className="space-y-1.5">
                      <Label htmlFor="chk-card-holder">
                        {t('merchant.checkoutSettings.cardHolderLabel')}
                      </Label>
                      <Input
                        id="chk-card-holder"
                        maxLength={120}
                        value={cardHolderName}
                        placeholder={t('merchant.checkoutSettings.cardHolderPlaceholder')}
                        onChange={(e) => {
                          setCardHolderName(e.target.value);
                          setCheckoutDirty(true);
                        }}
                      />
                    </div>
                  </div>

                  <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                    <div className="space-y-1.5">
                      <Label htmlFor="chk-bank-name">
                        {t('merchant.checkoutSettings.bankNameLabel')}
                      </Label>
                      <Input
                        id="chk-bank-name"
                        maxLength={80}
                        value={bankName}
                        placeholder={t('merchant.checkoutSettings.bankNamePlaceholder')}
                        onChange={(e) => {
                          setBankName(e.target.value);
                          setCheckoutDirty(true);
                        }}
                      />
                    </div>

                    <div className="space-y-1.5">
                      <Label htmlFor="chk-expiry-min">
                        {t('merchant.checkoutSettings.expiryMinutesLabel')}
                      </Label>
                      <Input
                        id="chk-expiry-min"
                        dir="ltr"
                        inputMode="numeric"
                        value={paymentExpiryMinutes}
                        onChange={(e) => {
                          setPaymentExpiryMinutes(e.target.value);
                          setCheckoutDirty(true);
                        }}
                      />
                    </div>
                  </div>

                  <p className="text-xs text-muted-foreground">
                    {t('merchant.checkoutSettings.expiryMinutesHint')}
                  </p>
                </div>
              ) : null}
            </div>

            {checkoutError ? (
              <p
                role="alert"
                className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
              >
                {checkoutError}
              </p>
            ) : null}

            {checkoutSaved ? (
              <p
                role="status"
                className="flex items-center gap-1.5 rounded-lg border border-success/40 bg-success/10 px-3 py-2 text-xs text-success"
              >
                <Check className="size-4" aria-hidden="true" />
                <span>{t('merchant.checkoutSettings.checkoutSettingsSaved')}</span>
              </p>
            ) : null}

            <div className="flex justify-end">
              <Button
                type="submit"
                disabled={!checkoutDirty || updateCheckoutSettingsMutation.isPending}
                className="w-full sm:w-auto"
              >
                <Save className="size-4" aria-hidden="true" />
                <span>
                  {updateCheckoutSettingsMutation.isPending
                    ? t('common.saving')
                    : t('merchant.checkoutSettings.saveCheckoutSettings')}
                </span>
              </Button>
            </div>
          </form>
        )}
      </Card>

      {/* Section 4: Shipping Methods & Rates */}
      <Card className="space-y-4">
        <div className="flex flex-wrap items-start justify-between gap-2">
          <div>
            <div className="flex items-center gap-2">
              <Truck className="size-4 text-primary" aria-hidden="true" />
              <CardTitle>{t('merchant.shipping.sectionTitle')}</CardTitle>
            </div>
            <CardDescription className="mt-1">
              {t('merchant.shipping.sectionDesc')}
            </CardDescription>
          </div>
          <Button
            type="button"
            size="sm"
            onClick={() => setShippingModal({ open: true, editing: null })}
          >
            <Plus className="size-4" aria-hidden="true" />
            <span>{t('merchant.shipping.addNew')}</span>
          </Button>
        </div>

        {shippingQuery.isLoading ? (
          <LoadingState rows={2} />
        ) : shippingQuery.isError ? (
          <ErrorState onRetry={() => void shippingQuery.refetch()} />
        ) : shippingMethods.length === 0 ? (
          <p className="rounded-xl border border-dashed border-border bg-background p-4 text-center text-xs text-muted-foreground">
            {t('merchant.shipping.empty')}
          </p>
        ) : (
          <div className="divide-y divide-border rounded-xl border border-border bg-background">
            {shippingMethods.map((method) => {
              const priceVal = toNumber(method.price);
              const freeAboveVal =
                method.freeAboveAmount !== null && method.freeAboveAmount !== undefined
                  ? toNumber(method.freeAboveAmount)
                  : null;

              return (
                <div
                  key={method.id}
                  className={
                    method.isActive
                      ? 'flex flex-wrap items-center justify-between gap-3 p-3.5'
                      : 'flex flex-wrap items-center justify-between gap-3 p-3.5 opacity-60'
                  }
                >
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-sm font-semibold text-foreground">{method.name}</p>
                      {method.isPayAtDestination ? (
                        <Badge variant="secondary">{t('orders.payAtDestination')}</Badge>
                      ) : priceVal === 0 ? (
                        <Badge variant="secondary">{t('orders.shippingFree')}</Badge>
                      ) : (
                        <span className="tabular-nums text-xs font-semibold text-primary">
                          {formatMoney(priceVal, currency, lang)}
                        </span>
                      )}
                      {method.isActive ? (
                        <Badge variant="secondary">{t('merchant.shipping.activeLabel')}</Badge>
                      ) : null}
                    </div>

                    {method.description ? (
                      <p className="text-xs text-muted-foreground">{method.description}</p>
                    ) : null}

                    <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                      {method.estimatedDaysText ? <span>{method.estimatedDaysText}</span> : null}
                      {!method.isPayAtDestination && freeAboveVal !== null ? (
                        <span className="tabular-nums">
                          {t('orders.shippingFree')}: ≥ {formatMoney(freeAboveVal, currency, lang)}
                        </span>
                      ) : null}
                    </div>
                  </div>

                  <div className="flex items-center gap-1.5">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => setShippingModal({ open: true, editing: method })}
                    >
                      <Pencil className="size-3.5" aria-hidden="true" />
                      <span>{t('common.edit')}</span>
                    </Button>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={t('common.delete')}
                      onClick={() => setDeletingShipping(method)}
                      className="text-destructive hover:bg-destructive/10"
                    >
                      <Trash2 className="size-4" aria-hidden="true" />
                    </Button>
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </Card>

      {/* Section 5: Friendly Storefront Details (zero JSON jargon!) */}
      <Card>
        <CardHeader>
          <CardTitle>{t('merchant.settings.shopInfoTitle')}</CardTitle>
          <CardDescription>{t('merchant.settings.shopInfoDesc')}</CardDescription>
        </CardHeader>

        {settingsQuery.isLoading ? (
          <LoadingState rows={2} />
        ) : settingsQuery.isError ? (
          <ErrorState onRetry={() => void settingsQuery.refetch()} />
        ) : (
          <form onSubmit={handleDetailsSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <Label htmlFor="setting-support-tg">
                {t('merchant.settings.supportTelegramLabel')}
              </Label>
              <Input
                id="setting-support-tg"
                dir="ltr"
                value={supportTelegram}
                placeholder={t('merchant.settings.supportTelegramPlaceholder')}
                onChange={(e) => {
                  setSupportTelegram(e.target.value);
                  setDetailsDirty(true);
                }}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="setting-store-bio">{t('merchant.settings.storeBioLabel')}</Label>
              <Textarea
                id="setting-store-bio"
                value={storeBio}
                placeholder={t('merchant.settings.storeBioPlaceholder')}
                onChange={(e) => {
                  setStoreBio(e.target.value);
                  setDetailsDirty(true);
                }}
              />
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="setting-shipping-note">
                {t('merchant.settings.shippingNoteLabel')}
              </Label>
              <Textarea
                id="setting-shipping-note"
                value={shippingNote}
                placeholder={t('merchant.settings.shippingNotePlaceholder')}
                onChange={(e) => {
                  setShippingNote(e.target.value);
                  setDetailsDirty(true);
                }}
              />
            </div>

            {/* Collapsible advanced extra fields */}
            <div className="rounded-xl border border-border bg-background">
              <button
                type="button"
                aria-expanded={advancedOpen}
                onClick={() => setAdvancedOpen((prev) => !prev)}
                className="flex min-h-11 w-full cursor-pointer items-center justify-between gap-2 px-3.5 py-2 text-start text-xs font-semibold text-muted-foreground hover:text-foreground"
              >
                <span>{t('merchant.settings.advancedToggle')}</span>
                {advancedOpen ? (
                  <ChevronUp className="size-4" aria-hidden="true" />
                ) : (
                  <ChevronDown className="size-4" aria-hidden="true" />
                )}
              </button>

              {advancedOpen ? (
                <div className="space-y-3 border-t border-border p-3.5">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <p className="text-xs text-muted-foreground">
                      {t('merchant.settings.advancedDesc')}
                    </p>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        setCustomRows((prev) => [
                          ...prev,
                          {
                            id: `new-${Date.now()}-${Math.random().toString(36).slice(2)}`,
                            key: '',
                            value: '',
                          },
                        ]);
                        setDetailsDirty(true);
                      }}
                    >
                      <Plus className="size-4" aria-hidden="true" />
                      <span>{t('merchant.settings.addRow')}</span>
                    </Button>
                  </div>

                  {customRows.length === 0 ? (
                    <p className="text-xs text-muted-foreground">{t('merchant.settings.empty')}</p>
                  ) : (
                    <div className="space-y-2.5">
                      {customRows.map((row) => (
                        <div
                          key={row.id}
                          className="grid grid-cols-1 gap-2 rounded-lg border border-border bg-surface p-3 sm:grid-cols-[1fr_1.4fr_auto] sm:items-end"
                        >
                          <div className="space-y-1">
                            <Label htmlFor={`ckey-${row.id}`}>
                              {t('merchant.settings.keyLabel')}
                            </Label>
                            <Input
                              id={`ckey-${row.id}`}
                              dir="ltr"
                              value={row.key}
                              placeholder={t('merchant.settings.keyPlaceholder')}
                              onChange={(e) => {
                                setCustomRows((prev) =>
                                  prev.map((item) =>
                                    item.id === row.id ? { ...item, key: e.target.value } : item,
                                  ),
                                );
                                setDetailsDirty(true);
                              }}
                            />
                          </div>

                          <div className="space-y-1">
                            <Label htmlFor={`cval-${row.id}`}>
                              {t('merchant.settings.valueLabel')}
                            </Label>
                            <Input
                              id={`cval-${row.id}`}
                              value={row.value}
                              placeholder={t('merchant.settings.valuePlaceholder')}
                              onChange={(e) => {
                                setCustomRows((prev) =>
                                  prev.map((item) =>
                                    item.id === row.id ? { ...item, value: e.target.value } : item,
                                  ),
                                );
                                setDetailsDirty(true);
                              }}
                            />
                          </div>

                          <Button
                            type="button"
                            variant="ghost"
                            size="icon"
                            aria-label={t('common.delete')}
                            onClick={() => {
                              setCustomRows((prev) => prev.filter((item) => item.id !== row.id));
                              setDetailsDirty(true);
                            }}
                            className="text-destructive hover:bg-destructive/10"
                          >
                            <Trash2 className="size-4" aria-hidden="true" />
                          </Button>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ) : null}
            </div>

            {detailsError ? (
              <p
                role="alert"
                className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
              >
                {detailsError}
              </p>
            ) : null}

            {detailsSaved ? (
              <p
                role="status"
                className="flex items-center gap-1.5 rounded-lg border border-success/40 bg-success/10 px-3 py-2 text-xs text-success"
              >
                <Check className="size-4" aria-hidden="true" />
                <span>{t('common.saved')}</span>
              </p>
            ) : null}

            <div className="flex justify-end">
              <Button
                type="submit"
                disabled={!detailsDirty || saveSettingsMutation.isPending}
                className="w-full sm:w-auto"
              >
                <Save className="size-4" aria-hidden="true" />
                <span>
                  {saveSettingsMutation.isPending ? t('common.saving') : t('common.save')}
                </span>
              </Button>
            </div>
          </form>
        )}
      </Card>

      {/* Section 6: Session & Account */}
      <Card className={telegram.inTelegram ? 'space-y-3 lg:hidden' : 'space-y-3'}>
        <CardHeader className="mb-0">
          <CardTitle>{t('merchant.settings.sessionSectionTitle')}</CardTitle>
          <CardDescription>{activeShop?.name}</CardDescription>
        </CardHeader>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline" size="sm" className="lg:hidden">
            <Link to="/auth">
              <Store className="size-4" aria-hidden="true" />
              <span>{t('merchant.switchShop')}</span>
            </Link>
          </Button>

          {!telegram.inTelegram ? (
            <Button
              type="button"
              variant="destructive"
              size="sm"
              onClick={() => {
                signOut();
                void navigate('/auth', { replace: true });
              }}
            >
              <LogOut className="size-4" aria-hidden="true" />
              <span>{t('auth.signOut')}</span>
            </Button>
          ) : null}
        </div>
      </Card>

      {shippingModal.open ? (
        <ShippingMethodModal
          tenantKey={tenantKey}
          currency={currency}
          editing={shippingModal.editing}
          onClose={() => setShippingModal({ open: false, editing: null })}
        />
      ) : null}

      {deletingShipping ? (
        <ConfirmDialog
          open
          title={t('merchant.shipping.deleteTitle')}
          description={t('merchant.shipping.confirmDelete', { name: deletingShipping.name })}
          busy={deleteShippingMutation.isPending}
          onConfirm={() => deleteShippingMutation.mutate(deletingShipping.id)}
          onCancel={() => setDeletingShipping(null)}
        />
      ) : null}
    </div>
  );
}
