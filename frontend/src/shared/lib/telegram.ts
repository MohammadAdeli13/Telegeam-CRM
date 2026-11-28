import {
  backButton,
  init,
  isTMA,
  miniApp,
  retrieveLaunchParams,
  retrieveRawInitData,
  themeParams,
} from '@tma.js/sdk-react';

interface TelegramHapticBridge {
  impactOccurred?: (style: 'light' | 'medium' | 'heavy' | 'rigid' | 'soft') => void;
  notificationOccurred?: (type: 'error' | 'success' | 'warning') => void;
  selectionChanged?: () => void;
}

interface TelegramWebAppBridge {
  initData?: string;
  initDataUnsafe?: {
    user?: {
      id?: number;
      language_code?: string;
      username?: string;
    };
  };
  colorScheme?: 'dark' | 'light';
  ready?: () => void;
  expand?: () => void;
  setHeaderColor?: (color: string) => void;
  setBackgroundColor?: (color: string) => void;
  openTelegramLink?: (url: string) => void;
  openLink?: (url: string) => void;
  HapticFeedback?: TelegramHapticBridge;
  onEvent?: (eventType: string, callback: () => void) => void;
  offEvent?: (eventType: string, callback: () => void) => void;
}

declare global {
  interface Window {
    Telegram?: {
      WebApp?: TelegramWebAppBridge;
    };
  }
}

export interface TelegramEnvironment {
  inTelegram: boolean;
  initDataRaw: string | null;
  startParam: string | null;
  colorScheme: 'dark' | 'light' | null;
  userLanguageCode: string | null;
}

let initialized = false;

interface BackStackEntry {
  id: number;
  handler: () => void;
}

let nextBackId = 1;
const backStack: BackStackEntry[] = [];
let activeBackOff: (() => void) | null = null;

function refreshTelegramBackButton(): void {
  try {
    if (activeBackOff) {
      activeBackOff();
      activeBackOff = null;
    }

    if (!backButton.isSupported()) {
      return;
    }

    if (!backButton.isMounted() && backButton.mount.isAvailable()) {
      backButton.mount();
    }

    const top = backStack[backStack.length - 1];
    if (top) {
      if (backButton.show.isAvailable()) {
        backButton.show();
      }
      if (backButton.onClick.isAvailable()) {
        activeBackOff = backButton.onClick(top.handler);
      }
    } else if (backButton.hide.isAvailable()) {
      backButton.hide();
    }
  } catch {
    // Ignore outside Telegram WebView.
  }
}

/**
 * Safely bootstraps the Telegram Mini App SDK when running inside Telegram WebView,
 * expands the viewport to full height, and degrades gracefully in standard browsers.
 */
export function initTelegramEnvironment(): TelegramEnvironment {
  let inTelegram = false;
  let initDataRaw: string | null = null;
  let startParam: string | null = null;
  let colorScheme: 'dark' | 'light' | null = null;
  let userLanguageCode: string | null = null;

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

  if (typeof window !== 'undefined') {
    try {
      window.Telegram?.WebApp?.ready?.();
      window.Telegram?.WebApp?.expand?.();
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

  if (typeof window !== 'undefined') {
    const lang = window.Telegram?.WebApp?.initDataUnsafe?.user?.language_code;
    if (typeof lang === 'string' && lang.trim().length > 0) {
      userLanguageCode = lang.trim();
    }
  }

  return { inTelegram, initDataRaw, startParam, colorScheme, userLanguageCode };
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

/**
 * Pushes a Telegram native BackButton handler onto the LIFO stack.
 * Modals push above route handlers so pressing Back closes the modal first.
 */
export function pushTelegramBackButton(onBack: () => void): () => void {
  const id = nextBackId++;
  backStack.push({ id, handler: onBack });
  refreshTelegramBackButton();

  return () => {
    const index = backStack.findIndex((entry) => entry.id === id);
    if (index >= 0) {
      backStack.splice(index, 1);
      refreshTelegramBackButton();
    }
  };
}

export function syncTelegramBackButton(visible: boolean, onBack: () => void): () => void {
  if (!visible) {
    refreshTelegramBackButton();
    return () => undefined;
  }
  return pushTelegramBackButton(onBack);
}

export function syncTelegramChromeColors(theme: 'dark' | 'light'): void {
  if (typeof window === 'undefined') {
    return;
  }
  const headerHex = theme === 'dark' ? '#111827' : '#ffffff';
  const bgHex = theme === 'dark' ? '#0b1120' : '#f8fafc';
  try {
    window.Telegram?.WebApp?.setHeaderColor?.(headerHex);
    window.Telegram?.WebApp?.setBackgroundColor?.(bgHex);
  } catch {
    // Non-fatal outside Telegram WebView.
  }
}

export function subscribeTelegramThemeChange(
  onChange: (scheme: 'dark' | 'light') => void,
): () => void {
  if (typeof window === 'undefined') {
    return () => undefined;
  }
  const webApp = window.Telegram?.WebApp;
  if (!webApp?.onEvent || !webApp.offEvent) {
    return () => undefined;
  }
  const listener = () => {
    const scheme = window.Telegram?.WebApp?.colorScheme;
    if (scheme === 'dark' || scheme === 'light') {
      onChange(scheme);
    }
  };
  try {
    webApp.onEvent('themeChanged', listener);
    return () => {
      try {
        webApp.offEvent?.('themeChanged', listener);
      } catch {
        // Ignore.
      }
    };
  } catch {
    return () => undefined;
  }
}

export function triggerHaptic(
  type: 'light' | 'medium' | 'success' | 'warning' | 'error' | 'selection',
): void {
  if (typeof window === 'undefined') {
    return;
  }
  const haptic = window.Telegram?.WebApp?.HapticFeedback;
  if (!haptic) {
    return;
  }
  try {
    if (type === 'selection') {
      haptic.selectionChanged?.();
    } else if (type === 'light' || type === 'medium') {
      haptic.impactOccurred?.(type);
    } else {
      haptic.notificationOccurred?.(type);
    }
  } catch {
    // Ignore on desktop / unsupported Telegram clients.
  }
}

export function openTelegramUrl(url: string): void {
  if (typeof window === 'undefined') {
    return;
  }
  try {
    if (url.startsWith('https://t.me/') && window.Telegram?.WebApp?.openTelegramLink) {
      window.Telegram.WebApp.openTelegramLink(url);
      return;
    }
    if (url.startsWith('tg://')) {
      window.location.href = url;
      return;
    }
  } catch {
    // Fallback below.
  }
  window.open(url, '_blank', 'noopener,noreferrer');
}

export async function copyTextToClipboard(text: string): Promise<boolean> {
  if (typeof window === 'undefined') {
    return false;
  }
  try {
    if (navigator.clipboard && typeof navigator.clipboard.writeText === 'function') {
      await navigator.clipboard.writeText(text);
      return true;
    }
  } catch {
    // Fallback to execCommand below for restricted WebViews.
  }

  try {
    const textarea = document.createElement('textarea');
    textarea.value = text;
    textarea.setAttribute('readonly', '');
    textarea.style.position = 'fixed';
    textarea.style.opacity = '0';
    document.body.appendChild(textarea);
    textarea.select();
    const ok = document.execCommand('copy');
    document.body.removeChild(textarea);
    return ok;
  } catch {
    return false;
  }
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
