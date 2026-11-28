import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import {
  apiClient,
  extractProblemMessage,
  registerAuthHandlers,
} from '@/shared/api/client';
import {
  clearSession,
  getSession,
  patchActiveTenantInSession,
  setSession,
  type StoredSession,
} from '@/shared/api/session';
import {
  hasExplicitLanguagePreference,
  normalizeLanguage,
  setAppLanguage,
} from '@/shared/lib/i18n';
import type { TelegramEnvironment } from '@/shared/lib/telegram';
import { AuthContext, type ActiveTenantPatch, type AuthStatus } from './auth-context';

export function AuthProvider({
  telegram,
  children,
}: {
  telegram: TelegramEnvironment;
  children: ReactNode;
}) {
  const queryClient = useQueryClient();
  const [session, setSessionState] = useState<StoredSession | null>(() => getSession());
  const [status, setStatus] = useState<AuthStatus>(() => {
    if (getSession()) {
      return 'authenticated';
    }
    return telegram.initDataRaw ? 'bootstrapping' : 'unauthenticated';
  });
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const lastInitDataRef = useRef<string | null>(telegram.initDataRaw);
  const bootstrappedRef = useRef(false);

  const signInWithInitData = useCallback(
    async (rawInitData: string): Promise<StoredSession | null> => {
      const trimmed = rawInitData.trim();
      if (!trimmed) {
        return null;
      }
      lastInitDataRef.current = trimmed;
      setErrorMessage(null);
      setStatus('bootstrapping');

      const { data, error } = await apiClient.POST('/api/v1/auth/telegram', {
        body: { initData: trimmed },
      });

      if (error || !data) {
        clearSession();
        setSessionState(null);
        setStatus('unauthenticated');
        setErrorMessage(extractProblemMessage(error, 'Authentication failed.'));
        return null;
      }

      const nextSession: StoredSession = {
        accessToken: data.accessToken,
        expiresAt: data.expiresAt,
        user: data.user,
        tenants: data.tenants,
        activeTenant: data.activeTenant ?? null,
      };

      setSession(nextSession);
      setSessionState(nextSession);
      setStatus('authenticated');
      void queryClient.invalidateQueries();
      return nextSession;
    },
    [queryClient],
  );

  const switchTenant = useCallback(
    async (tenantId: string): Promise<StoredSession | null> => {
      setErrorMessage(null);
      const { data, error } = await apiClient.POST('/api/v1/auth/switch-tenant', {
        body: { tenantId },
      });

      if (error || !data) {
        setErrorMessage(extractProblemMessage(error, 'Could not switch shop.'));
        return null;
      }

      const nextSession: StoredSession = {
        accessToken: data.accessToken,
        expiresAt: data.expiresAt,
        user: data.user,
        tenants: data.tenants,
        activeTenant: data.activeTenant ?? null,
      };

      setSession(nextSession);
      setSessionState(nextSession);
      setStatus('authenticated');
      void queryClient.invalidateQueries();
      return nextSession;
    },
    [queryClient],
  );

  const updateActiveTenant = useCallback((patch: ActiveTenantPatch) => {
    const updated = patchActiveTenantInSession(patch);
    if (updated) {
      setSessionState(updated);
    }
  }, []);

  const signOut = useCallback(() => {
    clearSession();
    setSessionState(null);
    setStatus('unauthenticated');
    setErrorMessage(null);
    queryClient.clear();
  }, [queryClient]);

  useEffect(() => {
    if (!hasExplicitLanguagePreference() && telegram.userLanguageCode) {
      setAppLanguage(normalizeLanguage(telegram.userLanguageCode), { explicit: false });
    }
  }, [telegram.userLanguageCode]);

  useEffect(() => {
    registerAuthHandlers({
      reauthenticate: async () => {
        const initData = lastInitDataRef.current;
        if (!initData) {
          return null;
        }
        return signInWithInitData(initData);
      },
      onSessionExpired: () => {
        setSessionState(null);
        setStatus(telegram.inTelegram ? 'reopen_required' : 'unauthenticated');
      },
    });

    return () => {
      registerAuthHandlers({ reauthenticate: null, onSessionExpired: null });
    };
  }, [signInWithInitData, telegram.inTelegram]);

  useEffect(() => {
    if (bootstrappedRef.current) {
      return;
    }
    bootstrappedRef.current = true;

    if (!session && telegram.initDataRaw) {
      void signInWithInitData(telegram.initDataRaw);
    }
  }, [session, signInWithInitData, telegram.initDataRaw]);

  const value = useMemo(
    () => ({
      status,
      session,
      telegram,
      errorMessage,
      signInWithInitData,
      switchTenant,
      refreshSessionAfterShopCreated: switchTenant,
      updateActiveTenant,
      signOut,
    }),
    [
      status,
      session,
      telegram,
      errorMessage,
      signInWithInitData,
      switchTenant,
      updateActiveTenant,
      signOut,
    ],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
