import { Suspense, type ReactNode } from 'react';
import { LoadingState } from '@/shared/ui/state-views';

export function LazyRoute({ children }: { children: ReactNode }) {
  return (
    <Suspense
      fallback={
        <div className="mx-auto max-w-3xl px-4 py-6">
          <LoadingState rows={3} />
        </div>
      }
    >
      {children}
    </Suspense>
  );
}
