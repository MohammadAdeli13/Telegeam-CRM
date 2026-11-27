import {
  backButton,
  init,
  isTMA,
  miniApp,
  retrieveLaunchParams,
  retrieveRawInitData,
  themeParams,
} from '@tma.js/sdk-react';

declare global {
  interface Window {
    Telegram?: {
      WebApp?: {
        initData?: string;
        colorScheme?: 'dark' | 'light';
        ready?: () => void;
        expand?: () => void;
      };
    };
  }
}

export interface TelegramEnvironment {
  inTelegram: boolean;
  initDataRaw: string | null;
  startParam: string | null;
  colorScheme: 'dark' | 'light' | null;
}

let initialized = false;

/**
 * Safely bootstraps the Telegram Mini App SDK when running inside Telegram WebView,
 * and degrades gracefully when running in a standard desktop/mobile browser or Vitest.
 */
export function initTelegramEnvironment(): TelegramEnvironment {
  let inTelegram = false;
  let initDataRaw: string | null = null;
  let startParam: string | null = null;
  let colorScheme: 'dark' | 'light' | null = null;

  try {
    inTelegram = isTMA();
  } catch {
    inTelegram = false;
  }

  if (inTelegram && !initialized) {
    try {
      init();
      initialized = true;
    } catch {
      // Non-fatal: continue with best-effort fallback.
    }

    try {
      if (miniApp.mount.isAvailable()) {
        miniApp.mount();
      }
      if (miniApp.ready.isAvailable()) {
        miniApp.ready();
      }
    } catch {
      // Non-fatal outside full Telegram bridge.
    }

    try {
      if (themeParams.mount.isAvailable()) {
        themeParams.mount();
      }
    } catch {
      // Non-fatal.
    }
  }

  try {
    const raw = retrieveRawInitData();
    if (typeof raw === 'string' && raw.trim().length > 0) {
      initDataRaw = raw;
      inTelegram = true;
    }
  } catch {
    // Fallback to window.Telegram below.
  }

  if (!initDataRaw && typeof window !== 'undefined') {
    const legacyRaw = window.Telegram?.WebApp?.initData;
    if (typeof legacyRaw === 'string' && legacyRaw.trim().length > 0) {
      initDataRaw = legacyRaw;
      inTelegram = true;
    }
  }

  try {
    const params = retrieveLaunchParams();
    if (typeof params.tgWebAppStartParam === 'string' && params.tgWebAppStartParam.trim().length > 0) {
      startParam = params.tgWebAppStartParam.trim();
    }
    if (typeof params.tgWebAppThemeParams?.bg_color === 'string') {
      colorScheme = isDarkHex(params.tgWebAppThemeParams.bg_color) ? 'dark' : 'light';
    }
  } catch {
    // Ignore outside Telegram.
  }

  if (!startParam && typeof window !== 'undefined') {
    const urlParam = new URLSearchParams(window.location.search).get('startapp');
    if (urlParam && urlParam.trim().length > 0) {
      startParam = urlParam.trim();
    }
  }

  if (!colorScheme && typeof window !== 'undefined') {
    const scheme = window.Telegram?.WebApp?.colorScheme;
    if (scheme === 'dark' || scheme === 'light') {
      colorScheme = scheme;
    }
  }

  return { inTelegram, initDataRaw, startParam, colorScheme };
}

export function extractShopSlugFromStartParam(startParam: string | null): string | null {
  if (!startParam) {
    return null;
  }
  const normalized = startParam.trim();
  if (!normalized.toLowerCase().startsWith('shop_')) {
    return null;
  }
  const slug = normalized.slice('shop_'.length).trim().toLowerCase();
  return /^[a-z0-9-]{3,40}$/.test(slug) ? slug : null;
}

export function syncTelegramBackButton(visible: boolean, onBack: () => void): () => void {
  try {
    if (!backButton.isSupported()) {
      return () => undefined;
    }
    if (!backButton.isMounted() && backButton.mount.isAvailable()) {
      backButton.mount();
    }
    if (visible && backButton.show.isAvailable()) {
      backButton.show();
      if (backButton.onClick.isAvailable()) {
        return backButton.onClick(onBack);
      }
    } else if (!visible && backButton.hide.isAvailable()) {
      backButton.hide();
    }
  } catch {
    // Ignore outside Telegram WebView.
  }
  return () => undefined;
}

function isDarkHex(hex: string): boolean {
  const clean = hex.replace('#', '');
  if (clean.length !== 6) {
    return true;
  }
  const r = Number.parseInt(clean.slice(0, 2), 16);
  const g = Number.parseInt(clean.slice(2, 4), 16);
  const b = Number.parseInt(clean.slice(4, 6), 16);
  const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
  return luminance < 0.5;
}
