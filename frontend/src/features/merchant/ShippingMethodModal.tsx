
import { zodResolver } from '@hookform/resolvers/zod';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { useState } from 'react';

import { Controller, useForm } from 'react-hook-form';

import { useTranslation } from 'react-i18next';

import { z } from 'zod';

import { apiClient, extractProblemMessage } from '@/shared/api/client';

import { getCurrencyUnit, toLatinDigits, toNumber } from '@/shared/lib/format';

import { normalizeLanguage } from '@/shared/lib/i18n';

import { triggerHaptic } from '@/shared/lib/telegram';

import { Button } from '@/shared/ui/button';

import { ModalDialog } from '@/shared/ui/dialog';

import { Input } from '@/shared/ui/input';

import { Label } from '@/shared/ui/label';

import { Switch } from '@/shared/ui/switch';

import type { components } from '@/types/api';



type ShippingMethodDto = components['schemas']['ShippingMethodDto'];



const shippingMethodSchema = z.object({

name: z.string().trim().min(2).max(120),

description: z.string().trim().max(500),

isPayAtDestination: z.boolean(),

price: z

.string()

.trim()

.refine((v) => {

if (v === '') {

return true;

}

const n = Number(toLatinDigits(v));

return !Number.isNaN(n) && n >= 0;

}),

freeAboveAmount: z

.string()

.trim()

.refine((v) => {

if (v === '') {

return true;

}

const n = Number(toLatinDigits(v));

return !Number.isNaN(n) && n >= 0;

}),

estimatedDaysText: z.string().trim().max(80),

sortOrder: z

.string()

.trim()

.refine((v) => v === '' || (/^\d+$/.test(toLatinDigits(v)) && Number(toLatinDigits(v)) >= 0)),

isActive: z.boolean(),

});



type ShippingFormValues = z.infer<typeof shippingMethodSchema>;



export function ShippingMethodModal({

tenantKey,

currency,

editing,

onClose,

}: {

tenantKey: string;

currency: string;

editing: ShippingMethodDto | null;

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

watch,

handleSubmit,

formState: { errors },

} = useForm<ShippingFormValues>({

resolver: zodResolver(shippingMethodSchema),

defaultValues: {

name: editing?.name ?? '',

description: editing?.description ?? '',

isPayAtDestination: editing ? editing.isPayAtDestination : false,

price: editing ? String(toNumber(editing.price)) : '0',

freeAboveAmount:

editing && editing.freeAboveAmount !== null && editing.freeAboveAmount !== undefined

? String(toNumber(editing.freeAboveAmount))

: '',

estimatedDaysText: editing?.estimatedDaysText ?? '',

sortOrder: String(editing ? toNumber(editing.sortOrder) : 0),

isActive: editing ? editing.isActive : true,

},

});



const isPayAtDestination = watch('isPayAtDestination');



const mutation = useMutation({

mutationFn: async (values: ShippingFormValues) => {

const parsedPrice = values.isPayAtDestination

? 0

: Math.max(0, Number(toLatinDigits(values.price)) || 0);

const cleanFree = toLatinDigits(values.freeAboveAmount).trim();

const parsedFree =

values.isPayAtDestination || cleanFree === '' ? null : Math.max(0, Number(cleanFree));

const parsedOrder = Math.max(0, Number.parseInt(toLatinDigits(values.sortOrder), 10) || 0);



if (editing) {

const { error } = await apiClient.PATCH('/api/v1/merchant/shipping-methods/{id}', {

params: { path: { id: editing.id } },

body: {

name: values.name,

description: values.description || null,

price: parsedPrice,

freeAboveAmount: parsedFree,

clearFreeAboveAmount: parsedFree === null,

isPayAtDestination: values.isPayAtDestination,

estimatedDaysText: values.estimatedDaysText || null,

sortOrder: parsedOrder,

isActive: values.isActive,

},

});

if (error) {

throw error;

}

} else {

const { error } = await apiClient.POST('/api/v1/merchant/shipping-methods', {

body: {

name: values.name,

description: values.description || null,

price: parsedPrice,

freeAboveAmount: parsedFree,

isPayAtDestination: values.isPayAtDestination,

estimatedDaysText: values.estimatedDaysText || null,

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

void queryClient.invalidateQueries({ queryKey: ['merchant', tenantKey, 'shipping-methods'] });

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

title={editing ? t('merchant.shipping.editTitle') : t('merchant.shipping.addNew')}

onClose={onClose}

>

<form

onSubmit={(e) => void handleSubmit((vals) => mutation.mutate(vals))(e)}

className="space-y-3.5"

noValidate

>

<div className="space-y-1.5">

<Label htmlFor="ship-name">{t('merchant.shipping.nameLabel')}</Label>

<Input

id="ship-name"

placeholder={t('merchant.shipping.namePlaceholder')}

aria-invalid={Boolean(errors.name)}

{...register('name')}

/>

{errors.name ? (

<p className="text-xs text-destructive">{t('merchant.catalog.validationName')}</p>

) : null}

</div>



<div className="space-y-1.5">

<Label htmlFor="ship-desc">{t('merchant.shipping.descriptionLabel')}</Label>

<Input id="ship-desc" {...register('description')} />

</div>



<div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-1.5">

<Label htmlFor="ship-pay-dest" className="cursor-pointer text-xs">

{t('merchant.shipping.payAtDestinationLabel')}

</Label>

<Controller

control={control}

name="isPayAtDestination"

render={({ field }) => (

<Switch

id="ship-pay-dest"

checked={field.value}

onCheckedChange={field.onChange}

ariaLabel={t('merchant.shipping.payAtDestinationLabel')}

/>

)}

/>

</div>



{!isPayAtDestination ? (

<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">

<div className="space-y-1.5">

<Label htmlFor="ship-price">

{t('merchant.shipping.priceLabel', { currency: currencyUnit })}

</Label>

<Input

id="ship-price"

dir="ltr"

inputMode="decimal"

aria-invalid={Boolean(errors.price)}

{...register('price')}

/>

{errors.price ? (

<p className="text-xs text-destructive">{t('merchant.catalog.validationPrice')}</p>

) : null}

</div>



<div className="space-y-1.5">

<Label htmlFor="ship-free">

{t('merchant.shipping.freeAboveLabel', { currency: currencyUnit })}

</Label>

<Input

id="ship-free"

dir="ltr"

inputMode="decimal"

aria-invalid={Boolean(errors.freeAboveAmount)}

{...register('freeAboveAmount')}

/>

</div>

</div>

) : null}



<div className="grid grid-cols-2 gap-3">

<div className="space-y-1.5">

<Label htmlFor="ship-days">{t('merchant.shipping.estimatedDaysLabel')}</Label>

<Input

id="ship-days"

placeholder={t('merchant.shipping.estimatedDaysPlaceholder')}

{...register('estimatedDaysText')}

/>

</div>



<div className="space-y-1.5">

<Label htmlFor="ship-order">{t('merchant.shipping.sortOrderLabel')}</Label>

<Input id="ship-order" dir="ltr" inputMode="numeric" {...register('sortOrder')} />

</div>

</div>



<div className="flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-3 py-1.5">

<Label htmlFor="ship-active" className="cursor-pointer">

{t('merchant.shipping.activeLabel')}

</Label>

<Controller

control={control}

name="isActive"

render={({ field }) => (

<Switch

id="ship-active"

checked={field.value}

onCheckedChange={field.onChange}

ariaLabel={t('merchant.shipping.activeLabel')}

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

