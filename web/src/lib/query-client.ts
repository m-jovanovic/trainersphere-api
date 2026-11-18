import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';
import { createSyncStoragePersister } from '@tanstack/query-sync-storage-persister';
import { toast } from 'sonner';
import { ApiError, NetworkError, errorMessage } from '@/lib/api/http';

const retryQuery = (failureCount: number, error: unknown) => {
  if (error instanceof ApiError && error.status < 500) return false;
  return failureCount < 2;
};

export function createQueryClient(): QueryClient {
  return new QueryClient({
    queryCache: new QueryCache(),
    mutationCache: new MutationCache({
      onError: (error, _vars, _ctx, mutation) => {
        // Mutations with their own onError (forms) handle display themselves.
        if (mutation.options.onError || error instanceof NetworkError) return;
        toast.error(errorMessage(error));
      },
    }),
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        gcTime: 24 * 60 * 60 * 1000,
        retry: retryQuery,
        refetchOnWindowFocus: true,
      },
      mutations: {
        retry: false,
      },
    },
  });
}

export const CACHE_KEY = 'ts-query-cache';

export function createPersister() {
  return createSyncStoragePersister({ storage: typeof window === 'undefined' ? undefined : window.localStorage, key: CACHE_KEY, throttleTime: 500 });
}

export const PERSIST_BUSTER = 'contract-v1';
