import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { ThemeContext, type ThemeMode } from './theme-context';

const THEME_KEY = 'telegram-crm:theme';

function readInitialTheme(preferred?: ThemeMode | null): ThemeMode {
  if (preferred === 'dark' || preferred === 'light') {
    return preferred;
  }
  try {
    const saved = window.sessionStorage.getItem(THEME_KEY);
    if (saved === 'dark' || saved === 'light') {
      return saved;
    }
  } catch {
    // Ignore storage access errors.
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

  useEffect(() => {
    const root = document.documentElement;
    if (theme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
    try {
      window.sessionStorage.setItem(THEME_KEY, theme);
    } catch {
      // Ignore storage errors.
    }
  }, [theme]);

  const value = useMemo(
    () => ({
      theme,
      setTheme: (next: ThemeMode) => setThemeState(next),
      toggleTheme: () => setThemeState((prev) => (prev === 'dark' ? 'light' : 'dark')),
    }),
    [theme],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
