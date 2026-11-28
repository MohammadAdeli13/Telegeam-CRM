import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { subscribeTelegramThemeChange, syncTelegramChromeColors } from './telegram';
import { ThemeContext, type ThemeMode } from './theme-context';

const THEME_KEY = 'telegram-crm:theme';

function readInitialTheme(preferred?: ThemeMode | null): ThemeMode {
  try {
    const saved = window.sessionStorage.getItem(THEME_KEY);
    if (saved === 'dark' || saved === 'light') {
      return saved;
    }
  } catch {
    // Ignore storage access errors.
  }
  if (preferred === 'dark' || preferred === 'light') {
    return preferred;
  }
  return 'dark';
}

export function ThemeProvider({
  initialTheme,
  children,
}: {
  initialTheme?: ThemeMode | null;
  children: ReactNode;
}) {
  const [theme, setThemeState] = useState<ThemeMode>(() => readInitialTheme(initialTheme));
  const [manualOverride, setManualOverride] = useState(false);

  useEffect(() => {
    const root = document.documentElement;
    if (theme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
    syncTelegramChromeColors(theme);
    try {
      window.sessionStorage.setItem(THEME_KEY, theme);
    } catch {
      // Ignore storage errors.
    }
  }, [theme]);

  useEffect(() => {
    if (manualOverride) {
      return;
    }
    return subscribeTelegramThemeChange((scheme) => {
      setThemeState(scheme);
    });
  }, [manualOverride]);

  const value = useMemo(
    () => ({
      theme,
      setTheme: (next: ThemeMode) => {
        setManualOverride(true);
        setThemeState(next);
      },
      toggleTheme: () => {
        setManualOverride(true);
        setThemeState((prev) => (prev === 'dark' ? 'light' : 'dark'));
      },
    }),
    [theme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
