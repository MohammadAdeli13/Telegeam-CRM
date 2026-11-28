import { AlertTriangle, Inbox, RefreshCw } from 'lucide-react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from './button';
import { Card } from './card';
import { Skeleton } from './skeleton';

export function LoadingState({ rows = 3 }: { rows?: number }) {
  const { t } = useTranslation();
  return (
    <div role="status" aria-label={t('app.loading')} className="space-y-3">
      {Array.from({ length: rows }).map((_, index) => (
        <Card key={index} className="space-y-2.5">
          <Skeleton className="h-4 w-2/5" />
          <Skeleton className="h-3.5 w-4/5" />
          <Skeleton className="h-3.5 w-3/5" />
        </Card>
      ))}
    </div>
  );
}

export function EmptyState({
  title,
  description,
  action,
  icon,
  embedded = false,
}: {
  title?: string;
  description: string;
  action?: ReactNode;
  icon?: ReactNode;
  embedded?: boolean;
}) {
  const { t } = useTranslation();
  const content = (
    <div className="flex flex-col items-center gap-3 py-8 text-center">
      <div className="flex size-12 items-center justify-center rounded-full bg-surface-elevated text-muted-foreground">
        {icon ?? <Inbox className="size-6" aria-hidden="true" />}
      </div>
      <div className="space-y-1">
        {title !== '' ? (
          <h3 className="text-base font-semibold text-foreground">{title ?? t('app.emptyTitle')}</h3>
        ) : null}
        <p className="max-w-sm text-sm text-muted-foreground">{description}</p>
      </div>
      {action ? <div className="pt-1">{action}</div> : null}
    </div>
  );

  if (embedded) {
    return content;
  }

  return <Card className="py-0">{content}</Card>;
}

export function ErrorState({
  title,
  message,
  onRetry,
  action,
}: {
  title?: string;
  message?: string;
  onRetry?: () => void;
  action?: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <Card role="alert" className="flex flex-col items-center gap-3 border-destructive/40 py-8 text-center">
      <div className="flex size-12 items-center justify-center rounded-full bg-destructive/15 text-destructive">
        <AlertTriangle className="size-6" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <h3 className="text-base font-semibold text-foreground">{title ?? t('app.errorTitle')}</h3>
        <p className="max-w-sm text-sm text-muted-foreground">{message ?? t('app.errorGeneric')}</p>
      </div>
      <div className="flex flex-wrap items-center justify-center gap-2 pt-1">
        {onRetry ? (
          <Button type="button" variant="outline" onClick={onRetry}>
            <RefreshCw className="size-4" aria-hidden="true" />
            {t('app.retry')}
          </Button>
        ) : null}
        {action}
      </div>
    </Card>
  );
}
