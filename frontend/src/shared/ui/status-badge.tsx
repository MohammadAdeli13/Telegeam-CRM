import {
  CheckCircle2,
  Clock,
  PackageCheck,
  Truck,
  XCircle,
  type LucideIcon,
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Badge } from './badge';

export type OrderStatusValue =
  | 'Pending'
  | 'Confirmed'
  | 'Preparing'
  | 'Shipped'
  | 'Delivered'
  | 'Cancelled';

const STATUS_META: Record<
  OrderStatusValue,
  {
    variant: 'warning' | 'default' | 'secondary' | 'success' | 'destructive';
    icon: LucideIcon;
  }
> = {
  Pending: { variant: 'warning', icon: Clock },
  Confirmed: { variant: 'default', icon: CheckCircle2 },
  Preparing: { variant: 'secondary', icon: PackageCheck },
  Shipped: { variant: 'default', icon: Truck },
  Delivered: { variant: 'success', icon: CheckCircle2 },
  Cancelled: { variant: 'destructive', icon: XCircle },
};

export function OrderStatusBadge({ status }: { status: string }) {
  const { t } = useTranslation();
  const known = status in STATUS_META ? (status as OrderStatusValue) : 'Pending';
  const meta = STATUS_META[known];
  const Icon = meta.icon;

  return (
    <Badge variant={meta.variant}>
      <Icon className="size-3.5" aria-hidden="true" />
      <span>{t(`orders.statuses.${known}`)}</span>
    </Badge>
  );
}
