import { cn } from '@/shared/lib/cn';

export function Switch({
  id,
  checked,
  onCheckedChange,
  disabled = false,
  ariaLabel,
}: {
  id?: string;
  checked: boolean;
  onCheckedChange: (next: boolean) => void;
  disabled?: boolean;
  ariaLabel?: string;
}) {
  return (
    <button
      id={id}
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={ariaLabel}
      disabled={disabled}
      onClick={() => onCheckedChange(!checked)}
      className="inline-flex min-h-11 min-w-11 cursor-pointer items-center justify-center disabled:cursor-not-allowed disabled:opacity-50"
    >
      <span
        className={cn(
          'flex h-7 w-12 items-center rounded-full border p-0.5 transition-colors',
          checked
            ? 'justify-end border-primary bg-primary'
            : 'justify-start border-border bg-secondary',
        )}
      >
        <span
          className={cn(
            'size-5 rounded-full shadow-xs transition-transform',
            checked ? 'bg-primary-foreground' : 'bg-surface',
          )}
        />
      </span>
    </button>
  );
}
