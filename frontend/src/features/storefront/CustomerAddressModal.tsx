import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { Controller, useForm } from 'react-hook-form';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';
import { apiClient, extractProblemMessage } from '@/shared/api/client';
import {
  isValidOptionalPhone,
  normalizePhoneToE164,
  toLatinDigits,
} from '@/shared/lib/format';
import { triggerHaptic } from '@/shared/lib/telegram';
import { Button } from '@/shared/ui/button';
import { ModalDialog } from '@/shared/ui/dialog';
import { Input } from '@/shared/ui/input';
import { Label } from '@/shared/ui/label';
import { Switch } from '@/shared/ui/switch';
import { Textarea } from '@/shared/ui/textarea';
import type { components } from '@/types/api';

type CustomerAddressDto = components['schemas']['CustomerAddressDto'];

const addressFormSchema = z.object({
  title: z.string().trim().min(1).max(60),
  recipientName: z.string().trim().min(2).max(150),
  phone: z
    .string()
    .trim()
    .min(1)
    .refine((v) => v.trim().length > 0 && isValidOptionalPhone(v)),
  province: z.string().trim().min(2).max(80),
  city: z.string().trim().min(2).max(80),
  addressLine: z.string().trim().min(4).max(500),
  plaque: z.string().trim().max(20),
  unit: z.string().trim().max(20),
  postalCode: z.string().trim().max(20),
  isDefault: z.boolean(),
});

type AddressFormValues = z.infer<typeof addressFormSchema>;

export function CustomerAddressModal({
  shopSlug,
  editing,
  onSaved,
  onClose,
}: {
  shopSlug: string;
  editing: CustomerAddressDto | null;
  onSaved: (address: CustomerAddressDto) => void;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [serverError, setServerError] = useState<string | null>(null);

  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<AddressFormValues>({
    resolver: zodResolver(addressFormSchema),
    defaultValues: {
      title: editing?.title ?? '',
      recipientName: editing?.recipientName ?? '',
      phone: editing?.phone ?? '',
      province: editing?.province ?? '',
      city: editing?.city ?? '',
      addressLine: editing?.addressLine ?? '',
      plaque: editing?.plaque ?? '',
      unit: editing?.unit ?? '',
      postalCode: editing?.postalCode ?? '',
      isDefault: editing ? editing.isDefault : true,
    },
  });

  const mutation = useMutation({
    mutationFn: async (values: AddressFormValues) => {
      const normalizedPhone = normalizePhoneToE164(values.phone);
      const cleanPostal = toLatinDigits(values.postalCode).trim();

      if (editing) {
        const { data, error } = await apiClient.PATCH(
          '/api/v1/storefront/{shopSlug}/addresses/{id}',
          {
            params: { path: { shopSlug, id: editing.id } },
            body: {
              title: values.title,
              recipientName: values.recipientName,
              phone: normalizedPhone,
              province: values.province,
              city: values.city,
              addressLine: values.addressLine,
              plaque: values.plaque || null,
              unit: values.unit || null,
              postalCode: cleanPostal || null,
              isDefault: values.isDefault,
            },
          },
        );
        if (error || !data) {
          throw error ?? new Error('Failed to update address');
        }
        return data;
      }

      const { data, error } = await apiClient.POST('/api/v1/storefront/{shopSlug}/addresses', {
        params: { path: { shopSlug } },
        body: {
          title: values.title,
          recipientName: values.recipientName,
          phone: normalizedPhone,
          province: values.province,
          city: values.city,
          addressLine: values.addressLine,
          plaque: values.plaque || null,
          unit: values.unit || null,
          postalCode: cleanPostal || null,
          isDefault: values.isDefault,
        },
      });
      if (error || !data) {
        throw error ?? new Error('Failed to create address');
      }
      return data;
    },
    onSuccess: (saved) => {
      triggerHaptic('success');
      void queryClient.invalidateQueries({ queryKey: ['storefront-addresses', shopSlug] });
      onSaved(saved);
    },
    onError: (err) => {
      triggerHaptic('error');
      setServerError(extractProblemMessage(err, t('app.errorGeneric')));
    },
  });

  return (
    <ModalDialog
      open
      title={
        editing ? t('storefront.addresses.editTitle') : t('storefront.addresses.addNew')
      }
      onClose={onClose}
    >
      <form
        onSubmit={(e) => void handleSubmit((vals) => mutation.mutate(vals))(e)}
        className="space-y-3"
        noValidate
      >
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div className="space-y-1">
            <Label htmlFor="addr-title">{t('storefront.addresses.titleLabel')}</Label>
            <Input
              id="addr-title"
              placeholder={t('storefront.addresses.titlePlaceholder')}
              aria-invalid={Boolean(errors.title)}
              {...register('title')}
            />
            {errors.title ? (
              <p className="text-xs text-destructive">
                {t('storefront.addresses.validationRequired')}
              </p>
            ) : null}
          </div>

          <div className="space-y-1">
            <Label htmlFor="addr-recipient">{t('storefront.addresses.recipientLabel')}</Label>
            <Input
              id="addr-recipient"
              aria-invalid={Boolean(errors.recipientName)}
              {...register('recipientName')}
            />
            {errors.recipientName ? (
              <p className="text-xs text-destructive">
                {t('storefront.addresses.validationRequired')}
              </p>
            ) : null}
          </div>
        </div>

        <div className="space-y-1">
          <Label htmlFor="addr-phone">{t('storefront.addresses.phoneLabel')}</Label>
          <Input
            id="addr-phone"
            dir="ltr"
            type="tel"
            inputMode="tel"
            placeholder={t('storefront.phonePlaceholder')}
            aria-invalid={Boolean(errors.phone)}
            {...register('phone')}
          />
          {errors.phone ? (
            <p className="text-xs text-destructive">{t('storefront.phoneInvalid')}</p>
          ) : null}
        </div>

        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <Label htmlFor="addr-province">{t('storefront.addresses.provinceLabel')}</Label>
            <Input
              id="addr-province"
              placeholder={t('storefront.addresses.provincePlaceholder')}
              aria-invalid={Boolean(errors.province)}
              {...register('province')}
            />
            {errors.province ? (
              <p className="text-xs text-destructive">
                {t('storefront.addresses.validationRequired')}
              </p>
            ) : null}
          </div>

          <div className="space-y-1">
            <Label htmlFor="addr-city">{t('storefront.addresses.cityLabel')}</Label>
            <Input
              id="addr-city"
              placeholder={t('storefront.addresses.cityPlaceholder')}
              aria-invalid={Boolean(errors.city)}
              {...register('city')}
            />
            {errors.city ? (
              <p className="text-xs text-destructive">
                {t('storefront.addresses.validationRequired')}
              </p>
            ) : null}
          </div>
        </div>

        <div className="space-y-1">
          <Label htmlFor="addr-line">{t('storefront.addresses.addressLineLabel')}</Label>
          <Textarea
            id="addr-line"
            placeholder={t('storefront.addresses.addressLinePlaceholder')}
            aria-invalid={Boolean(errors.addressLine)}
            {...register('addressLine')}
          />
          {errors.addressLine ? (
            <p className="text-xs text-destructive">
              {t('storefront.addresses.validationRequired')}
            </p>
          ) : null}
        </div>

        <div className="grid grid-cols-3 gap-2">
          <div className="space-y-1">
            <Label htmlFor="addr-plaque">{t('storefront.addresses.plaqueLabel')}</Label>
            <Input id="addr-plaque" dir="ltr" {...register('plaque')} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="addr-unit">{t('storefront.addresses.unitLabel')}</Label>
            <Input id="addr-unit" dir="ltr" {...register('unit')} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="addr-postal">{t('storefront.addresses.postalCodeLabel')}</Label>
            <Input id="addr-postal" dir="ltr" inputMode="numeric" {...register('postalCode')} />
          </div>
        </div>

        <div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-1.5">
          <Label htmlFor="addr-default-switch" className="cursor-pointer">
            {t('storefront.addresses.makeDefaultLabel')}
          </Label>
          <Controller
            control={control}
            name="isDefault"
            render={({ field }) => (
              <Switch
                id="addr-default-switch"
                checked={field.value}
                onCheckedChange={field.onChange}
                ariaLabel={t('storefront.addresses.makeDefaultLabel')}
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
