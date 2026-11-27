import { Languages, Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { normalizeLanguage, setAppLanguage } from '@/shared/lib/i18n';
import { useTheme } from '@/shared/lib/theme-context';
import { Button } from './button';

export function ToolbarControls() {
  const { t, i18n } = useTranslation();
  const { theme, toggleTheme } = useTheme();
  const currentLang = normalizeLanguage(i18n.language);

  const handleToggleLanguage = () => {
    setAppLanguage(currentLang === 'fa' ? 'en' : 'fa');
  };

  return (
    <div className="flex items-center gap-2">
      <Button
        type="button"
        variant="outline"
        size="sm"
        onClick={handleToggleLanguage}
        aria-label={t('app.languageToggle')}
        className="px-2.5"
      >
        <Languages className="size-4" aria-hidden="true" />
        <span>{currentLang === 'fa' ? 'EN' : 'فا'}</span>
      </Button>
      <Button
        type="button"
        variant="outline"
        size="icon"
        onClick={toggleTheme}
        aria-label={t('app.themeToggle')}
      >
        {theme === 'dark' ? (
          <Sun className="size-4" aria-hidden="true" />
        ) : (
          <Moon className="size-4" aria-hidden="true" />
        )}
      </Button>
    </div>
  );
}
