import { useEffect } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '@/features/auth/auth-context';
import { extractShopSlugFromStartParam } from '@/shared/lib/telegram';
import { LoadingState } from '@/shared/ui/state-views';

export function EntryRedirect() {
  const navigate = useNavigate();
  const { status, session, telegram } = useAuth();

  useEffect(() => {
    const deepLinkSlug = extractShopSlugFromStartParam(telegram.startParam);
    if (deepLinkSlug) {
      void navigate(`/s/${deepLinkSlug}`, { replace: true });
      return;
    }

    if (status === 'bootstrapping') {
      return;
    }

    if (status === 'authenticated' && session) {
      if (session.activeTenant) {
        void navigate('/merchant', { replace: true });
      } else if (session.tenants.length === 0) {
        void navigate('/onboarding', { replace: true });
      } else {
        void navigate('/auth', { replace: true });
      }
      return;
    }

    void navigate('/auth', { replace: true });
  }, [navigate, session, status, telegram.startParam]);

  return (
    <div className="mx-auto max-w-lg px-4 py-8">
      <LoadingState rows={2} />
    </div>
  );
}
