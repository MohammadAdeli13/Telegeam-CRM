import { createContext, useContext } from 'react';
import type { StoredSession, TenantSummary } from '@/shared/api/session';
import type { TelegramEnvironment } from '@/shared/lib/telegram';

export type AuthStatus =
  | 'bootstrapping'
  | 'authenticated'
  | 'unauthenticated'
  | 'reopen_required';

export type ActiveTenantPatch = Partial<
  Pick<
    TenantSummary,
    'name' | 'defaultLanguage' | 'currency' | 'logoUrl' | 'defaultProductImageUrl'
  >
>;

export interface AuthContextValue {
  status: AuthStatus;
  session: StoredSession | null;
  telegram: TelegramEnvironment;
  errorMessage: string | null;
  signInWithInitData: (rawInitData: string) => Promise<StoredSession | null>;
  switchTenant: (tenantId: string) => Promise<StoredSession | null>;
  refreshSessionAfterShopCreated: (tenantId: string) => Promise<StoredSession | null>;
  updateActiveTenant: (patch: ActiveTenantPatch) => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) {
    throw new Error('useAuth must be used inside AuthProvider.');
  }
  return value;
}
