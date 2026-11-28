import {
  ArrowLeft,
  ArrowRight,
  KeyRound,
  LogIn,
  PlusCircle,
  RefreshCw,
  Store,
} from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import { triggerHaptic } from '@/shared/lib/telegram';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { LoadingState } from '@/shared/ui/state-views';
import { ToolbarControls } from '@/shared/ui/toolbar-controls';
import { useAuth } from './auth-context';

export function AuthPage() {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const { status, session, telegram, errorMessage, signInWithInitData, switchTenant } = useAuth();
  const [shopSlug, setShopSlug] = useState('');
  const [devInitData, setDevInitData] = useState('');
  const [busyShopId, setBusyShopId] = useState<string | null>(null);

  const dir = getDirection(normalizeLanguage(i18n.language));
  const DirectionalArrow = dir === 'rtl' ? ArrowLeft : ArrowRight;

  const handleOpenStorefront = (event: FormEvent) => {
    event.preventDefault();
    const cleaned = shopSlug.trim().toLowerCase();
    if (!cleaned) {
      return;
    }
    triggerHaptic('light');
    void navigate(`/s/${encodeURIComponent(cleaned)}`);
  };

  const handleTelegramReSignIn = async () => {
    if (!telegram.initDataRaw) {
      return;
    }
    triggerHaptic('light');
    const next = await signInWithInitData(telegram.initDataRaw);
    if (!next) {
      return;
    }
    if (next.activeTenant) {
      void navigate('/merchant', { replace: true });
    } else if (next.tenants.length === 0) {
      void navigate('/onboarding', { replace: true });
    }
  };

  const handleDevLogin = async (event: FormEvent) => {
    event.preventDefault();
    const next = await signInWithInitData(devInitData);
    if (!next) {
      return;
    }
    if (next.activeTenant) {
      void navigate('/merchant', { replace: true });
    } else if (next.tenants.length === 0) {
      void navigate('/onboarding', { replace: true });
    }
  };

  const handleSelectShop = async (tenantId: string) => {
    setBusyShopId(tenantId);
    triggerHaptic('selection');
    try {
      const updated = await switchTenant(tenantId);
      if (updated?.activeTenant) {
        void navigate('/merchant', { replace: true });
      }
    } finally {
      setBusyShopId(null);
    }
  };

  return (
    <div className="mx-auto flex min-h-dvh w-full max-w-lg flex-col px-4 py-4">
      <header className="mb-6 flex items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-2.5">
          <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/15 text-primary">
            <Store className="size-5" aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h1 className="truncate text-base font-bold text-foreground">{t('app.name')}</h1>
            <p className="truncate text-xs text-muted-foreground">{t('app.tagline')}</p>
          </div>
        </div>
        <div className="shrink-0">
          <ToolbarControls />
        </div>
      </header>

      <main id="main-content" className="flex flex-1 flex-col gap-4">
        {status === 'bootstrapping' ? (
          <div className="space-y-4">
            <p className="text-sm text-muted-foreground">{t('auth.authenticating')}</p>
            <LoadingState rows={2} />
          </div>
        ) : null}

        {status === 'reopen_required' ? (
          <Card className="space-y-3 border-warning/40">
            <CardHeader>
              <CardTitle>{t('auth.reopenTitle')}</CardTitle>
              <CardDescription>{t('auth.reopenDesc')}</CardDescription>
            </CardHeader>
            {telegram.initDataRaw ? (
              <Button type="button" onClick={() => void handleTelegramReSignIn()} className="w-full">
                <RefreshCw className="size-4" aria-hidden="true" />
                {t('auth.retryAuth')}
              </Button>
            ) : null}
          </Card>
        ) : null}

        {status === 'authenticated' && session ? (
          <Card className="space-y-4">
            <CardHeader className="mb-0">
              <div className="flex items-center justify-between gap-2">
                <CardTitle>{t('auth.selectShopTitle')}</CardTitle>
                {session.activeTenant ? (
                  <Button asChild variant="ghost" size="sm">
                    <Link to="/merchant">{t('auth.backToDashboard')}</Link>
                  </Button>
                ) : null}
              </div>
              <CardDescription>{t('auth.selectShopDesc')}</CardDescription>
            </CardHeader>

            {session.tenants.length > 0 ? (
              <div className="space-y-2">
                {session.tenants.map((shop) => {
                  const isActive = session.activeTenant?.id === shop.id;
                  const roleLabel = t(`roles.${shop.role}`, { defaultValue: shop.role });
                  return (
                    <button
                      key={shop.id}
                      type="button"
                      disabled={busyShopId === shop.id}
                      onClick={() => void handleSelectShop(shop.id)}
                      className="flex min-h-14 w-full cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background px-3.5 py-2.5 text-start transition-colors hover:bg-surface-elevated disabled:opacity-50"
                    >
                      <div className="flex min-w-0 items-center gap-3">
                        <div className="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-primary/15 text-primary">
                          {shop.logoUrl ? (
                            <img
                              src={shop.logoUrl}
                              alt={shop.name}
                              className="size-full object-cover"
                            />
                          ) : (
                            <Store className="size-4" aria-hidden="true" />
                          )}
                        </div>
                        <div className="min-w-0">
                          <div className="flex items-center gap-1.5">
                            <p className="truncate text-sm font-semibold text-foreground">
                              {shop.name}
                            </p>
                            {isActive ? (
                              <Badge variant="success">{t('auth.activeBadge')}</Badge>
                            ) : null}
                          </div>
                          <p className="truncate text-xs text-muted-foreground" dir="ltr">
                            {shop.slug}
                          </p>
                        </div>
                      </div>
                      <div className="flex shrink-0 items-center gap-2">
                        <Badge variant="secondary">{roleLabel}</Badge>
                        <DirectionalArrow
                          className="size-4 text-muted-foreground"
                          aria-hidden="true"
                        />
                      </div>
                    </button>
                  );
                })}
              </div>
            ) : null}

            <Button asChild className="w-full">
              <Link to="/onboarding">
                <PlusCircle className="size-4" aria-hidden="true" />
                {t('auth.createNewShop')}
              </Link>
            </Button>
          </Card>
        ) : null}

        {status === 'unauthenticated' ? (
          <>
            <Card className="space-y-3">
              <CardHeader className="mb-0">
                <CardTitle>{t('auth.outsideTelegramTitle')}</CardTitle>
                <CardDescription>{t('auth.outsideTelegramDesc')}</CardDescription>
              </CardHeader>

              {errorMessage ? (
                <p
                  role="alert"
                  className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
                >
                  {errorMessage}
                </p>
              ) : null}

              {telegram.initDataRaw ? (
                <Button
                  type="button"
                  onClick={() => void handleTelegramReSignIn()}
                  className="w-full"
                >
                  <LogIn className="size-4" aria-hidden="true" />
                  {t('auth.signInWithTelegram')}
                </Button>
              ) : null}
            </Card>

            <Card>
              <CardHeader>
                <CardTitle>{t('auth.openStorefrontTitle')}</CardTitle>
              </CardHeader>
              <form onSubmit={handleOpenStorefront} className="space-y-3">
                <div className="space-y-1.5">
                  <Label htmlFor="shop-slug-input">{t('auth.shopSlugLabel')}</Label>
                  <Input
                    id="shop-slug-input"
                    dir="ltr"
                    value={shopSlug}
                    onChange={(e) => setShopSlug(e.target.value)}
                    placeholder={t('auth.shopSlugPlaceholder')}
                    autoComplete="off"
                  />
                </div>
                <Button type="submit" className="w-full">
                  <Store className="size-4" aria-hidden="true" />
                  {t('auth.openStorefrontCta')}
                </Button>
              </form>
            </Card>

            {import.meta.env.DEV ? (
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2 text-sm">
                    <KeyRound className="size-4 text-primary" aria-hidden="true" />
                    {t('auth.devInitDataLabel')}
                  </CardTitle>
                </CardHeader>
                <form onSubmit={(e) => void handleDevLogin(e)} className="space-y-3">
                  <div className="space-y-1.5">
                    <Label htmlFor="dev-init-data">{t('auth.devInitDataLabel')}</Label>
                    <Input
                      id="dev-init-data"
                      dir="ltr"
                      value={devInitData}
                      onChange={(e) => setDevInitData(e.target.value)}
                      placeholder={t('auth.devInitDataPlaceholder')}
                    />
                  </div>
                  <Button type="submit" variant="secondary" className="w-full">
                    {t('auth.devSignInCta')}
                  </Button>
                </form>
              </Card>
            ) : null}
          </>
        ) : null}
      </main>
    </div>
  );
}
