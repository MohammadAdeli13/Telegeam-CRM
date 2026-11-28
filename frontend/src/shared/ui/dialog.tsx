import { X } from 'lucide-react';
import { useEffect, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '@/shared/lib/cn';
import { pushTelegramBackButton } from '@/shared/lib/telegram';
import { Button } from './button';

let openModalCount = 0;

export function ModalDialog({
  open,
  title,
  description,
  onClose,
  children,
  className,
}: {
  open: boolean;
  title: string;
  description?: string;
  onClose: () => void;
  children: ReactNode;
  className?: string;
}) {
  const { t } = useTranslation();

  useEffect(() => {
    if (!open) {
      return;
    }

    openModalCount += 1;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';

    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    const removeBackHandler = pushTelegramBackButton(onClose);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      removeBackHandler();
      openModalCount = Math.max(0, openModalCount - 1);
      if (openModalCount === 0) {
        document.body.style.overflow = prevOverflow;
      }
    };
  }, [open, onClose]);

  if (!open) {
    return null;
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-end justify-center bg-black/55 p-0 backdrop-blur-xs sm:items-center sm:p-4"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={(e) => {
        if (e.target === e.currentTarget) {
          onClose();
        }
      }}
    >
      <div
        className={cn(
          'safe-bottom max-h-[90dvh] w-full overflow-y-auto rounded-t-2xl border border-border bg-surface p-4 pb-6 text-foreground shadow-xl sm:max-w-lg sm:rounded-2xl sm:p-5',
          className,
        )}
      >
        <div className="mx-auto mb-3 h-1 w-10 rounded-full bg-border sm:hidden" aria-hidden="true" />
        <div className="mb-4 flex items-start justify-between gap-3 border-b border-border pb-3">
          <div className="min-w-0">
            <h2 className="text-base font-bold text-foreground">{title}</h2>
            {description ? (
              <p className="mt-0.5 text-xs text-muted-foreground">{description}</p>
            ) : null}
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={onClose}
            aria-label={t('common.close')}
            className="shrink-0"
          >
            <X className="size-4" aria-hidden="true" />
          </Button>
        </div>
        {children}
      </div>
    </div>
  );
}
