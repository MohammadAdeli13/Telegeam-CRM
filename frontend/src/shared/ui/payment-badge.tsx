import { CheckCircle2, Clock, FileCheck, XCircle, type LucideIcon } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from './badge';

export type PaymentStatusValue =
  | 'NotRequired'
  | 'AwaitingReceipt'
  | 'ReceiptSubmitted'
  | 'Verified'
  | 'Rejected';

const PAYMENT_META: Record<
  Exclude<PaymentStatusValue, 'NotRequired'>,
  {
    variant: 'warning' | 'default' | 'success' | 'destructive';
    icon: LucideIcon;
  }
> = {
  AwaitingReceipt: { variant: 'warning', icon: Clock },
  ReceiptSubmitted: { variant: 'default', icon: FileCheck },
  Verified: { variant: 'success', icon: CheckCircle2 },
  Rejected: { variant: 'destructive', icon: XCircle },
};

export function PaymentStatusBadge({ status }: { status?: string | null }) {
  const { t } = useTranslation();
  if (!status || status === 'NotRequired' || !(status in PAYMENT_META)) {
    return null;
  }

  const key = status as Exclude<PaymentStatusValue, 'NotRequired'>;
  const meta = PAYMENT_META[key];
  const Icon = meta.icon;

  return (
    <Badge variant={meta.variant}>
      <Icon className="size-3.5" aria-hidden="true" />
      <span>{t(`orders.paymentStatuses.${key}`)}</span>
    </Badge>
  );
}
