import { zodResolver } from '@hookform/resolvers/zod';
import { ArrowLeft, ArrowRight, Store } from 'lucide-react';
import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { z } from 'zod';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import { pushTelegramBackButton, triggerHaptic } from '@/shared/lib/telegram';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Select } from '@/shared/ui/select';
import { ToolbarControls } from '@/shared/ui/toolbar-controls';
import { useAuth } from './auth-context';

const onboardingSchema = z.object({
  name: z.string().trim().min(2).max(120),
  slug: z
    .string()
    .trim()
    .toLowerCase()
    .regex(/^[a-z0-9-]{3,40}$/),
  defaultLanguage: z.enum(['fa', 'en']),
  currency: z.enum(['IRT', 'IRR', 'USD', 'EUR']),
});

type OnboardingValues = z.infer<typeof onboardingSchema>;

export function OnboardingPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { session, refreshSessionAfterShopCreated } = useAuth();
  const [serverError, setServerError] = useState<string | null>(null);

  const lang = normalizeLanguage(i18n.language);
  const BackIcon = getDirection(lang) === 'rtl' ? ArrowRight : ArrowLeft;
  const canGoBack = Boolean(session && session.tenants.length > 0);

  useEffect(() => {
    if (!canGoBack) {
      return;
    }
    return pushTelegramBackButton(() => {
      void navigate('/auth');
    });
  }, [canGoBack, navigate]);

  const {
    register,
    watch,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<OnboardingValues>({
    resolver: zodResolver(onboardingSchema),
    defaultValues: {
      name: '',
      slug: '',
      defaultLanguage: 'fa',
      currency: 'IRT',
    },
  });

  const slugPreview = watch('slug')?.trim().toLowerCase() || 'noor-gallery';

  const onSubmit = async (values: OnboardingValues) => {
    setServerError(null);
    const { data, error } = await apiClient.POST('/api/v1/merchants', {
      body: {
        name: values.name,
        slug: values.slug,
        defaultLanguage: values.defaultLanguage,
        currency: values.currency,
      },
    });

    if (error || !data) {
      triggerHaptic('error');
      const code = extractProblemCode(error);
      setServerError(
        code === 'slug_taken'
          ? t('onboarding.slugTaken')
          : extractProblemMessage(error, t('app.errorGeneric')),
      );
      return;
    }

    triggerHaptic('success');
    const updated = await refreshSessionAfterShopCreated(data.id);
    if (updated?.activeTenant) {
      void navigate('/merchant', { replace: true });
    }
  };

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-lg flex-col px-4 py-4">
      <header className="mb-6 flex items-center justify-between gap-2">
        <div className="flex min-w-0 items-center gap-2.5">
          {canGoBack ? (
            <Button asChild variant="ghost" size="icon" aria-label={t('onboarding.backToShops')}>
              <Link to="/auth">
                <BackIcon className="size-4" aria-hidden="true" />
              </Link>
            </Button>
          ) : (
            <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary">
              <Store className="size-5" aria-hidden="true" />
            </div>
          )}
          <h1 className="truncate text-base font-bold text-foreground">{t('app.name')}</h1>
        </div>
        <ToolbarControls />
      </header>

      <main id="main-content" className="flex-1">
        <Card>
          <CardHeader>
            <CardTitle>{t('onboarding.title')}</CardTitle>
            <CardDescription>{t('onboarding.subtitle')}</CardDescription>
          </CardHeader>

          <form onSubmit={(e) => void handleSubmit(onSubmit)(e)} className="space-y-4" noValidate>
            <div className="space-y-1.5">
              <Label htmlFor="shop-name">{t('onboarding.nameLabel')}</Label>
              <Input
                id="shop-name"
                placeholder={t('onboarding.namePlaceholder')}
                aria-invalid={Boolean(errors.name)}
                aria-describedby={errors.name ? 'shop-name-error' : undefined}
                {...register('name')}
              />
              {errors.name ? (
                <p id="shop-name-error" className="text-xs text-destructive">
                  {t('onboarding.validationName')}
                </p>
              ) : null}
            </div>

            <div className="space-y-1.5">
              <Label htmlFor="shop-slug">{t('onboarding.slugLabel')}</Label>
              <Input
                id="shop-slug"
                dir="ltr"
                placeholder={t('onboarding.slugPlaceholder')}
                aria-invalid={Boolean(errors.slug)}
                aria-describedby="shop-slug-helper"
                {...register('slug')}
              />
              <p
                id="shop-slug-helper"
                className={errors.slug ? 'text-xs text-destructive' : 'text-xs text-muted-foreground'}
              >
                {errors.slug ? t('onboarding.validationSlug') : t('onboarding.slugHelper')}
              </p>
              <p className="truncate text-xs text-primary/90" dir="ltr">
                {`${window.location.origin}/s/${slugPreview}`}
              </p>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="shop-lang">{t('onboarding.languageLabel')}</Label>
                <Select id="shop-lang" {...register('defaultLanguage')}>
                  <option value="fa">{t('app.persian')}</option>
                  <option value="en">{t('app.english')}</option>
                </Select>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="shop-currency">{t('onboarding.currencyLabel')}</Label>
                <Select id="shop-currency" {...register('currency')}>
                  <option value="IRT">{t('currencies.IRT')}</option>
                  <option value="IRR">{t('currencies.IRR')}</option>
                  <option value="USD">{t('currencies.USD')}</option>
                  <option value="EUR">{t('currencies.EUR')}</option>
                </Select>
              </div>
            </div>

            {serverError ? (
              <p
                role="alert"
                className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
              >
                {serverError}
              </p>
            ) : null}

            <Button type="submit" disabled={isSubmitting} className="w-full">
              {isSubmitting ? t('onboarding.submitting') : t('onboarding.submit')}
            </Button>
          </form>
        </Card>
      </main>
    </div>
  );
}
