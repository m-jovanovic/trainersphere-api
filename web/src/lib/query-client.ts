import { MutationCache, QueryCache, QueryClient } from '@tanstack/react-query';
import { createAsyncStoragePersister } from '@tanstack/query-async-storage-persister';
import { del, get, set } from 'idb-keyval';
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

// IndexedDB rather than localStorage: room for a few weeks of history and queued writes.
export function createPersister() {
  return createAsyncStoragePersister({
    storage: { getItem: (k: string) => get<string>(k), setItem: (k: string, v: string) => set(k, v), removeItem: (k: string) => del(k) },
    key: CACHE_KEY,
    throttleTime: 500,
  });
}

export async function clearPersistedCache(): Promise<void> {
  try {
    await del(CACHE_KEY);
  } catch {
    // IndexedDB unavailable (private mode); nothing persisted.
  }
}

export const PERSIST_BUSTER = 'contract-v1';
