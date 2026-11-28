import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { MessageCircle, Pencil, Plus, Search, Tag, Trash2, User, Users, X } from 'lucide-react';
import { useState, type FormEvent } from 'react';
import { useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { useAuth } from '@/features/auth/auth-context';
import { apiClient, extractProblemCode, extractProblemMessage } from '@/shared/api/client';
import { cn } from '@/shared/lib/cn';
import {
  buildTelegramChatUrl,
  formatDate,
  formatMoney,
  isValidOptionalPhone,
  isValidOptionalTelegramUsername,
  normalizePhoneToE164,
  normalizeTelegramUsername,
  toLatinDigits,
  toNumber,
} from '@/shared/lib/format';
import { normalizeLanguage } from '@/shared/lib/i18n';
import { openTelegramUrl, triggerHaptic } from '@/shared/lib/telegram';
import { Badge } from '@/shared/ui/badge';
import { Button } from '@/shared/ui/button';
import { Card } from '@/shared/ui/card';
import { ModalDialog } from '@/shared/ui/dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Pagination } from '@/shared/ui/pagination';
import { EmptyState, ErrorState, LoadingState } from '@/shared/ui/state-views';
import { OrderStatusBadge } from '@/shared/ui/status-badge';
import { Textarea } from '@/shared/ui/textarea';
import type { components } from '@/types/api';

type CustomerDto = components['schemas']['CustomerDto'];
type TagDto = components['schemas']['TagDto'];

export function MerchantCustomersPage() {
  const { t } = useTranslation();
  const { session } = useAuth();
  const activeShop = session?.activeTenant;
  const tenantKey = activeShop?.id ?? 'none';
  const currency = activeShop?.currency ?? 'IRT';

  const [searchTerm, setSearchTerm] = useState('');
  const [selectedTagId, setSelectedTagId] = useState<string | undefined>(undefined);
  const [page, setPage] = useState(1);

  const [formModal, setFormModal] = useState<{ open: boolean; editing: CustomerDto | null }>({
    open: false,
    editing: null,
  });
  const [detailCustomerId, setDetailCustomerId] = useState<string | null>(null);
  const [tagsModalOpen, setTagsModalOpen] = useState(false);

  const tagsQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'tags'],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/tags');
      if (error || !data) {
        throw new Error('Failed to load tags');
      }
      return data;
    },
  });

  const customersQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'customers', page, selectedTagId ?? 'all', searchTerm.trim()],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/customers', {
        params: {
          query: {
            page,
            pageSize: 20,
            q: searchTerm.trim() || undefined,
            tagId: selectedTagId,
          },
        },
      });
      if (error || !data) {
        throw new Error('Failed to load customers');
      }
      return data;
    },
  });

  const tags = tagsQuery.data ?? [];

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h1 className="text-xl font-bold tracking-tight text-foreground">
          {t('merchant.customers.title')}
        </h1>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" variant="outline" size="sm" onClick={() => setTagsModalOpen(true)}>
            <Tag className="size-4" aria-hidden="true" />
            <span>{t('merchant.customers.manageTags')}</span>
          </Button>
          <Button
            type="button"
            size="sm"
            onClick={() => setFormModal({ open: true, editing: null })}
          >
            <Plus className="size-4" aria-hidden="true" />
            <span>{t('merchant.customers.newCustomer')}</span>
          </Button>
        </div>
      </div>

      {/* Search */}
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
          placeholder={t('merchant.customers.searchPlaceholder')}
          aria-label={t('common.search')}
          className="ps-9"
        />
      </div>

      {/* Horizontal scrollable tag filter chips */}
      {tags.length > 0 ? (
        <div className="no-scrollbar flex flex-nowrap gap-2 overflow-x-auto pb-1 lg:flex-wrap">
          <button
            type="button"
            onClick={() => {
              triggerHaptic('selection');
              setSelectedTagId(undefined);
              setPage(1);
            }}
            className={cn(
              'min-h-11 shrink-0 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
              !selectedTagId
                ? 'border-primary bg-primary text-primary-foreground'
                : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
            )}
          >
            {t('common.all')}
          </button>
          {tags.map((tag) => {
            const active = selectedTagId === tag.id;
            return (
              <button
                key={tag.id}
                type="button"
                onClick={() => {
                  triggerHaptic('selection');
                  setSelectedTagId(tag.id);
                  setPage(1);
                }}
                className={cn(
                  'min-h-11 shrink-0 cursor-pointer rounded-full border px-3.5 text-xs font-medium transition-colors',
                  active
                    ? 'border-primary bg-primary text-primary-foreground'
                    : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
                )}
              >
                #{tag.name}
              </button>
            );
          })}
        </div>
      ) : null}

      {customersQuery.isLoading ? (
        <LoadingState rows={3} />
      ) : customersQuery.isError ? (
        <ErrorState onRetry={() => void customersQuery.refetch()} />
      ) : !customersQuery.data || customersQuery.data.items.length === 0 ? (
        <EmptyState
          icon={<Users className="size-6" aria-hidden="true" />}
          title={t('merchant.customers.empty')}
          description={t('merchant.customers.emptyHint')}
          action={
            <Button
              type="button"
              size="sm"
              onClick={() => setFormModal({ open: true, editing: null })}
            >
              <Plus className="size-4" aria-hidden="true" />
              <span>{t('merchant.customers.newCustomer')}</span>
            </Button>
          }
        />
      ) : (
        <>
          <div className="space-y-2.5">
            {customersQuery.data.items.map((customer) => {
              const chatUrl = buildTelegramChatUrl(
                customer.telegramUsername,
                customer.telegramUserId,
              );
              return (
                <Card key={customer.id} className="space-y-2.5">
                  <div className="flex flex-wrap items-start justify-between gap-3">
                    <div className="min-w-0 space-y-1">
                      <div className="flex items-center gap-2">
                        <User className="size-4 shrink-0 text-primary" aria-hidden="true" />
                        <h2 className="truncate text-sm font-semibold text-foreground">
                          {customer.displayName ?? t('orders.guestCustomer')}
                        </h2>
                      </div>
                      <div
                        className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground"
                        dir="ltr"
                      >
                        {customer.telegramUsername ? (
                          <span className="font-medium text-primary">
                            @{customer.telegramUsername}
                          </span>
                        ) : null}
                        {customer.phone ? (
                          <span className="tabular-nums">{customer.phone}</span>
                        ) : null}
                      </div>
                    </div>

                    <div className="flex flex-wrap items-center gap-1.5">
                      <Button
                        type="button"
                        variant="outline"
                        size="icon"
                        aria-label={t('merchant.customers.directChatCta')}
                        onClick={() => {
                          triggerHaptic('light');
                          openTelegramUrl(chatUrl);
                        }}
                      >
                        <MessageCircle className="size-4 text-primary" aria-hidden="true" />
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        onClick={() => setFormModal({ open: true, editing: customer })}
                      >
                        <Pencil className="size-3.5" aria-hidden="true" />
                        <span>{t('common.edit')}</span>
                      </Button>
                      <Button
                        type="button"
                        size="sm"
                        onClick={() => setDetailCustomerId(customer.id)}
                      >
                        {t('merchant.customers.detailsButton')}
                      </Button>
                    </div>
                  </div>

                  {customer.tags.length > 0 ? (
                    <div className="flex flex-wrap gap-1.5">
                      {customer.tags.map((tg) => (
                        <Badge key={tg.id} variant="secondary">
                          #{tg.name}
                        </Badge>
                      ))}
                    </div>
                  ) : null}
                </Card>
              );
            })}
          </div>

          <Pagination
            page={toNumber(customersQuery.data.page, 1)}
            pageSize={toNumber(customersQuery.data.pageSize, 20)}
            total={toNumber(customersQuery.data.total, 0)}
            onPageChange={setPage}
          />
        </>
      )}

      {formModal.open ? (
        <CustomerFormModal
          tenantKey={tenantKey}
          editing={formModal.editing}
          onClose={() => setFormModal({ open: false, editing: null })}
        />
      ) : null}

      {tagsModalOpen ? (
        <ManageTagsModal
          tenantKey={tenantKey}
          tags={tags}
          onClose={() => setTagsModalOpen(false)}
        />
      ) : null}

      {detailCustomerId ? (
        <CustomerDetailModal
          tenantKey={tenantKey}
          currency={currency}
          customerId={detailCustomerId}
          allTags={tags}
          onClose={() => setDetailCustomerId(null)}
        />
      ) : null}
    </div>
  );
}

const customerFormSchema = z.object({
  telegramUserId: z.string().trim(),
  telegramUsername: z.string().trim().refine(isValidOptionalTelegramUsername),
  displayName: z.string().trim().max(200),
  phone: z.string().trim().refine(isValidOptionalPhone),
  notes: z.string().max(2000),
});

type CustomerFormValues = z.infer<typeof customerFormSchema>;

function CustomerFormModal({
  tenantKey,
  editing,
  onClose,
}: {
  tenantKey: string;
  editing: CustomerDto | null;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);
  const [telegramIdError, setTelegramIdError] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CustomerFormValues>({
    resolver: zodResolver(customerFormSchema),
    defaultValues: {
      telegramUserId: editing ? String(editing.telegramUserId) : '',
      telegramUsername: editing?.telegramUsername ?? '',
      displayName: editing?.displayName ?? '',
      phone: editing?.phone ?? '',
      notes: editing?.notes ?? '',
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: CustomerFormValues) => {
      const normalizedPhone = normalizePhoneToE164(values.phone);
      const normalizedUsername = normalizeTelegramUsername(values.telegramUsername);

      if (editing) {
        const { error } = await apiClient.PATCH('/api/v1/merchant/customers/{id}', {
          params: { path: { id: editing.id } },
          body: {
            displayName: values.displayName || null,
            phone: normalizedPhone || null,
            notes: values.notes.trim() || null,
            telegramUsername: normalizedUsername,
          },
        });
        if (error) {
          throw error;
        }
      } else {
        const parsedId = Number.parseInt(toLatinDigits(values.telegramUserId), 10);
        const { error } = await apiClient.POST('/api/v1/merchant/customers', {
          body: {
            telegramUserId: parsedId,
            displayName: values.displayName || null,
            phone: normalizedPhone || null,
            notes: values.notes.trim() || null,
            telegramUsername: normalizedUsername || null,
          },
        });
        if (error) {
          throw error;
        }
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'customers'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'kpi-customers'] });
      if (editing) {
        void queryClient.invalidateQueries({
          queryKey: ['merchant', tenantKey, 'customer-detail', editing.id],
        });
      }
      onClose();
    },
    onError: (err) => {
      triggerHaptic('error');
      const code = extractProblemCode(err);
      setServerError(
        code === 'customer_exists'
          ? t('merchant.customers.customerExists')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const onValidSubmit = (values: CustomerFormValues) => {
    setServerError(null);
    setTelegramIdError(false);

    if (!editing) {
      const cleanId = toLatinDigits(values.telegramUserId);
      const parsedId = Number.parseInt(cleanId, 10);
      if (!/^\d+$/.test(cleanId) || Number.isNaN(parsedId) || parsedId <= 0) {
        setTelegramIdError(true);
        return;
      }
    }

    mutation.mutate(values);
  };

  return (
    <ModalDialog
      open
      title={editing ? t('merchant.customers.editCustomer') : t('merchant.customers.newCustomer')}
      onClose={onClose}
    >
      <form
        onSubmit={(e) => void handleSubmit(onValidSubmit)(e)}
        className="space-y-3.5"
        noValidate
      >
        {!editing ? (
          <div className="space-y-1.5">
            <Label htmlFor="cust-tg-id">{t('merchant.customers.telegramIdLabel')}</Label>
            <Input
              id="cust-tg-id"
              dir="ltr"
              inputMode="numeric"
              aria-invalid={telegramIdError}
              {...register('telegramUserId')}
            />
            <p
              className={
                telegramIdError ? 'text-xs text-destructive' : 'text-xs text-muted-foreground'
              }
            >
              {telegramIdError
                ? t('merchant.customers.validationTelegramId')
                : t('merchant.customers.telegramIdHelper')}
            </p>
          </div>
        ) : null}

        <div className="space-y-1.5">
          <Label htmlFor="cust-name">{t('merchant.customers.displayNameLabel')}</Label>
          <Input id="cust-name" {...register('displayName')} />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="cust-username">{t('merchant.customers.telegramUsernameLabel')}</Label>
          <Input
            id="cust-username"
            dir="ltr"
            placeholder={t('merchant.customers.telegramUsernamePlaceholder')}
            aria-invalid={Boolean(errors.telegramUsername)}
            {...register('telegramUsername')}
          />
          {errors.telegramUsername ? (
            <p className="text-xs text-destructive">{t('merchant.customers.validationUsername')}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="cust-phone">{t('merchant.customers.phoneLabel')}</Label>
          <Input
            id="cust-phone"
            dir="ltr"
            type="tel"
            inputMode="tel"
            placeholder={t('merchant.customers.phonePlaceholder')}
            aria-invalid={Boolean(errors.phone)}
            {...register('phone')}
          />
          {errors.phone ? (
            <p className="text-xs text-destructive">{t('merchant.customers.validationPhone')}</p>
          ) : null}
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="cust-notes">{t('merchant.customers.notesLabel')}</Label>
          <Textarea
            id="cust-notes"
            placeholder={t('merchant.customers.notesPlaceholder')}
            {...register('notes')}
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

function ManageTagsModal({
  tenantKey,
  tags,
  onClose,
}: {
  tenantKey: string;
  tags: TagDto[];
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [name, setName] = useState('');
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const createMutation = useMutation({
    mutationFn: async () => {
      const { error } = await apiClient.POST('/api/v1/merchant/tags', {
        body: { name: name.trim() },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      setName('');
      setErrorMsg(null);
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'tags'] });
    },
    onError: (err) => {
      triggerHaptic('error');
      const code = extractProblemCode(err);
      setErrorMsg(
        code === 'tag_exists'
          ? t('merchant.customers.tagExists')
          : extractProblemMessage(err, t('app.errorGeneric')),
      );
    },
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await apiClient.DELETE('/api/v1/merchant/tags/{id}', {
        params: { path: { id } },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('success');
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'tags'] });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'customers'] });
    },
  });

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!name.trim()) {
      return;
    }
    createMutation.mutate();
  };

  return (
    <ModalDialog open title={t('merchant.customers.tagsTitle')} onClose={onClose}>
      <div className="space-y-4">
        <form onSubmit={handleSubmit} className="flex gap-2">
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={t('merchant.customers.newTagPlaceholder')}
            maxLength={60}
          />
          <Button type="submit" disabled={createMutation.isPending || !name.trim()}>
            {t('merchant.customers.addTag')}
          </Button>
        </form>

        {errorMsg ? (
          <p role="alert" className="text-xs text-destructive">
            {errorMsg}
          </p>
        ) : null}

        {tags.length === 0 ? (
          <EmptyState embedded description={t('merchant.customers.emptyTags')} />
        ) : (
          <div className="divide-y divide-border rounded-lg border border-border bg-background">
            {tags.map((tag) => (
              <div key={tag.id} className="flex items-center justify-between gap-2 px-3 py-1.5">
                <span className="text-sm font-medium text-foreground">#{tag.name}</span>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={t('common.delete')}
                  disabled={deleteMutation.isPending}
                  onClick={() => deleteMutation.mutate(tag.id)}
                  className="text-destructive hover:bg-destructive/10"
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                </Button>
              </div>
            ))}
          </div>
        )}
      </div>
    </ModalDialog>
  );
}

function CustomerDetailModal({
  tenantKey,
  currency,
  customerId,
  allTags,
  onClose,
}: {
  tenantKey: string;
  currency: string;
  customerId: string;
  allTags: TagDto[];
  onClose: () => void;
}) {
  const { t, i18n } = useTranslation();
  const queryClient = useQueryClient();
  const lang = normalizeLanguage(i18n.language);

  const customerQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'customer-detail', customerId],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/customers/{id}', {
        params: { path: { id: customerId } },
      });
      if (error || !data) {
        throw new Error('Failed to load customer');
      }
      return data;
    },
  });

  const ordersQuery = useQuery({
    queryKey: ['merchant', tenantKey, 'customer-orders', customerId],
    queryFn: async () => {
      const { data, error } = await apiClient.GET('/api/v1/merchant/customers/{id}/orders', {
        params: { path: { id: customerId }, query: { page: 1, pageSize: 20 } },
      });
      if (error || !data) {
        throw new Error('Failed to load customer orders');
      }
      return data.items;
    },
  });

  const attachMutation = useMutation({
    mutationFn: async (tagId: string) => {
      const { error } = await apiClient.POST('/api/v1/merchant/customers/{id}/tags', {
        params: { path: { id: customerId } },
        body: { tagId },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('selection');
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'customer-detail', customerId],
      });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'customers'] });
    },
  });

  const detachMutation = useMutation({
    mutationFn: async (tagId: string) => {
      const { error } = await apiClient.DELETE('/api/v1/merchant/customers/{id}/tags/{tagId}', {
        params: { path: { id: customerId, tagId } },
      });
      if (error) {
        throw error;
      }
    },
    onSuccess: () => {
      triggerHaptic('selection');
      void queryClient.invalidateQueries({
        queryKey: ['merchant', tenantKey, 'customer-detail', customerId],
      });
      void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'customers'] });
    },
  });

  const customer = customerQuery.data;
  const attachedIds = new Set(customer?.tags.map((tg) => tg.id) ?? []);
  const chatUrl = customer
    ? buildTelegramChatUrl(customer.telegramUsername, customer.telegramUserId)
    : null;

  return (
    <ModalDialog
      open
      title={customer?.displayName ?? t('orders.guestCustomer')}
      onClose={onClose}
    >
      {customerQuery.isLoading ? (
        <LoadingState rows={2} />
      ) : customerQuery.isError || !customer ? (
        <ErrorState onRetry={() => void customerQuery.refetch()} />
      ) : (
        <div className="space-y-4">
          <div className="space-y-2 rounded-lg border border-border bg-background p-3 text-xs">
            {customer.telegramUsername ? (
              <p className="font-semibold text-primary" dir="ltr">
                @{customer.telegramUsername}
              </p>
            ) : null}
            {customer.phone ? (
              <p className="tabular-nums text-foreground" dir="ltr">
                {customer.phone}
              </p>
            ) : null}
            <p className="tabular-nums text-muted-foreground">
              {t('merchant.customers.telegramIdShort')}:{' '}
              <span dir="ltr">{customer.telegramUserId}</span>
            </p>
            {customer.notes ? (
              <p className="border-t border-border pt-2 text-sm text-foreground">{customer.notes}</p>
            ) : null}
          </div>

          {/* Direct Telegram Chat CTA */}
          {chatUrl ? (
            <Button
              type="button"
              className="w-full"
              onClick={() => {
                triggerHaptic('light');
                openTelegramUrl(chatUrl);
              }}
            >
              <MessageCircle className="size-4" aria-hidden="true" />
              <span>{t('merchant.customers.directChatCta')}</span>
            </Button>
          ) : null}

          {/* Toggle tags */}
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-foreground">
              {t('merchant.customers.customerTagsLabel')}
            </h3>
            {allTags.length === 0 ? (
              <p className="text-xs text-muted-foreground">{t('merchant.customers.emptyTags')}</p>
            ) : (
              <div className="flex flex-wrap gap-2">
                {allTags.map((tag) => {
                  const attached = attachedIds.has(tag.id);
                  return (
                    <button
                      key={tag.id}
                      type="button"
                      disabled={attachMutation.isPending || detachMutation.isPending}
                      onClick={() => {
                        if (attached) {
                          detachMutation.mutate(tag.id);
                        } else {
                          attachMutation.mutate(tag.id);
                        }
                      }}
                      className={cn(
                        'inline-flex min-h-11 cursor-pointer items-center gap-1.5 rounded-full border px-3.5 text-xs font-medium transition-colors',
                        attached
                          ? 'border-primary bg-primary text-primary-foreground'
                          : 'border-border bg-surface text-foreground hover:bg-surface-elevated',
                      )}
                    >
                      <span>#{tag.name}</span>
                      {attached ? <X className="size-3.5" aria-hidden="true" /> : null}
                    </button>
                  );
                })}
              </div>
            )}
          </div>

          {/* Order History */}
          <div className="space-y-2">
            <h3 className="text-sm font-semibold text-foreground">
              {t('merchant.customers.orderHistoryTitle')}
            </h3>
            {ordersQuery.isLoading ? (
              <LoadingState rows={2} />
            ) : !ordersQuery.data || ordersQuery.data.length === 0 ? (
              <EmptyState embedded description={t('merchant.customers.noOrders')} />
            ) : (
              <div className="divide-y divide-border rounded-lg border border-border bg-background">
                {ordersQuery.data.map((ord) => (
                  <div key={ord.id} className="flex items-center justify-between gap-2 p-3 text-xs">
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <span className="tabular-nums font-semibold text-foreground" dir="ltr">
                          #{ord.id.slice(0, 8).toUpperCase()}
                        </span>
                        <OrderStatusBadge status={ord.status} />
                      </div>
                      <p className="text-muted-foreground">{formatDate(ord.placedAt, lang)}</p>
                    </div>
                    <span className="tabular-nums text-sm font-bold text-primary">
                      {formatMoney(ord.totalAmount, currency, lang)}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </ModalDialog>
  );
}
