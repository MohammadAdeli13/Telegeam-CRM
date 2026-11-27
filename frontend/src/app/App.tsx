import { RouterProvider } from 'react-router';
import type { TelegramEnvironment } from '@/shared/lib/telegram';
import { AppProviders } from './providers';
import { router } from './router';

export function App({ telegram }: { telegram: TelegramEnvironment }) {
  return (
    <AppProviders telegram={telegram}>
      <RouterProvider router={router} />
    </AppProviders>
  );
}
