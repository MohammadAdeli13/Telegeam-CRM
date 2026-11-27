import { DirectionProvider } from '@radix-ui/react-direction';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { AuthProvider } from '@/features/auth/AuthProvider';
import { applyDocumentDirection, getDirection, normalizeLanguage } from '@/shared/lib/i18n';
import type { TelegramEnvironment } from '@/shared/lib/telegram';
import { ThemeProvider } from '@/shared/lib/ThemeProvider';

export function AppProviders({
  telegram,
  children,
}: {
  telegram: TelegramEnvironment;
  children: ReactNode;
}) {
  const { i18n } = useTranslation();
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            retry: 1,
            staleTime: 30_000,
            refetchOnWindowFocus: false,
          },
        },
      }),
  );

  const lang = normalizeLanguage(i18n.language);
  const dir = getDirection(lang);

  useEffect(() => {
    applyDocumentDirection(lang);
  }, [lang]);

  return (
    <DirectionProvider dir={dir}>
      <ThemeProvider initialTheme={telegram.colorScheme}>
        <QueryClientProvider client={queryClient}>
          <AuthProvider telegram={telegram}>{children}</AuthProvider>
        </QueryClientProvider>
      </ThemeProvider>
    </DirectionProvider>
  );
}
