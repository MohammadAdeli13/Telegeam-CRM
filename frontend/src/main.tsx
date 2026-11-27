import '@/shared/lib/i18n';
import './index.css';
import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { App } from '@/app/App';
import { initTelegramEnvironment } from '@/shared/lib/telegram';

const telegram = initTelegramEnvironment();
const rootElement = document.getElementById('root');

if (!rootElement) {
  throw new Error('Root element #root was not found in index.html.');
}

createRoot(rootElement).render(
  <StrictMode>
    <App telegram={telegram} />
  </StrictMode>,
);
