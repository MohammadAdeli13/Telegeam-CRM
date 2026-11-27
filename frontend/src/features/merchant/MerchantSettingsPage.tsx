import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Check, LogOut, Plus, Save, Store, Trash2 } from 'lucide-react';
import { useEffect, useState, type FormEvent } from 'react';
import { useTranslation } from 'react-i18next';
import { Link, useNavigate } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemMessage } from '@/shared/api/client';
import { Button } from '@/shared/ui/button';
import { Card, CardDescription, CardHeader, CardTitle } from '@/shared/ui/card';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { isValidJsonText } from './merchant-state';

interface SettingRow {
  id: string;
  key: string;
  value: string;
}

const PRESETS = [
  { key: 'support_telegram', value: '"@shop_support"' },
  { key: 'store_bio', value: '"Welcome to our shop"' },
  { key: 'shipping_note', value: '"Delivery within 24-48h"' },
] as const;

export function MerchantSettingsPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { session, signOut } = useAuth();
  const tenantKey = session?.activeTenant?.id ?? 'none';

  const [rows, setRows] = useState<SettingRow[]>([]);
  const [initialized, setInitialized] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);
  const [savedNotice, setSavedNotice] = useState(false);

  const settingsQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'settings'],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/settings');
      if (error || !data) {
        throw new Error('Failed to load settings');
      }
      return data;
    },
  });

  useEffect(() => {
    if (settingsQuery.data && !initialized) {
      setRows(
        settingsQuery.data.map((item, idx) => ({
          id: `loaded-${idx}-${item.key}`,
          key: item.key,
          value: item.value,
        })),
      );
      setInitialized(true);
    }
  }, [initialized, settingsQuery.data]);

  const saveMutation = useMutation({
    mutationFn: async (cleanSettings: { key: string; value: string }[]) => {
      const { data, error } = await apiClient.PUT('/api/v1/merchant/settings', {
        body: { settings: cleanSettings },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to save settings');
      }
      return data;
    },
    onSuccess: (updated) => {
      setRows(
        updated.map((item, idx) => ({
          id: `saved-${idx}-${item.key}`,
          key: item.key,
          value: item.value,
        })),
      );
      setErrorMsg(null);
      setSavedNotice(true);
      window.setTimeout(() => setSavedNotice(false), 2500);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'settings'] });
    },
    onError: (err) => {
      setErrorMsg(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  const addRow = (key = '', value = '""') => {
    setRows((prev) => [
      ...prev,
      { id: `new-${Date.now()}-${Math.random().toString(36).slice(2)}`, key, value },
    ]);
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    setErrorMsg(null);
    setSavedNotice(false);

    const seen = new Set<string>();
    const clean: { key: string; value: string }[] = [];

    for (const row of rows) {
      const k = row.key.trim();
      const v = row.value.trim();
      if (!k) {
        setErrorMsg(t('merchant.settings.emptyKey'));
        return;
      }
      if (seen.has(k)) {
        setErrorMsg(t('merchant.settings.duplicateKey'));
        return;
      }
      if (!isValidJsonText(v)) {
        setErrorMsg(t('merchant.settings.invalidJson'));
        return;
      }
      seen.add(k);
      clean.push({ key: k, value: v });
    }

    saveMutation.mutate(clean);
  };

  return (
    <div className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <h1 className="text-xl font-bold tracking-tight text-foreground">
            {t('merchant.settings.title')}
          </h1>
          <p className="mt-0.5 text-xs text-muted-foreground">{t('merchant.settings.subtitle')}</p>
        </div>

        <Button type="button" variant="outline" size="sm" onClick={() => addRow()}>
          <Plus className="size-4" aria-hidden="true" />
          <span>{t('merchant.settings.addRow')}</span>
        </Button>
      </div>

      {/* Quick presets */}
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs text-muted-foreground">{t('merchant.settings.presetsTitle')}:</span>
        {PRESETS.map((preset) => {
          const alreadyExists = rows.some((r) => r.key.trim() === preset.key);
          return (
            <Button
              key={preset.key}
              type="button"
              variant="secondary"
              size="sm"
              disabled={alreadyExists}
              onClick={() => addRow(preset.key, preset.value)}
              className="min-h-9 px-2.5 text-xs"
              dir="ltr"
            >
              + {preset.key}
            </Button>
          );
        })}
      </div>

      {settingsQuery.isLoading ? (
        <LoadingState rows={2} />
      ) : settingsQuery.isError ? (
        <ErrorState onRetry={() => void settingsQuery.refetch()} />
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          {rows.length === 0 ? (
            <EmptyState description={t('merchant.settings.empty')} />
          ) : (
            <div className="space-y-2.5">
              {rows.map((row) => {
                const jsonValid = isValidJsonText(row.value);
                return (
                  <Card key={row.id} className="space-y-3">
                    <div className="grid grid-cols-1 gap-3 sm:grid-cols-[1fr_1.4fr_auto] sm:items-end">
                      <div className="space-y-1.5">
                        <Label htmlFor={`key-${row.id}`}>{t('merchant.settings.keyLabel')}</Label>
                        <Input
                          id={`key-${row.id}`}
                          dir="ltr"
                          value={row.key}
                          placeholder={t('merchant.settings.keyPlaceholder')}
                          onChange={(e) =>
                            setRows((prev) =>
                              prev.map((item) =>
                                item.id === row.id ? { ...item, key: e.target.value } : item,
                              ),
                            )
                          }
                        />
                      </div>

                      <div className="space-y-1.5">
                        <Label htmlFor={`val-${row.id}`}>{t('merchant.settings.valueLabel')}</Label>
                        <Input
                          id={`val-${row.id}`}
                          dir="ltr"
                          value={row.value}
                          placeholder={t('merchant.settings.valuePlaceholder')}
                          aria-invalid={!jsonValid}
                          onChange={(e) =>
                            setRows((prev) =>
                              prev.map((item) =>
                                item.id === row.id ? { ...item, value: e.target.value } : item,
                              ),
                            )
                          }
                        />
                      </div>

                      <Button
                        type="button"
                        variant="ghost"
                        size="icon"
                        aria-label={t('common.delete')}
                        onClick={() => setRows((prev) => prev.filter((item) => item.id !== row.id))}
                        className="text-destructive hover:bg-destructive/10"
                      >
                        <Trash2 className="size-4" aria-hidden="true" />
                      </Button>
                    </div>
                  </Card>
                );
              })}
            </div>
          )}

          {errorMsg ? (
            <p
              role="alert"
              className="rounded-lg border border-destructive/40 bg-destructive/10 px-3 py-2 text-xs text-destructive"
            >
              {errorMsg}
            </p>
          ) : null}

          {savedNotice ? (
            <p
              role="status"
              className="flex items-center gap-1.5 rounded-lg border border-success/40 bg-success/10 px-3 py-2 text-xs text-success"
            >
              <Check className="size-4" aria-hidden="true" />
              <span>{t('common.saved')}</span>
            </p>
          ) : null}

          <div className="flex justify-end">
            <Button type="submit" disabled={saveMutation.isPending}>
              <Save className="size-4" aria-hidden="true" />
              <span>{saveMutation.isPending ? t('common.saving') : t('common.save')}</span>
            </Button>
          </div>
        </form>
      )}

      {/* Session & Shop Switcher */}
      <Card className="space-y-3">
        <CardHeader className="mb-0">
          <CardTitle>{t('merchant.settings.sessionSectionTitle')}</CardTitle>
          <CardDescription>
            {session?.activeTenant?.name}
          </CardDescription>
        </CardHeader>
        <div className="flex flex-wrap gap-2">
          <Button asChild variant="outline" size="sm">
            <Link to="/auth">
              <Store className="size-4" aria-hidden="true" />
              <span>{t('merchant.switchShop')}</span>
            </Link>
          </Button>
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
        </div>
      </Card>
    </div>
  );
}
