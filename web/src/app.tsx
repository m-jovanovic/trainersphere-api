import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { PersistQueryClientProvider } from '@tanstack/react-query-persist-client';
import { RouterProvider } from '@tanstack/react-router';
import { useEffect } from 'react';
import { Toaster } from 'sonner';
import { session } from '@/lib/auth/session';
import { createPersister, PERSIST_BUSTER } from '@/lib/query-client';
import { useTheme } from '@/lib/theme';
import type { createAppRouter } from './router';

type AppRouter = ReturnType<typeof createAppRouter>;

function SessionWatcher({ router }: { router: AppRouter }) {
  useEffect(() => {
    let hadUser = session.get().user !== null;
    return session.subscribe(() => {
      const hasUser = session.get().user !== null;
      // Refresh failed or the user logged out in this tab: re-run route guards.
      if (hadUser && !hasUser) void router.invalidate();
      hadUser = hasUser;
    });
  }, [router]);
  return null;
}

function ThemedToaster() {
  const [theme] = useTheme();
  return <Toaster theme={theme} position="top-center" richColors closeButton toastOptions={{ duration: 4000 }} />;
}

export function App({ queryClient, router, persist = true }: { queryClient: QueryClient; router: AppRouter; persist?: boolean }) {
  const content = (
    <>
      <RouterProvider router={router} />
      <SessionWatcher router={router} />
      <ThemedToaster />
    </>
  );
  if (!persist) return <QueryClientProvider client={queryClient}>{content}</QueryClientProvider>;
  return (
    <PersistQueryClientProvider
      client={queryClient}
      persistOptions={{ persister: createPersister(), buster: PERSIST_BUSTER, maxAge: 24 * 60 * 60 * 1000 }}
      onSuccess={() => void queryClient.resumePausedMutations()}
    >
      {content}
    </PersistQueryClientProvider>
  );
}
