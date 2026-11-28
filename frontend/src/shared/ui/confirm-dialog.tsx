import { AlertTriangle } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { Button } from './button';
import { ModalDialog } from './dialog';

export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel,
  variant = 'destructive',
  busy = false,
  onConfirm,
  onCancel,
}: {
  open: boolean;
  title: string;
  description: string;
  confirmLabel?: string;
  cancelLabel?: string;
  variant?: 'destructive' | 'default';
  busy?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}) {
  const { t } = useTranslation();

  return (
    <ModalDialog open={open} title={title} onClose={onCancel} className="sm:max-w-md">
      <div className="space-y-4">
        <div className="flex items-start gap-3">
          {variant === 'destructive' ? (
            <div className="flex size-10 shrink-0 items-center justify-center rounded-full bg-destructive/15 text-destructive">
              <AlertTriangle className="size-5" aria-hidden="true" />
            </div>
          ) : null}
          <p className="text-sm text-foreground">{description}</p>
        </div>

        <div className="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
          <Button
            type="button"
            variant="outline"
            onClick={onCancel}
            disabled={busy}
            className="w-full sm:w-auto"
          >
            {cancelLabel ?? t('common.cancel')}
          </Button>
          <Button
            type="button"
            variant={variant}
            onClick={onConfirm}
            disabled={busy}
            className="w-full sm:w-auto"
          >
            {confirmLabel ?? t('common.confirm')}
          </Button>
        </div>
      </div>
    </ModalDialog>
  );
}
