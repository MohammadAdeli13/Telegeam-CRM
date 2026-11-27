import { useQuery } from '@tanstack/react-query';
import { Check, Copy, ExternalLink, Package, ShoppingBag, Users } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient } from '@/shared/api/client';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { EmptyState, LoadingState } from '@/shared/ui/state-views';

export function MerchantDashboardPage() {
  const { t } = useTranslation();
  const { session } = useAuth();
  const [copied, setCopied] = useState(false);

  const healthQuery = useQuery({
    queryKey: ['api-health'],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/health');
      if (error || !data) {
        throw new Error('Health check failed');
      }
      return data;
    },
  });

  const activeShop = session?.activeTenant;
  const storefrontUrl = activeShop ? `${window.location.origin}/s/${activeShop.slug}` : '';

  const handleCopyLink = async () => {
    if (!storefrontUrl) {
      return;
    }
    try {
      await navigator.clipboard.writeText(storefrontUrl);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard may be restricted in some WebViews.
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('merchant.dashboard.title')}
        </h1>
        {activeShop ? (
          <div className="flex flex-wrap items-center gap-2">
            <Button type="button" variant="outline" size="sm" onClick={() => void handleCopyLink()}>
              {copied ? (
                <Check className="size-4 text-success" aria-hidden="true" />
              ) : (
                <Copy className="size-4" aria-hidden="true" />
              )}
              <span>{copied ? t('merchant.linkCopied') : t('merchant.copyShopLink')}</span>
            </Button>
            <Button asChild size="sm">
              <Link to={`/s/${activeShop.slug}`}>
                <ExternalLink className="size-4" aria-hidden="true" />
                <span>{t('merchant.previewStorefront')}</span>
              </Link>
            </Button>
          </div>
        ) : null}
      </div>

      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <Card>
          <p className="text-xs font-medium text-muted-foreground">
            {t('merchant.dashboard.activeShop')}
          </p>
          <p className="mt-1 text-lg font-semibold text-foreground">
            {activeShop?.name ?? '—'}
          </p>
          {activeShop ? (
            <p className="mt-0.5 text-xs text-muted-foreground" dir="ltr">
              /s/{activeShop.slug}
            </p>
          ) : null}
        </Card>

        <Card>
          <p className="text-xs font-medium text-muted-foreground">
            {t('merchant.dashboard.apiStatus')}
          </p>
          <div className="mt-2 flex items-center gap-2">
            {healthQuery.isLoading ? (
              <LoadingState rows={1} />
            ) : healthQuery.isSuccess ? (
              <Badge variant="success">{t('merchant.dashboard.apiHealthy')}</Badge>
            ) : (
              <Badge variant="destructive">{t('merchant.dashboard.apiOffline')}</Badge>
            )}
          </div>
        </Card>
      </div>

      <Card className="space-y-3">
        <CardHeader className="mb-0">
          <CardTitle>{t('merchant.dashboard.quickActions')}</CardTitle>
          <CardDescription>{t('merchant.dashboard.shellReadyNotice')}</CardDescription>
        </CardHeader>
        <div className="grid grid-cols-1 gap-2 sm:grid-cols-3">
          <Button asChild variant="outline" className="justify-start">
            <Link to="/merchant/products">
              <Package className="size-4 text-primary" aria-hidden="true" />
              <span>{t('merchant.dashboard.manageProducts')}</span>
            </Link>
          </Button>
          <Button asChild variant="outline" className="justify-start">
            <Link to="/merchant/orders">
              <ShoppingBag className="size-4 text-primary" aria-hidden="true" />
              <span>{t('merchant.dashboard.manageOrders')}</span>
            </Link>
          </Button>
          <Button asChild variant="outline" className="justify-start">
            <Link to="/merchant/customers">
              <Users className="size-4 text-primary" aria-hidden="true" />
              <span>{t('merchant.dashboard.manageCustomers')}</span>
            </Link>
          </Button>
        </div>
      </Card>
    </div>
  );
}

export function MerchantSectionPlaceholder({
  titleKey,
  descKey,
}: {
  titleKey: string;
  descKey: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="space-y-4">
      <h1 className="text-xl font-bold tracking-tight text-foreground">{t(titleKey)}</h1>
      <EmptyState title={t(titleKey)} description={t(descKey)} />
    </div>
  );
}
