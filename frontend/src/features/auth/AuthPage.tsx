import { ArrowLeft, ArrowRight, KeyRound, PlusCircle, RefreshCw, Store } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
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
    void navigate(`/s/${encodeURIComponent(cleaned)}`);
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
      <header className="mb-6 flex items-center justify-between gap-2">
        <div className="flex items-center gap-2.5">
          <div className="flex size-10 items-center justify-center rounded-xl bg-primary/15 text-primary">
            <Store className="size-5" aria-hidden="true" />
          </div>
          <div>
            <h1 className="text-base font-bold text-foreground">{t('app.name')}</h1>
            <p className="text-xs text-muted-foreground">{t('app.tagline')}</p>
          </div>
        </div>
        <ToolbarControls />
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
              <Button
                type="button"
                onClick={() => void signInWithInitData(telegram.initDataRaw ?? '')}
              >
                <RefreshCw className="size-4" aria-hidden="true" />
                {t('auth.retryAuth')}
              </Button>
            ) : null}
          </Card>
        ) : null}

        {status === 'authenticated' && session ? (
          <Card className="space-y-4">
            <CardHeader>
              <CardTitle>{t('auth.selectShopTitle')}</CardTitle>
              <CardDescription>{t('auth.selectShopDesc')}</CardDescription>
            </CardHeader>

            {session.tenants.length > 0 ? (
              <div className="space-y-2">
                {session.tenants.map((shop) => (
                  <button
                    key={shop.id}
                    type="button"
                    disabled={busyShopId === shop.id}
                    onClick={() => void handleSelectShop(shop.id)}
                    className="flex min-h-12 w-full cursor-pointer items-center justify-between gap-3 rounded-lg border border-border bg-background px-3.5 py-2.5 text-start transition-colors hover:bg-surface-elevated disabled:opacity-50"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-semibold text-foreground">{shop.name}</p>
                      <p className="truncate text-xs text-muted-foreground" dir="ltr">
                        /{shop.slug}
                      </p>
                    </div>
                    <div className="flex items-center gap-2">
                      <Badge variant="secondary">{shop.role}</Badge>
                      <DirectionalArrow className="size-4 text-muted-foreground" aria-hidden="true" />
                    </div>
                  </button>
                ))}
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
              <CardHeader>
                <CardTitle>{t('auth.outsideTelegramTitle')}</CardTitle>
                <CardDescription>{t('auth.outsideTelegramDesc')}</CardDescription>
              </CardHeader>
              {errorMessage ? (
                <p role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive">
                  {errorMessage}
                </p>
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
                <Button type="submit" variant="secondary" className="w-full">
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
                  <Button type="submit" className="w-full">
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
