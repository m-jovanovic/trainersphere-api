import type { QueryClient } from '@tanstack/react-query';
import { authApi } from '@/lib/api/endpoints';
import { session } from '@/lib/auth/session';
import { clearPersistedCache } from '@/lib/query-client';
import { clearIdAliases } from '@/features/session/id-aliases';

export async function logout(queryClient: QueryClient): Promise<void> {
  try {
    await authApi.logout();
  } catch {
    // The cookie is cleared server-side when reachable; locally we sign out regardless.
  }
  session.clear();
  queryClient.clear();
  clearIdAliases();
  await clearPersistedCache();
}
