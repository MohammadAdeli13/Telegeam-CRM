import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import en from '@/locales/en.json';
import fa from '@/locales/fa.json';

export type AppLanguage = 'fa' | 'en';
export type TextDirection = 'rtl' | 'ltr';

const LANGUAGE_KEY = 'telegram-crm:lang';

export function normalizeLanguage(raw?: string | null): AppLanguage {
  if (raw && raw.toLowerCase().startsWith('en')) {
    return 'en';
  }
  return 'fa';
}

export function getDirection(lang: AppLanguage): TextDirection {
  return lang === 'fa' ? 'rtl' : 'ltr';
}

export function applyDocumentDirection(lang: AppLanguage): void {
  if (typeof document === 'undefined') {
    return;
  }
  const dir = getDirection(lang);
  document.documentElement.lang = lang;
  document.documentElement.dir = dir;
}

export function hasExplicitLanguagePreference(): boolean {
  if (typeof window === 'undefined') {
    return false;
  }
  try {
    const saved = window.sessionStorage.getItem(LANGUAGE_KEY);
    return saved === 'fa' || saved === 'en';
  } catch {
    return false;
  }
}

function readInitialLanguage(): AppLanguage {
  try {
    const saved = window.sessionStorage.getItem(LANGUAGE_KEY);
    if (saved === 'fa' || saved === 'en') {
      return saved;
    }
  } catch {
    // Ignore storage access errors in restricted WebViews.
  }
  return 'fa';
}

const initialLanguage = typeof window !== 'undefined' ? readInitialLanguage() : 'fa';

void i18n.use(initReactI18next).init({
  resources: {
    fa: { translation: fa },
    en: { translation: en },
  },
  lng: initialLanguage,
  fallbackLng: 'fa',
  interpolation: {
    escapeValue: false,
  },
});

applyDocumentDirection(initialLanguage);

export function setAppLanguage(lang: AppLanguage, options?: { explicit?: boolean }): void {
  const explicit = options?.explicit ?? true;
  void i18n.changeLanguage(lang);
  applyDocumentDirection(lang);
  if (!explicit) {
    return;
  }
  try {
    window.sessionStorage.setItem(LANGUAGE_KEY, lang);
  } catch {
    // Ignore storage access errors.
  }
}

export default i18n;
