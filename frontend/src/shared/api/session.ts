import type { components } from '@/types/api';

export type AuthUser = components['schemas']['AuthUserDto'];
export type TenantSummary = components['schemas']['TenantSummary'];
export type AuthSessionPayload = components['schemas']['AuthResponse'];

export interface StoredSession {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
  tenants: TenantSummary[];
  activeTenant: TenantSummary | null;
}

const SESSION_STORAGE_KEY = 'telegram-crm:session';

let memorySession: StoredSession | null = null;

function isValidSession(candidate: unknown): candidate is StoredSession {
  if (!candidate || typeof candidate !== 'object') {
    return false;
  }
  const obj = candidate as Record<string, unknown>;
  return (
    typeof obj.accessToken === 'string' &&
    obj.accessToken.length > 0 &&
    typeof obj.expiresAt === 'string' &&
    typeof obj.user === 'object' &&
    obj.user !== null &&
    Array.isArray(obj.tenants)
  );
}

export function getSession(): StoredSession | null {
  if (memorySession) {
    return memorySession;
  }
  if (typeof window === 'undefined') {
    return null;
  }
  try {
    const raw = window.sessionStorage.getItem(SESSION_STORAGE_KEY);
    if (!raw) {
      return null;
    }
    const parsed: unknown = JSON.parse(raw);
    if (!isValidSession(parsed)) {
      window.sessionStorage.removeItem(SESSION_STORAGE_KEY);
      return null;
    }
    memorySession = parsed;
    return memorySession;
  } catch {
    return null;
  }
}

export function getAccessToken(): string | null {
  return getSession()?.accessToken ?? null;
}

export function setSession(session: StoredSession): void {
  memorySession = session;
  if (typeof window === 'undefined') {
    return;
  }
  try {
    window.sessionStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify(session));
  } catch {
    // WebView may restrict storage; in-memory session remains active.
  }
}

export function patchActiveTenantInSession(
  patch: Partial<
    Pick<
      TenantSummary,
      'name' | 'defaultLanguage' | 'currency' | 'logoUrl' | 'defaultProductImageUrl'
    >
  >,
): StoredSession | null {
  const current = getSession();
  if (!current || !current.activeTenant) {
    return current;
  }

  const updatedActive: TenantSummary = {
    ...current.activeTenant,
    ...patch,
  };

  const updatedTenants = current.tenants.map((tenant) =>
    tenant.id === updatedActive.id ? { ...tenant, ...patch } : tenant,
  );

  const nextSession: StoredSession = {
    ...current,
    activeTenant: updatedActive,
    tenants: updatedTenants,
  };

  setSession(nextSession);
  return nextSession;
}

export function clearSession(): void {
  memorySession = null;
  if (typeof window === 'undefined') {
    return;
  }
  try {
    window.sessionStorage.removeItem(SESSION_STORAGE_KEY);
  } catch {
    // Ignore.
  }
}
