import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router';
import { describe, expect, it } from 'vitest';
import { AppProviders } from '@/app/providers';
import { AuthPage } from '@/features/auth/AuthPage';
import '@/shared/lib/i18n';
import { setAppLanguage } from '@/shared/lib/i18n';

const browserTelegramEnv = {
  inTelegram: false,
  initDataRaw: null,
  startParam: null,
  colorScheme: 'dark' as const,
  userLanguageCode: null,
};

describe('App Shell i18n, RTL/LTR and Theme controls', () => {
  it('renders Persian RTL by default and switches cleanly to English LTR', async () => {
    setAppLanguage('fa');
    const user = userEvent.setup();

    render(
      <AppProviders telegram={browserTelegramEnv}>
        <MemoryRouter>
          <AuthPage />
        </MemoryRouter>
      </AppProviders>,
    );

    expect(document.documentElement.dir).toBe('rtl');
    expect(document.documentElement.lang).toBe('fa');
    expect(screen.getByText('تلگرام CRM')).toBeInTheDocument();

    const langButton = screen.getByRole('button', { name: 'تغییر زبان' });
    await user.click(langButton);

    expect(document.documentElement.dir).toBe('ltr');
    expect(document.documentElement.lang).toBe('en');
    expect(screen.getByText('Telegram CRM')).toBeInTheDocument();
  });
});
