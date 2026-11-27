import createClient, { type Middleware } from 'openapi-fetch';
import type { paths } from '@/types/api';
import { clearSession, getAccessToken, setSession, type StoredSession } from './session';

type ReauthenticateHandler = () => Promise<StoredSession | null>;
type SessionExpiredHandler = () => void;

let reauthenticateHandler: ReauthenticateHandler | null = null;
let sessionExpiredHandler: SessionExpiredHandler | null = null;
let inflightReauth: Promise<StoredSession | null> | null = null;

export function registerAuthHandlers(handlers: {
  reauthenticate: ReauthenticateHandler | null;
  onSessionExpired: SessionExpiredHandler | null;
}): void {
  reauthenticateHandler = handlers.reauthenticate;
  sessionExpiredHandler = handlers.onSessionExpired;
}

const RETRIED_HEADER = 'X-Crm-Auth-Retried';

const authMiddleware: Middleware = {
  async onRequest({ request }) {
    const token = getAccessToken();
    if (token && !request.headers.has('Authorization')) {
      request.headers.set('Authorization', `Bearer ${token}`);
    }
    return request;
  },
  async onResponse({ request, response }) {
    const url = new URL(request.url, window.location.origin);
    const isAuthRoute = url.pathname.startsWith('/api/v1/auth/telegram');

    if (response.status !== 401 || isAuthRoute) {
      return response;
    }

    if (request.headers.get(RETRIED_HEADER) === '1' || !reauthenticateHandler) {
      clearSession();
      sessionExpiredHandler?.();
      return response;
    }

    clearSession();

    if (!inflightReauth) {
      inflightReauth = reauthenticateHandler().finally(() => {
        inflightReauth = null;
      });
    }

    const refreshed = await inflightReauth;
    if (!refreshed) {
      sessionExpiredHandler?.();
      return response;
    }

    setSession(refreshed);

    const retryHeaders = new Headers(request.headers);
    retryHeaders.set('Authorization', `Bearer ${refreshed.accessToken}`);
    retryHeaders.set(RETRIED_HEADER, '1');

    const retriedRequest = new Request(request, { headers: retryHeaders });
    const retriedResponse = await fetch(retriedRequest);

    if (retriedResponse.status === 401) {
      clearSession();
      sessionExpiredHandler?.();
    }

    return retriedResponse;
  },
};

export const apiClient = createClient<paths>({
  baseUrl: '',
});

apiClient.use(authMiddleware);

export function extractProblemMessage(error: unknown, fallback: string): string {
  if (!error || typeof error !== 'object') {
    return fallback;
  }
  const record = error as Record<string, unknown>;
  if (typeof record.detail === 'string' && record.detail.trim().length > 0) {
    return record.detail;
  }
  if (typeof record.title === 'string' && record.title.trim().length > 0) {
    return record.title;
  }
  return fallback;
}

export function extractProblemCode(error: unknown): string | null {
  if (!error || typeof error !== 'object') {
    return null;
  }
  const record = error as Record<string, unknown>;
  return typeof record.code === 'string' ? record.code : null;
}
