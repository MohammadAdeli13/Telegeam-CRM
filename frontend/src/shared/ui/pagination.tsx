import { ChevronLeft, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { formatNumber } from '@/shared/lib/format';
import { getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import { Button } from './button';

export function Pagination({
  page,
  pageSize,
  total,
  onPageChange,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPageChange: (nextPage: number) => void;
}) {
  const { t, i18n } = useTranslation();
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1) {
    return null;
  }

  const lang = normalizeLanguage(i18n.language);
  const isRtl = getDirection(lang) === 'rtl';
  const PrevIcon = isRtl ? ChevronRight : ChevronLeft;
  const NextIcon = isRtl ? ChevronLeft : ChevronRight;

  return (
    <div className="flex items-center justify-between gap-2 pt-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={page <= 1}
        onClick={() => onPageChange(page - 1)}
      >
        <PrevIcon className="size-4" aria-hidden="true" />
        <span>{t('common.previous')}</span>
      </Button>
      <span className="tabular-nums text-xs font-medium text-muted-foreground">
        {t('common.pageOf', {
          page: formatNumber(page, lang),
          total: formatNumber(totalPages, lang),
        })}
      </span>
      <Button
        type="button"
        variant="outline"
        size="sm"
        disabled={page >= totalPages}
        onClick={() => onPageChange(page + 1)}
      >
        <span>{t('common.next')}</span>
        <NextIcon className="size-4" aria-hidden="true" />
      </Button>
    </div>
  );
}
